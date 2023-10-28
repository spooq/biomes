using Cairo;
using HarmonyLib;
using Newtonsoft.Json;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Server;
using Vintagestory.ServerMods;
using Vintagestory.ServerMods.NoObf;

namespace Biomes
{
    public class BiomeConfig
    {
        public List<string> NorthernRealms = new List<string>();
        public List<string> SouthernRealms = new List<string>();
        public List<string> SpawnWhiteList = new List<string>();
        public Dictionary<string, List<string>> TreeBiomes = new Dictionary<string, List<string>>();
    }

    [ProtoContract]
    public class BiomeNameAndCoords
    {
        [ProtoMember(1)]
        public int mapRegionX;

        [ProtoMember(2)]
        public int mapRegionZ;

        [ProtoMember(3)]
        public string biome;
    }

    [HarmonyPatch]
    public class BiomesModSystem : ModSystem
    {
        public static ICoreServerAPI sapi;
        public Harmony harmony;

        private const string realmProperty = "biorealm";
        private const string hemisphereProperty = "hemisphere";

        public static BiomeConfig config;
        private static List<Regex> SpawnWhiteListRx = new List<Regex>
        {
        };

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);

            harmony = new Harmony(Mod.Info.ModID);
            harmony.PatchAll();

            sapi = api;

            config = JsonConvert.DeserializeObject<BiomeConfig>(sapi.Assets.Get("biomes:config/biomes.json").ToText());

            foreach (var item in config.SpawnWhiteList)
                SpawnWhiteListRx.Add(new Regex(item));

            sapi.Event.MapRegionGeneration(OnMapRegionGeneration, "standard");

