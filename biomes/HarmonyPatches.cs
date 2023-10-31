using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Server;
using Vintagestory.ServerMods;
using Vintagestory.ServerMods.NoObf;

namespace Biomes
{
    [HarmonyPatch]
    public static class HarmonyPatches
    {
        public static Harmony harmony;
        public static BiomesModSystem BiomesMod;

        public static void Init(BiomesModSystem mod)
        {
            BiomesMod = mod;
            harmony = new Harmony(BiomesMod.Mod.Info.ModID);
            harmony.PatchAll();
        }

        public static void Shutdown()
        {
            harmony.UnpatchAll(BiomesMod.Mod.Info.ModID);
        }

        [HarmonyPrefix]
        [HarmonyPriority(400)]
        [HarmonyPatch(typeof(ForestFloorSystem), "GenPatches")]
        public static bool genPatchesUnderTreePrefix(ref ForestFloorSystem __instance, out List<BlockPatch> __state, IBlockAccessor blockAccessor, BlockPos pos, float forestNess, EnumTreeType treetype, LCGRandom rnd)
        {
            var blockPatchesField = Traverse.Create(__instance).Field("underTreePatches");
            var blockPatchesValue = blockPatchesField.GetValue() as List<BlockPatch>;
            __state = blockPatchesValue;

            var mapChunk = blockAccessor.GetMapChunkAtBlockPos(pos);
            var chunkRealms = new List<string>();
            BiomesMod.getModProperty(mapChunk, BiomesModSystem.RealmPropertyName, ref chunkRealms);

            var biomeBPs = new List<BlockPatch>();
            foreach (var gen in blockPatchesValue)
            {
                var allNamesForThisBP = gen.blockCodes.Select(x => x.Path);
                if (allNamesForThisBP.Contains("flower-lilyofthevalley-free"))
                    biomeBPs.Add(gen);

                    //var allBiomesForThisBP = BiomesMod.ModConfig.ForestBlockPatchBiomes.Where(x => allNamesForThisBP.Contains(x.Key)).SelectMany(x => x.Value).Distinct();
                    //if (allNamesForThisBP.Contains("mushroom-whiteoyster-normal-north"))
                    //if (allBiomesForThisBP.Intersect(chunkRealms).Any())
                    //  biomeBPs.Add(gen);
            }

            blockPatchesField.SetValue(biomeBPs);
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPriority(400)]
        [HarmonyPatch(typeof(ForestFloorSystem), "GenPatches")]
        public static void genPatchesUnderTreePostfix(ref ForestFloorSystem __instance, List<BlockPatch> __state, IBlockAccessor blockAccessor, BlockPos pos, float forestNess, EnumTreeType treetype, LCGRandom rnd)
        {
            Traverse.Create(__instance).Field("underTreePatches").SetValue(__state);
        }

