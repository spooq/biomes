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

        public TextCommandResult onTreesCommand(TextCommandCallingArgs args)
        {
            if (args.Caller != null)
            {
                var serverPlayer = args.Caller.Player as IServerPlayer;
                if (serverPlayer != null)
                {
                    var coords = serverPlayer.Entity.Pos.AsBlockPos;
                    var regionRealms = new List<string>();
                    getModProperty(coords, realmProperty, ref regionRealms);

                    var treeList = config.TreeBiomes.Where(x => x.Value.Intersect(regionRealms).Any()).Select(x => x.Key).Join(delimiter: "\r\n");
                    serverPlayer.SendMessage(GlobalConstants.CurrentChatGroup, treeList, EnumChatType.Notification);
                    return new TextCommandResult { Status = EnumCommandStatus.Success };
                }
            }

            return new TextCommandResult { Status = EnumCommandStatus.Error };
        }

        public override void Dispose()
        {
            harmony.UnpatchAll(Mod.Info.ModID);
            base.Dispose();
        }

        public void OnMapRegionGeneration(IMapRegion mapRegion, int regionX, int regionZ, ITreeAttribute chunkGenParams)
        {
            BlockPos blockPos = new BlockPos(regionX * sapi.WorldManager.RegionSize, 0, regionZ * sapi.WorldManager.RegionSize);
            var hemisphere = sapi.World.Calendar.GetHemisphere(blockPos);
            setModProperty(mapRegion, hemisphereProperty, ref hemisphere);

            int realmCount;
            if (hemisphere == EnumHemisphere.North)
                realmCount = config.NorthernRealms.Count;
            else
                realmCount = config.SouthernRealms.Count;

            int widthInRegions = sapi.WorldManager.MapSizeX / sapi.WorldManager.RegionSize;
            float realmWidthInRegions = widthInRegions / (float) realmCount;
            int currentRealm = 0;
            if (realmWidthInRegions != 0)
                currentRealm = (int)(regionX / realmWidthInRegions);
            if (currentRealm >= realmCount)
                currentRealm = realmCount - 1;

            // TODO: pick up next door names and add to list
            string localRealmName = "";
            if (hemisphere == EnumHemisphere.North)
                localRealmName = config.NorthernRealms[currentRealm];
            else
                localRealmName = config.SouthernRealms[currentRealm];

            var realmNames = new List<string> { localRealmName };

            setModProperty(mapRegion, realmProperty, ref realmNames);
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

        public static TextCommandResult onGetBiomeCommand(TextCommandCallingArgs args)
        {
            if (args.Caller != null)
            {
                var serverPlayer = args.Caller.Player as IServerPlayer;
                if (serverPlayer != null)
                {
                    var coords = serverPlayer.Entity.Pos.AsBlockPos;
                    var regionHemisphere = EnumHemisphere.North;
                    getModProperty(coords, hemisphereProperty, ref regionHemisphere);
                    var hemisphereStr = Enum.GetName(typeof(EnumHemisphere), regionHemisphere);

                    var regionRealms = new List<string>();
                    getModProperty(coords, realmProperty, ref regionRealms);
                    var realmsStr = regionRealms?.Join(delimiter: ",");
                    
                    serverPlayer.SendMessage(GlobalConstants.CurrentChatGroup, $"Hemisphere: {hemisphereStr} Realms: {realmsStr}", EnumChatType.Notification);
                    return new TextCommandResult { Status = EnumCommandStatus.Success };
                }
            }

            return new TextCommandResult { Status = EnumCommandStatus.Error };
        }

        public static TextCommandResult onSetHemisphereCommand(TextCommandCallingArgs args)
        {
            if (args.Caller != null && Enum.TryParse(args.Parsers[0].GetValue() as string, out EnumHemisphere hemisphere))
                return new TextCommandResult { Status = setModPropertyForCallerRegion(args.Caller, hemisphereProperty, hemisphere) };

            return new TextCommandResult { Status = EnumCommandStatus.Error };
        }

        public static TextCommandResult onAddRealmCommand(TextCommandCallingArgs args)
        {
            if (args.Caller == null)
                return new TextCommandResult { Status = EnumCommandStatus.Error };

            var currentRealms = new List<string>();
            EnumCommandStatus result = getModPropertyForCallerRegion(args.Caller, realmProperty, ref currentRealms);
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
            if (args.Caller == null)
                return new TextCommandResult { Status = EnumCommandStatus.Error };

            var currentRealms = new List<string>();
            EnumCommandStatus result = getModPropertyForCallerRegion(args.Caller, realmProperty, ref currentRealms);
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
            if (caller != null)
            {
                var serverPlayer = caller.Player as IServerPlayer;
                if (serverPlayer != null)
                {
                    var coords = serverPlayer.Entity.Pos.AsBlockPos;
                    return setModProperty(coords, name, value);
                }
            }
            return EnumCommandStatus.Error;
        }

        public static EnumCommandStatus setModProperty(BlockPos pos, string name, object value)
        {
            return setModProperty(sapi.World.BlockAccessor.GetMapChunkAtBlockPos(pos)?.MapRegion, name, ref value);
        }

        public static EnumCommandStatus setModProperty<T>(IMapRegion region, string name, ref T value)
        {
            if (region == null)
                return EnumCommandStatus.Error;

            region.SetModdata(name, value);
            region.DirtyForSaving = true;
            return EnumCommandStatus.Success;
        }

        public static EnumCommandStatus getModPropertyForCallerRegion<T>(Caller caller, string name, ref T value)
        {
            if (caller != null)
            {
                var serverPlayer = caller.Player as IServerPlayer;
                if (serverPlayer != null)
                {
                    var coords = serverPlayer.Entity.Pos.AsBlockPos;
                    return getModProperty(coords, name, ref value);
                }
            }

            return EnumCommandStatus.Error;
        }

        public static EnumCommandStatus getModProperty<T>(BlockPos pos, string name, ref T value)
        {
            return getModProperty(sapi?.World.BlockAccessor.GetMapChunkAtBlockPos(pos)?.MapRegion, name, ref value);
        }

        public static EnumCommandStatus getModProperty<T>(IMapRegion region, string name, ref T value)
        {
            if (region == null)
                return EnumCommandStatus.Error;

            value = region.GetModdata<T>(name);
            return EnumCommandStatus.Success;
        }

        /*
        public Vec3i MapRegionPosFromIndex2D(long index)
        {
            return new Vec3i(
                (int)(index % sapi.World.BlockAccessor.RegionMapSizeX),
                0,
                (int)(index / sapi.World.BlockAccessor.RegionMapSizeX)
            );
        }
        */
    }
}