            sapi.ChatCommands.Create("biome")
                .WithDescription("Biome main command")
                .RequiresPrivilege(Privilege.chat)
                .BeginSubCommand("show")
                    .HandleWith(onGetBiomeCommand)
                .EndSubCommand()
                .BeginSubCommand("trees")
                    .HandleWith(onTreesCommand)
                .EndSubCommand()
                .BeginSubCommand("debug")
                    .HandleWith(onDebugCommand)
                .EndSubCommand()
                .BeginSubCommand("hemisphere")
                    .WithArgs(sapi.ChatCommands.Parsers.WordRange("hemisphere", Enum.GetNames(typeof(EnumHemisphere))))
                    .HandleWith(onSetHemisphereCommand)
                .EndSubCommand()
                .BeginSubCommand("add")
                    .WithArgs(sapi.ChatCommands.Parsers.WordRange("realm", config.NorthernRealms.Union(config.SouthernRealms).Select(i => i.Replace(' ', '_')).ToArray()))
                    .HandleWith(onAddRealmCommand)
                .EndSubCommand()
                .BeginSubCommand("remove")
                    .WithArgs(sapi.ChatCommands.Parsers.WordRange("realm", config.NorthernRealms.Union(config.SouthernRealms).Select(i => i.Replace(' ', '_')).ToArray()))
                    .HandleWith(onRemoveRealmCommand)
                .EndSubCommand();
        }

        public override void Dispose()
        {
            harmony.UnpatchAll(Mod.Info.ModID);
            base.Dispose();
        }

        public static String NorthOrSouth(EnumHemisphere hemisphere, int realm)
        {
            return hemisphere == EnumHemisphere.North ? config.NorthernRealms[realm] : config.SouthernRealms[realm];
        }

        public void OnMapRegionGeneration(IMapRegion mapRegion, int regionX, int regionZ, ITreeAttribute chunkGenParams)
        {
            EnumHemisphere hemisphere;
            int currentRealm;

            var realmNames = new List<string>();
            CalculateValues(mapRegion, regionX, regionZ, out hemisphere, out currentRealm);
            realmNames.Add(NorthOrSouth(hemisphere, currentRealm));
            CalculateValues(null, regionX - 1, regionZ, out hemisphere, out currentRealm);
            realmNames.Add(NorthOrSouth(hemisphere, currentRealm));
            CalculateValues(null, regionX + 1, regionZ, out hemisphere, out currentRealm);
            realmNames.Add(NorthOrSouth(hemisphere, currentRealm));
            CalculateValues(null, regionX, regionZ - 1, out hemisphere, out currentRealm);
            realmNames.Add(NorthOrSouth(hemisphere, currentRealm));
            CalculateValues(null, regionX, regionZ + 1, out hemisphere, out currentRealm);
            realmNames.Add(NorthOrSouth(hemisphere, currentRealm));
            realmNames = realmNames.Distinct().ToList();

            setModProperty(mapRegion, realmProperty, ref realmNames);
        }

        private static void CalculateValues(IMapRegion mapRegion, int regionX, int regionZ, out EnumHemisphere hemisphere, out int currentRealm)
        {
            BlockPos blockPos = new BlockPos(regionX * sapi.WorldManager.RegionSize, 0, regionZ * sapi.WorldManager.RegionSize);
            hemisphere = sapi.World.Calendar.GetHemisphere(blockPos);
            setModProperty(mapRegion, hemisphereProperty, ref hemisphere);

            int realmCount;
            if (hemisphere == EnumHemisphere.North)
                realmCount = config.NorthernRealms.Count;
            else
                realmCount = config.SouthernRealms.Count;

            int worldWidthInRegions = sapi.WorldManager.MapSizeX / sapi.WorldManager.RegionSize;
            float realmWidthInRegions = worldWidthInRegions / (float)realmCount;
            currentRealm = 0;
            if (realmWidthInRegions != 0)
                currentRealm = (int)(regionX / realmWidthInRegions);
            if (currentRealm >= realmCount)
                currentRealm = realmCount - 1;
            if (currentRealm < 0)
                currentRealm = 0;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GenCreatures), "CanSpawnAtPosition")]
        public static bool CanSpawnAtPosition(GenCreatures __instance, ref bool __result, IBlockAccessor blockAccessor, EntityProperties type, BlockPos pos, BaseSpawnConditions sc)
        {
            IMapRegion mapRegion = blockAccessor.GetMapRegion(pos.X / blockAccessor.RegionSize, pos.Z / blockAccessor.RegionSize);
            __result = AllowSpawn(mapRegion, type);
            return __result;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ServerSystemEntitySpawner), "CanSpawnAt")]
        public static bool CanSpawnAt(ServerSystemEntitySpawner __instance, ref Vec3d __result, EntityProperties type, Vec3i spawnPosition, RuntimeSpawnConditions sc, IWorldChunk[] chunkCol)
        {
            IMapRegion mapRegion = chunkCol[0].MapChunk.MapRegion;
            return AllowSpawn(mapRegion, type);
        }

        public static bool AllowSpawn(IMapRegion mapRegion, EntityProperties type)
        {
            foreach (var rx in SpawnWhiteListRx)
                if (rx.IsMatch(type.Code.Path))
                    return true;

            // Only blessed animals get in.
            if (type.Attributes == null || !type.Attributes.KeyExists(realmProperty))
                return false;

            // Test map region attributes
            var regionRealms = new List<string>();
            getModProperty(mapRegion, realmProperty, ref regionRealms);
            var animalRealms = type.Attributes[realmProperty].AsArray<string>();
            return animalRealms.Intersect(regionRealms).Any();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GenVegetationAndPatches), "genShrubs")]
        public static bool genShrubsPrefix(ref GenVegetationAndPatches __instance, out List<TreeVariant> __state, int chunkX, int chunkZ)
        {
            var treeSupplier = Traverse.Create(__instance).Field("treeSupplier").GetValue() as WgenTreeSupplier;
            var treeGenProps = Traverse.Create(treeSupplier).Field("treeGenProps").GetValue() as TreeGenProperties;

            BlockPos pos = new BlockPos(chunkX * sapi.WorldManager.ChunkSize, chunkX * sapi.WorldManager.ChunkSize, chunkZ * sapi.WorldManager.ChunkSize);
            IWorldGenBlockAccessor blockAccessor = Traverse.Create(__instance).Field("blockAccessor").GetValue() as IWorldGenBlockAccessor;
            IMapRegion mapRegion = blockAccessor.GetMapRegion(pos.X / blockAccessor.RegionSize, pos.Z / blockAccessor.RegionSize);

            var regionRealms = new List<string>();
            getModProperty(mapRegion, realmProperty, ref regionRealms);

            __state = treeGenProps.ShrubGens.ToList();

            var treeVariants = new List<TreeVariant>();
            foreach (var gen in treeGenProps.ShrubGens)
            {
                var name = gen.Generator.GetName();
                if (!config.TreeBiomes.ContainsKey(name))
                    continue;

                if (config.TreeBiomes[name].Intersect(regionRealms).Any())
                    treeVariants.Add(gen);
            }

            treeGenProps.ShrubGens = treeVariants.ToArray();

            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GenVegetationAndPatches), "genShrubs")]
        public static void genShrubsPostFix(ref GenVegetationAndPatches __instance, List<TreeVariant> __state, int chunkX, int chunkZ)
        {
            var treeSupplier = Traverse.Create(__instance).Field("treeSupplier").GetValue() as WgenTreeSupplier;
            var treeGenProps = Traverse.Create(treeSupplier).Field("treeGenProps").GetValue() as TreeGenProperties;
            treeGenProps.ShrubGens = __state.ToArray();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GenVegetationAndPatches), "genTrees")]
        public static bool genTreesPrefix(ref GenVegetationAndPatches __instance, out List<TreeVariant> __state, int chunkX, int chunkZ)
        {
            var treeSupplier = Traverse.Create(__instance).Field("treeSupplier").GetValue() as WgenTreeSupplier;
            var treeGenProps = Traverse.Create(treeSupplier).Field("treeGenProps").GetValue() as TreeGenProperties;

            BlockPos pos = new BlockPos(chunkX * sapi.WorldManager.ChunkSize, chunkX * sapi.WorldManager.ChunkSize, chunkZ * sapi.WorldManager.ChunkSize);
            IWorldGenBlockAccessor blockAccessor = Traverse.Create(__instance).Field("blockAccessor").GetValue() as IWorldGenBlockAccessor;
            IMapRegion mapRegion = blockAccessor.GetMapRegion(pos.X / blockAccessor.RegionSize, pos.Z / blockAccessor.RegionSize);

            var regionRealms = new List<string>();
            getModProperty(mapRegion, realmProperty, ref regionRealms);

            __state = treeGenProps.TreeGens.ToList();

            var treeVariants = new List<TreeVariant>();
            foreach (var gen in treeGenProps.TreeGens)
            {
                var name = gen.Generator.GetName();
                if (!config.TreeBiomes.ContainsKey(name))
                    continue;

                if (config.TreeBiomes[name].Intersect(regionRealms).Any())
                    treeVariants.Add(gen);
            }

            treeGenProps.TreeGens = treeVariants.ToArray();

            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GenVegetationAndPatches), "genTrees")]
        public static void genTreesPostfix(ref GenVegetationAndPatches __instance, List<TreeVariant> __state, int chunkX, int chunkZ)
        {
            var treeSupplier = Traverse.Create(__instance).Field("treeSupplier").GetValue() as WgenTreeSupplier;
            var treeGenProps = Traverse.Create(treeSupplier).Field("treeGenProps").GetValue() as TreeGenProperties;
            treeGenProps.TreeGens = __state.ToArray();
        }

        public TextCommandResult onTreesCommand(TextCommandCallingArgs args)
        {
            var regionRealms = new List<string>();
            getModProperty(args.Caller, realmProperty, ref regionRealms);
            var treeList = config.TreeBiomes.Where(x => x.Value.Intersect(regionRealms).Any()).Select(x => x.Key).Join(delimiter: "\r\n");

            var serverPlayer = args.Caller.Player as IServerPlayer;
            if (serverPlayer != null)
                serverPlayer.SendMessage(GlobalConstants.CurrentChatGroup, treeList, EnumChatType.Notification);

            return new TextCommandResult { Status = EnumCommandStatus.Success };
        }

        private TextCommandResult onDebugCommand(TextCommandCallingArgs args)
        {
            int worldWidthInRegions = sapi.WorldManager.MapSizeX / sapi.WorldManager.RegionSize;
            for (int regionX = 0; regionX < worldWidthInRegions; regionX++)
            {
                CalculateValues(null, regionX, 0, out EnumHemisphere hemisphere, out int currentRealm);
                string realmName = NorthOrSouth(hemisphere, currentRealm);
                sapi.Logger.Notification($"{regionX} => {realmName}");
            }
            return new TextCommandResult { Status = EnumCommandStatus.Success };
        }

        public static TextCommandResult onGetBiomeCommand(TextCommandCallingArgs args)
        {
            var regionHemisphere = EnumHemisphere.North;
            getModProperty(args.Caller, hemisphereProperty, ref regionHemisphere);
            var hemisphereStr = Enum.GetName(typeof(EnumHemisphere), regionHemisphere);

            var regionRealms = new List<string>();
            getModProperty(args.Caller, realmProperty, ref regionRealms);
            var realmsStr = regionRealms?.Join(delimiter: ",");

            var serverPlayer = args.Caller.Player as IServerPlayer;
            if (serverPlayer != null)
                serverPlayer.SendMessage(GlobalConstants.CurrentChatGroup, $"Hemisphere: {hemisphereStr} Realms: {realmsStr}", EnumChatType.Notification);

            return new TextCommandResult { Status = EnumCommandStatus.Success };
        }

        public static TextCommandResult onSetHemisphereCommand(TextCommandCallingArgs args)
        {
            if (Enum.TryParse(args.Parsers[0].GetValue() as string, out EnumHemisphere hemisphere))
                return new TextCommandResult { Status = setModPropertyForCallerRegion(args.Caller, hemisphereProperty, hemisphere) };

            return new TextCommandResult { Status = EnumCommandStatus.Error };
        }

        public static TextCommandResult onAddRealmCommand(TextCommandCallingArgs args)
        {
            var currentRealms = new List<string>();
            EnumCommandStatus result = getModProperty(args.Caller, realmProperty, ref currentRealms);
            if (result == EnumCommandStatus.Success)
            {
                var value = (args.Parsers[0].GetValue() as string).Replace('_', ' ');
                currentRealms.Add(value);
                result = setModPropertyForCallerRegion(args.Caller, realmProperty, currentRealms.Distinct());
            }

            return new TextCommandResult { Status = result };
        }

        private static TextCommandResult onRemoveRealmCommand(TextCommandCallingArgs args)
        {
            var currentRealms = new List<string>();
            EnumCommandStatus result = getModProperty(args.Caller, realmProperty, ref currentRealms);
            if (result == EnumCommandStatus.Success)
            {
                var value = (args.Parsers[0].GetValue() as string).Replace('_', ' ');
                currentRealms.Remove(value);
                result = setModPropertyForCallerRegion(args.Caller, realmProperty, currentRealms.Distinct());
            }

            return new TextCommandResult { Status = result };
        }

        public static EnumCommandStatus setModPropertyForCallerRegion(Caller caller, string name, object value)
        {
            var chunk = caller.Entity.World.BlockAccessor.GetMapChunkAtBlockPos(caller.Entity.Pos.AsBlockPos);
            return setModProperty(chunk.MapRegion, name, ref value);
        }

        public static EnumCommandStatus setModProperty<T>(IMapRegion region, string name, ref T value)
        {
            if (region == null)
                return EnumCommandStatus.Error;

            region.SetModdata(name, value);
            region.DirtyForSaving = true;
            return EnumCommandStatus.Success;
        }

        public static EnumCommandStatus getModProperty<T>(Caller caller, string name, ref T value)
        {
            var chunk = caller.Entity.World.BlockAccessor.GetMapChunkAtBlockPos(caller.Entity.Pos.AsBlockPos);
            return getModProperty(chunk.MapRegion, name, ref value);
        }

        public static EnumCommandStatus getModProperty<T>(IMapRegion region, string name, ref T value)
        {
            if (region == null)
                return EnumCommandStatus.Error;

            value = region.GetModdata<T>(name);
            return EnumCommandStatus.Success;
        }
    }
}