        [HarmonyPrefix]
        [HarmonyPriority(300)]
        [HarmonyPatch(typeof(ForestFloorSystem), "GenPatches")]
        public static bool genPatchesOnTreePrefix(ref ForestFloorSystem __instance, out List<BlockPatch> __state, IBlockAccessor blockAccessor, BlockPos pos, float forestNess, EnumTreeType treetype, LCGRandom rnd)
        {
            var blockPatchesField = Traverse.Create(__instance).Field("onTreePatches");
            var blockPatchesValue = blockPatchesField.GetValue() as List<BlockPatch>;
            __state = blockPatchesValue;

            var mapChunk = blockAccessor.GetMapChunkAtBlockPos(pos);
            var chunkRealms = new List<string>();
            BiomesMod.getModProperty(mapChunk, BiomesModSystem.RealmPropertyName, ref chunkRealms);

            var biomeBPs = new List<BlockPatch>();
            foreach (var gen in blockPatchesValue)
            {
                var allNamesForThisBP = gen.blockCodes.Select(x => x.Path);
                if (allNamesForThisBP.Contains("flower-lilyofthevalley-free"))
                    biomeBPs.Add(gen);

                //var allBiomesForThisBP = BiomesMod.ModConfig.ForestBlockPatchBiomes.Where(x => allNamesForThisBP.Contains(x.Key)).SelectMany(x => x.Value).Distinct();
                //if (allNamesForThisBP.Contains("mushroom-whiteoyster-normal-north"))
                //if (allBiomesForThisBP.Intersect(chunkRealms).Any())
                //  biomeBPs.Add(gen);
            }

            blockPatchesField.SetValue(biomeBPs);
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPriority(300)]
        [HarmonyPatch(typeof(ForestFloorSystem), "GenPatches")]
        public static void genPatchesOnTreePostfix(ref ForestFloorSystem __instance, List<BlockPatch> __state, IBlockAccessor blockAccessor, BlockPos pos, float forestNess, EnumTreeType treetype, LCGRandom rnd)
        {
            Traverse.Create(__instance).Field("onTreePatches").SetValue(__state);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GenVegetationAndPatches), "genPatches")]
        public static bool genPatchesPrefix(ref GenVegetationAndPatches __instance, out List<BlockPatch> __state, int chunkX, int chunkZ, bool postPass)
        {
            var blockAccessor = Traverse.Create(__instance).Field("blockAccessor").GetValue() as IWorldGenBlockAccessor;
            var bpc = Traverse.Create(__instance).Field("bpc").GetValue() as BlockPatchConfig;
            __state = bpc.PatchesNonTree.ToList();

            var mapChunk = blockAccessor.GetMapChunk(chunkX, chunkZ);
            var chunkRealms = new List<string>();
            BiomesMod.getModProperty(mapChunk, BiomesModSystem.RealmPropertyName, ref chunkRealms);

            var blockPatches = new List<BlockPatch>();
            foreach (var gen in __state)
            {
                var allNamesForThisBP = gen.blockCodes.Select(x => x.Path);
                if (allNamesForThisBP.Contains("flower-lilyofthevalley-free"))
                    blockPatches.Add(gen);
                //if (BiomesMod.ModConfig.TreeBiomes.ContainsKey(name) && BiomesMod.ModConfig.TreeBiomes[name].Intersect(chunkRealms).Any())
                //  blockPatches.Add(gen);
            }

            bpc.PatchesNonTree = blockPatches.ToArray();
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GenVegetationAndPatches), "genPatches")]
        public static void genPatchesPostfix(ref GenVegetationAndPatches __instance, List<BlockPatch> __state, int chunkX, int chunkZ, bool postPass)
        {
            var bpc = Traverse.Create(__instance).Field("bpc").GetValue() as BlockPatchConfig;
            bpc.PatchesNonTree = __state.ToArray();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GenVegetationAndPatches), "genShrubs")]
        public static bool genShrubsPrefix(ref GenVegetationAndPatches __instance, out List<TreeVariant> __state, int chunkX, int chunkZ)
        {
            var blockAccessor = Traverse.Create(__instance).Field("blockAccessor").GetValue() as IWorldGenBlockAccessor;
            var treeSupplier = Traverse.Create(__instance).Field("treeSupplier").GetValue() as WgenTreeSupplier;
            var treeGenProps = Traverse.Create(treeSupplier).Field("treeGenProps").GetValue() as TreeGenProperties;
            __state = treeGenProps.ShrubGens.ToList();
            
            var mapChunk = blockAccessor.GetMapChunk(chunkX, chunkZ);
            var chunkRealms = new List<string>();
            BiomesMod.getModProperty(mapChunk, BiomesModSystem.RealmPropertyName, ref chunkRealms);

            var treeVariants = new List<TreeVariant>();
            foreach (var gen in treeGenProps.ShrubGens)
            {
                var name = gen.Generator.GetName();
                if (BiomesMod.ModConfig.TreeBiomes.ContainsKey(name) && BiomesMod.ModConfig.TreeBiomes[name].Intersect(chunkRealms).Any())
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
            var blockAccessor = Traverse.Create(__instance).Field("blockAccessor").GetValue() as IWorldGenBlockAccessor;
            var treeSupplier = Traverse.Create(__instance).Field("treeSupplier").GetValue() as WgenTreeSupplier;
            var treeGenProps = Traverse.Create(treeSupplier).Field("treeGenProps").GetValue() as TreeGenProperties;
            __state = treeGenProps.TreeGens.ToList();
            
            var mapChunk = blockAccessor.GetMapChunk(chunkX, chunkZ);
            var chunkRealms = new List<string>();
            BiomesMod.getModProperty(mapChunk, BiomesModSystem.RealmPropertyName, ref chunkRealms);

            var treeVariants = new List<TreeVariant>();
            foreach (var gen in treeGenProps.TreeGens)
            {
                var name = gen.Generator.GetName();
                if (BiomesMod.ModConfig.TreeBiomes.ContainsKey(name) && BiomesMod.ModConfig.TreeBiomes[name].Intersect(chunkRealms).Any())
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

        // World-gen spawn
        [HarmonyPrefix]
        [HarmonyPatch(typeof(GenCreatures), "CanSpawnAtPosition")]
        public static bool CanSpawnAtPosition(GenCreatures __instance, ref bool __result, IBlockAccessor blockAccessor, EntityProperties type, BlockPos pos, BaseSpawnConditions sc)
        {
            __result = BiomesMod.AllowEntitySpawn(blockAccessor.GetMapChunkAtBlockPos(pos), type);
            return __result;
        }

        // Run-time spawn
        [HarmonyPrefix]
        [HarmonyPatch(typeof(ServerSystemEntitySpawner), "CanSpawnAt")]
        public static bool CanSpawnAt(ServerSystemEntitySpawner __instance, ref Vec3d __result, EntityProperties type, Vec3i spawnPosition, RuntimeSpawnConditions sc, IWorldChunk[] chunkCol)
        {
            return chunkCol.Any() && BiomesMod.AllowEntitySpawn(chunkCol[0].MapChunk, type);
        }
    }
}
