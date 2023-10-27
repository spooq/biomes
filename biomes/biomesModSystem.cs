using Cairo;
using HarmonyLib;
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
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace biomes
{
    public class BiomeConfig
    {
        public List<string> NorthernRealms = new List<string>();
        public List<string> SouthernRealms = new List<string>();
        public List<string> SpawnWhiteList = new List<string>();
        public Dictionary<string, List<string>> TreeBiomes = new Dictionary<string, List<string>>();
    }

    [ProtoContract]
    public class SinglePlaceNameByMapRegionCoords
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

        public static BiomeConfig config;

        private const string realmProperty = "biorealm";
        private const string hemisphereProperty = "hemisphere";

        // Read from config
        private static List<Regex> SpawnWhiteList = new List<Regex>
        {
        };

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }

        public override void Dispose()
        {
            harmony.UnpatchAll(Mod.Info.ModID);
            base.Dispose();
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

            config = sapi.LoadModConfig<BiomeConfig>(Mod.Info.ModID + ".json");
            if (config == null)
            {
                config = new BiomeConfig
                {
                    NorthernRealms = new List<string>
                    {
                        "nearctic",
                        "western paleartic",
                        "central paleartic",
                        "eastern paleartic"
                    },
                    SouthernRealms = new List<string>
                    {
                        "neotropical",
                        "afrotropical",
                        "indomalayan",
                        "austrolasian"
                    },
                    SpawnWhiteList = new List<string>
                    {
                        "playerbot-*",
                        "animalbot-*",
                        "humanoid-*",
                        "bell",
                        "drifter",
                        "eidolon-*",
                        "locust-*",
                        "mechhelper",
                        "strawdummy",
                        "echochamber",
                        "libraryresonator",
                        "boat"
                    },
                    TreeBiomes = new Dictionary<string, List<string>>
                    {
                        { "scotspine", new List<string> { "nearctic", "western paleartic", "central paleartic", "eastern paleartic" } },
                        { "englishoak", new List<string> { "western paleartic", "central paleartic", "eastern paleartic" } }
                    }
                };
                sapi.StoreModConfig(config, Mod.Info.ModID + ".json");
            }

            foreach (var item in config.SpawnWhiteList)
                SpawnWhiteList.Add(new Regex(item));

            sapi.Event.MapRegionGeneration(OnMapRegionGeneration, "standard");

            sapi.ChatCommands.Create("biome")
                .WithDescription("Biome main command")
                .RequiresPrivilege(Privilege.chat)
                .BeginSubCommand("show")
                    .HandleWith(onGetBiomeCommand)
                .EndSubCommand()
                .BeginSubCommand("hemisphere")
                    .WithArgs(sapi.ChatCommands.Parsers.WordRange("hemisphere", "north", "south"))
                    .HandleWith(onSetHemisphereCommand)
                .EndSubCommand()
                .BeginSubCommand("realm")
                    .WithArgs(sapi.ChatCommands.Parsers.WordRange("realm", config.NorthernRealms.Union(config.SouthernRealms).Select(i => i.Replace(' ', '_')).ToArray()))
                    .HandleWith(onSetBiomeCommand)
                .EndSubCommand();
        }

        public void OnMapRegionGeneration(IMapRegion mapRegion, int regionX, int regionZ, ITreeAttribute chunkGenParams)
        {
            BlockPos blockPos = new BlockPos(regionX * sapi.WorldManager.RegionSize, 0, regionZ * sapi.WorldManager.RegionSize);
            /*
            ClimateCondition baseClimate = sapi.World.BlockAccessor.GetClimateAt(blockPos, EnumGetClimateMode.WorldGenValues);
            if (baseClimate != null)
            {
                float baseTemperature = baseClimate.Temperature;
            }*/

            /*
            int heightInRegions = sapi.WorldManager.MapSizeZ / sapi.WorldManager.RegionSize;
            int hemiHeightInRegions = heightInRegions / 2;
            int hemi = 0;
            if (heightInRegions > 0)
                hemi = regionZ / hemiHeightInRegions;

            EnumHemisphere hemisphere = hemi == 0 ? EnumHemisphere.North : EnumHemisphere.South;
                        */

            EnumHemisphere hemisphere = sapi.World.Calendar.GetHemisphere(blockPos);
            if (hemisphere == EnumHemisphere.North)
                mapRegion.SetModdata(hemisphereProperty, "north");
            else
                mapRegion.SetModdata(hemisphereProperty, "south");

            int realmCount;
            if (hemisphere == EnumHemisphere.North)
                realmCount = config.NorthernRealms.Count;
            else
                realmCount = config.SouthernRealms.Count;

            int widthInRegions = sapi.WorldManager.MapSizeX / sapi.WorldManager.RegionSize;
            int realmWidthInRegions = widthInRegions / realmCount;
            int currentRealm = 0;
            if (realmWidthInRegions > 0)
                currentRealm = regionX / realmWidthInRegions;

            string realmName = "";
            if (hemisphere == EnumHemisphere.North)
                realmName = config.NorthernRealms[currentRealm];
            else
                realmName = config.SouthernRealms[currentRealm];
            mapRegion.SetModdata(realmProperty, realmName);
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
            foreach (var rx in SpawnWhiteList)
                if (rx.IsMatch(type.Code.Path))
                    return true;

            // Only blessed animals get in.
            if (type.Attributes == null
                || !type.Attributes.KeyExists(realmProperty)
                || !type.Attributes.KeyExists(hemisphereProperty))
            {
                return false;
            }

            // Test map region attributes
            string regionHemisphere = mapRegion.GetModdata<string>(hemisphereProperty);
            var animalHemisphere = type.Attributes[hemisphereProperty].AsArray<string>();
            if (!animalHemisphere.Contains(regionHemisphere))
                return false;

            string regionRealm = mapRegion.GetModdata<string>(realmProperty);
            var animalRealms = type.Attributes[realmProperty].AsArray<string>();
            if (!animalRealms.Contains(regionRealm))
                return false;

            return true;
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
            string regionHemisphere = mapRegion.GetModdata<string>(hemisphereProperty);
            string regionRealm = mapRegion.GetModdata<string>(realmProperty);

            __state = treeGenProps.ShrubGens.ToList();

            var treeVariants = new List<TreeVariant>();
            foreach (var gen in treeGenProps.ShrubGens)
            {
                var name = gen.Generator.GetName();
                if (!config.TreeBiomes.ContainsKey(name))
                    continue;

                if (config.TreeBiomes[name].Contains(regionRealm))
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
            string regionHemisphere = mapRegion.GetModdata<string>(hemisphereProperty);
            string regionRealm = mapRegion.GetModdata<string>(realmProperty);

            __state = treeGenProps.TreeGens.ToList();

            var treeVariants = new List<TreeVariant>();
            foreach (var gen in treeGenProps.TreeGens)
            {
                var name = gen.Generator.GetName();
                if (!config.TreeBiomes.ContainsKey(name))
                    continue;

                if (config.TreeBiomes[name].Contains(regionRealm))
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

        private TextCommandResult onGetBiomeCommand(TextCommandCallingArgs args)
        {
            if (args.Caller != null)
            {
                var serverPlayer = args.Caller.Player as IServerPlayer;
                if (serverPlayer != null)
                {
                    var coords = serverPlayer.Entity.Pos.AsBlockPos;
                    var chunk = sapi.World.BlockAccessor.GetMapChunkAtBlockPos(coords);
                    if (chunk != null)
                    {
                        var region = chunk.MapRegion;
                        if (region != null)
                        {
                            serverPlayer.SendMessage(GlobalConstants.CurrentChatGroup, $"Hemi: {region.GetModdata<string>(hemisphereProperty)} Realm: {region.GetModdata<string>(realmProperty)}", EnumChatType.Notification);
                            return new TextCommandResult { Status = EnumCommandStatus.Success };
                        }
                    }
                }
            }

            return new TextCommandResult { Status = EnumCommandStatus.Error };
        }

        private TextCommandResult onSetHemisphereCommand(TextCommandCallingArgs args)
        {
            if (args.Caller == null)
                return new TextCommandResult { Status = EnumCommandStatus.Error };

            EnumCommandStatus result = setModProperty(args.Caller.Entity.Pos.AsBlockPos, hemisphereProperty, args.Parsers[0].GetValue() as string);
            onGetBiomeCommand(args);
            return new TextCommandResult { Status = result };
        }

        private TextCommandResult onSetBiomeCommand(TextCommandCallingArgs args)
        {
            if (args.Caller == null)
                return new TextCommandResult { Status = EnumCommandStatus.Error };

            EnumCommandStatus result = setModProperty(args.Caller.Entity.Pos.AsBlockPos, realmProperty, args.Parsers[0].GetValue() as string);
            onGetBiomeCommand(args);
            return new TextCommandResult { Status = result };
        }

        public EnumCommandStatus setModProperty(BlockPos pos, string name, string value)
        {
            var chunk = sapi.World.BlockAccessor.GetMapChunkAtBlockPos(pos);
            if (chunk == null) return EnumCommandStatus.Error;
            var region = chunk.MapRegion;
            if (region == null) return EnumCommandStatus.Error;
            region.SetModdata(name.Replace('_', ' '), value);
            region.DirtyForSaving = true;
            return EnumCommandStatus.Success;
        }

        public Vec3i MapRegionPosFromIndex2D(long index)
        {
            return new Vec3i(
                (int)(index % sapi.World.BlockAccessor.RegionMapSizeX),
                0,
                (int)(index / sapi.World.BlockAccessor.RegionMapSizeX)
            );
        }

    }
}
