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
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Server;
using Vintagestory.ServerMods;
using Vintagestory.ServerMods.NoObf;

namespace Biomes
{
    public class BiomeUserConfig
    {
        public bool FlipNorthSouth = false;
    }

    public class BiomeConfig
    {
        public List<string> NorthernRealms = new List<string>();
        public List<string> SouthernRealms = new List<string>();
        public List<string> SpawnWhiteList = new List<string>();
        public Dictionary<string, List<string>> TreeBiomes = new Dictionary<string, List<string>>();
        public Dictionary<string, List<string>> ForestBlockPatchBiomes = new Dictionary<string, List<string>>();
    }

    [ProtoContract]
    public class BiomeNameAndCoords
    {
        [ProtoMember(1)]
        public int chunkX;

        [ProtoMember(2)]
        public int chunkZ;

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

        public BiomeUserConfig userConfig;
        public static BiomeConfig modConfig;
        private static List<Regex> SpawnWhiteListRx = new List<Regex>
        {
        };

        //NormalizedSimplexNoise noise = new NormalizedSimplexNoise();

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

            modConfig = JsonConvert.DeserializeObject<BiomeConfig>(sapi.Assets.Get($"{Mod.Info.ModID}:config/{Mod.Info.ModID}.json").ToText());

            foreach (var item in modConfig.SpawnWhiteList)
                SpawnWhiteListRx.Add(new Regex(item));

            userConfig = sapi.LoadModConfig<BiomeUserConfig>($"{Mod.Info.ModID}.json");
            if (userConfig == null)
            {
                userConfig = new BiomeUserConfig();
                userConfig.FlipNorthSouth = true;
            }
            sapi.StoreModConfig(userConfig, $"{Mod.Info.ModID}.json");

            if (userConfig.FlipNorthSouth)
            {
                var tmp = modConfig.NorthernRealms;
                modConfig.NorthernRealms = modConfig.SouthernRealms;
                modConfig.SouthernRealms = tmp;
            }

            sapi.Event.MapChunkGeneration(OnMapChunkGeneration, "standard");

            sapi.ChatCommands.Create("biome")
                .WithDescription("Biome main command")
                .RequiresPrivilege(Privilege.gamemode)
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
                    .WithArgs(sapi.ChatCommands.Parsers.WordRange("realm", modConfig.NorthernRealms.Union(modConfig.SouthernRealms).Select(i => i.Replace(' ', '_')).ToArray()))
                    .HandleWith(onAddRealmCommand)
                .EndSubCommand()
                .BeginSubCommand("remove")
                    .WithArgs(sapi.ChatCommands.Parsers.WordRange("realm", modConfig.NorthernRealms.Union(modConfig.SouthernRealms).Select(i => i.Replace(' ', '_')).ToArray()))
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
            // modconfig.flipworld exchanges the lists, so we always do choose the same here no matter what
            return hemisphere == EnumHemisphere.North ? modConfig.NorthernRealms[realm] : modConfig.SouthernRealms[realm];
        }

        private void OnMapChunkGeneration(IMapChunk mapChunk, int chunkX, int chunkZ)
        {
            EnumHemisphere hemisphere;
            int currentRealm;

            var realmNames = new List<string>();
            CalculateValues(mapChunk, chunkX, chunkZ, out hemisphere, out currentRealm);
            realmNames.Add(NorthOrSouth(hemisphere, currentRealm));
            CalculateValues(null, chunkX - 1, chunkZ, out hemisphere, out currentRealm);
            realmNames.Add(NorthOrSouth(hemisphere, currentRealm));
            CalculateValues(null, chunkX + 1, chunkZ, out hemisphere, out currentRealm);
            realmNames.Add(NorthOrSouth(hemisphere, currentRealm));
            CalculateValues(null, chunkX, chunkZ - 1, out hemisphere, out currentRealm);
            realmNames.Add(NorthOrSouth(hemisphere, currentRealm));
            CalculateValues(null, chunkX, chunkZ + 1, out hemisphere, out currentRealm);
            realmNames.Add(NorthOrSouth(hemisphere, currentRealm));
            realmNames = realmNames.Distinct().ToList();

            setModProperty(mapChunk, realmProperty, ref realmNames);
        }

        private static void CalculateValues(IMapChunk mapChunk, int chunkX, int chunkZ, out EnumHemisphere hemisphere, out int currentRealm)
        {
            BlockPos blockPos = new BlockPos(chunkX * sapi.WorldManager.ChunkSize, 0, chunkZ * sapi.WorldManager.ChunkSize);
            hemisphere = sapi.World.Calendar.GetHemisphere(blockPos);
            setModProperty(mapChunk, hemisphereProperty, ref hemisphere);

            int realmCount;
            if (hemisphere == EnumHemisphere.North)
                realmCount = modConfig.NorthernRealms.Count;
            else
                realmCount = modConfig.SouthernRealms.Count;

            int worldWidthInChunks = sapi.WorldManager.MapSizeX / sapi.WorldManager.ChunkSize;
            float realmWidthInChunks = worldWidthInChunks / (float)realmCount;
            currentRealm = 0;
            if (realmWidthInChunks != 0)
                currentRealm = (int)(chunkX / realmWidthInChunks);
            if (currentRealm >= realmCount)
                currentRealm = realmCount - 1;
            if (currentRealm < 0)
                currentRealm = 0;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GenCreatures), "CanSpawnAtPosition")]
        public static bool CanSpawnAtPosition(GenCreatures __instance, ref bool __result, IBlockAccessor blockAccessor, EntityProperties type, BlockPos pos, BaseSpawnConditions sc)
        {
            __result = AllowEntitySpawn(blockAccessor.GetMapChunkAtBlockPos(pos), type);
            return __result;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ServerSystemEntitySpawner), "CanSpawnAt")]
        public static bool CanSpawnAt(ServerSystemEntitySpawner __instance, ref Vec3d __result, EntityProperties type, Vec3i spawnPosition, RuntimeSpawnConditions sc, IWorldChunk[] chunkCol)
        {
            IMapChunk mapChunk = chunkCol[0].MapChunk;
            return AllowEntitySpawn(mapChunk, type);
        }

        public static bool AllowEntitySpawn(IMapChunk mapChunk, EntityProperties type)
        {
            foreach (var rx in SpawnWhiteListRx)
                if (rx.IsMatch(type.Code.Path))
                    return true;

            // Only blessed animals get in.
            if (type.Attributes == null || !type.Attributes.KeyExists(realmProperty))
                return false;

            // Test map chunk attributes
            var chunkRealms = new List<string>();
            getModProperty(mapChunk, realmProperty, ref chunkRealms);
            var entityNativeRealms = type.Attributes[realmProperty].AsArray<string>();
            return entityNativeRealms.Intersect(chunkRealms).Any();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ForestFloorSystem), "GenPatches")]
        public static bool genPatchesPrefix(ref ForestFloorSystem __instance, out List<BlockPatch> __state, IBlockAccessor blockAccessor, BlockPos pos, float forestNess, EnumTreeType treetype, LCGRandom rnd)
        {
            var underTreePatches = Traverse.Create(__instance).Field("underTreePatches").GetValue() as List<BlockPatch>;
            __state = underTreePatches;

            var mapChunk = blockAccessor.GetMapChunkAtBlockPos(pos);
            var chunkRealms = new List<string>();
            getModProperty(mapChunk, realmProperty, ref chunkRealms);

            var undertreeBlockPatches = new List<BlockPatch>();
            foreach (var gen in underTreePatches)
            {
                var names = gen.blockCodes.Select(x => x.Path).ToList();
                var intersect = modConfig.ForestBlockPatchBiomes.Keys.Intersect(names).ToList();
                if (!intersect.Any())
                    continue;

                if (modConfig.TreeBiomes[intersect.First()].Intersect(chunkRealms).Any())
                    undertreeBlockPatches.Add(gen);
            }

            underTreePatches = undertreeBlockPatches;

            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ForestFloorSystem), "GenPatches")]
        public static void genPatchesPostfix(ref ForestFloorSystem __instance, List<BlockPatch> __state, IBlockAccessor blockAccessor, BlockPos pos, float forestNess, EnumTreeType treetype, LCGRandom rnd)
        {
            var underTreePatches = Traverse.Create(__instance).Field("underTreePatches").GetValue() as List<BlockPatch>;
            underTreePatches = __state;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GenVegetationAndPatches), "genShrubs")]
        public static bool genShrubsPrefix(ref GenVegetationAndPatches __instance, out List<TreeVariant> __state, int chunkX, int chunkZ)
        {
            var treeSupplier = Traverse.Create(__instance).Field("treeSupplier").GetValue() as WgenTreeSupplier;
            var treeGenProps = Traverse.Create(treeSupplier).Field("treeGenProps").GetValue() as TreeGenProperties;
            __state = treeGenProps.ShrubGens.ToList();

            IWorldGenBlockAccessor blockAccessor = Traverse.Create(__instance).Field("blockAccessor").GetValue() as IWorldGenBlockAccessor;
            var mapChunk = blockAccessor.GetMapChunk(chunkX, chunkZ);

            var chunkRealms = new List<string>();
            getModProperty(mapChunk, realmProperty, ref chunkRealms);

            var treeVariants = new List<TreeVariant>();
            foreach (var gen in treeGenProps.ShrubGens)
            {
                var name = gen.Generator.GetName();
                if (!modConfig.TreeBiomes.ContainsKey(name))
                    continue;

                if (modConfig.TreeBiomes[name].Intersect(chunkRealms).Any())
                    treeVariants.Add(gen);
            }

            treeGenProps.ShrubGens = treeVariants.ToArray();

            return true;
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(GenVegetationAndPatches), "genShrubs")]
        public static void genShrubsPostfix(ref GenVegetationAndPatches __instance, List<TreeVariant> __state, int chunkX, int chunkZ)
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
            __state = treeGenProps.TreeGens.ToList();

            BlockPos pos = new BlockPos(chunkX * sapi.WorldManager.ChunkSize, chunkX * sapi.WorldManager.ChunkSize, chunkZ * sapi.WorldManager.ChunkSize);
            IWorldGenBlockAccessor blockAccessor = Traverse.Create(__instance).Field("blockAccessor").GetValue() as IWorldGenBlockAccessor;
            var mapChunk = blockAccessor.GetMapChunk(chunkX, chunkZ);

            var chunkRealms = new List<string>();
            getModProperty(mapChunk, realmProperty, ref chunkRealms);

            var treeVariants = new List<TreeVariant>();
            foreach (var gen in treeGenProps.TreeGens)
            {
                var name = gen.Generator.GetName();
                if (!modConfig.TreeBiomes.ContainsKey(name))
                    continue;

                if (modConfig.TreeBiomes[name].Intersect(chunkRealms).Any())
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
            var chunkRealms = new List<string>();
            getModProperty(args.Caller, realmProperty, ref chunkRealms);
            var treeList = modConfig.TreeBiomes.Where(x => x.Value.Intersect(chunkRealms).Any()).Select(x => x.Key).Join(delimiter: "\r\n");

            var serverPlayer = args.Caller.Player as IServerPlayer;
            if (serverPlayer != null)
                serverPlayer.SendMessage(GlobalConstants.CurrentChatGroup, treeList, EnumChatType.Notification);

            return new TextCommandResult { Status = EnumCommandStatus.Success };
        }

        private TextCommandResult onDebugCommand(TextCommandCallingArgs args)
        {
            int worldWidthInChunks = sapi.WorldManager.MapSizeX / sapi.WorldManager.ChunkSize;
            for (int chunkX = 0; chunkX < worldWidthInChunks; chunkX++)
            {
                CalculateValues(null, chunkX, 0, out EnumHemisphere hemisphere, out int currentRealm);
                string realmName = NorthOrSouth(hemisphere, currentRealm);
                sapi.Logger.Notification($"{chunkX} => {realmName}");
            }
            return new TextCommandResult { Status = EnumCommandStatus.Success };
        }

        public static TextCommandResult onGetBiomeCommand(TextCommandCallingArgs args)
        {
            var chunkHemisphere = EnumHemisphere.North;
            getModProperty(args.Caller, hemisphereProperty, ref chunkHemisphere);
            var hemisphereStr = Enum.GetName(typeof(EnumHemisphere), chunkHemisphere);

            var chunkRealms = new List<string>();
            getModProperty(args.Caller, realmProperty, ref chunkRealms);
            var realmsStr = chunkRealms?.Join(delimiter: ",");

            var serverPlayer = args.Caller.Player as IServerPlayer;
            if (serverPlayer != null)
                serverPlayer.SendMessage(GlobalConstants.CurrentChatGroup, $"Hemisphere: {hemisphereStr} Realms: {realmsStr}", EnumChatType.Notification);

            return new TextCommandResult { Status = EnumCommandStatus.Success };
        }

        public static TextCommandResult onSetHemisphereCommand(TextCommandCallingArgs args)
        {
            if (Enum.TryParse(args.Parsers[0].GetValue() as string, out EnumHemisphere hemisphere))
                return new TextCommandResult { Status = setModPropertyForCallerChunk(args.Caller, hemisphereProperty, hemisphere) };

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
                result = setModPropertyForCallerChunk(args.Caller, realmProperty, currentRealms.Distinct());
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
                result = setModPropertyForCallerChunk(args.Caller, realmProperty, currentRealms.Distinct());
            }

            return new TextCommandResult { Status = result };
        }

        public static EnumCommandStatus setModPropertyForCallerChunk(Caller caller, string name, object value)
        {
            var chunk = caller.Entity.World.BlockAccessor.GetMapChunkAtBlockPos(caller.Entity.Pos.AsBlockPos);
            return setModProperty(chunk, name, ref value);
        }

        public static EnumCommandStatus setModProperty<T>(IMapChunk chunk, string name, ref T value)
        {
            if (chunk == null)
                return EnumCommandStatus.Error;

            chunk.SetModdata(name, value);
            chunk.MarkDirty();
            return EnumCommandStatus.Success;
        }

        public static EnumCommandStatus getModProperty<T>(Caller caller, string name, ref T value)
        {
            var chunk = caller.Entity.World.BlockAccessor.GetMapChunkAtBlockPos(caller.Entity.Pos.AsBlockPos);
            return getModProperty(chunk, name, ref value);
        }

        public static EnumCommandStatus getModProperty<T>(IMapChunk chunk, string name, ref T value)
        {
            if (chunk == null)
                return EnumCommandStatus.Error;

            value = chunk.GetModdata<T>(name);
            return EnumCommandStatus.Success;
        }
    }
}
