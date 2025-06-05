using HarmonyLib;
using ProperVersion;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.Server;
using Vintagestory.ServerMods;
using Vintagestory.ServerMods.NoObf;

namespace Biomes
{
    [HarmonyPatch]
    public static class HarmonyPatches
    {
        public static Harmony harmony;
        public static BiomesModSystem biomesMod;

        public static void Init(BiomesModSystem mod)
        {
            biomesMod = mod;

            SemVer vsVersion;

            harmony = new Harmony(biomesMod.Mod.Info.ModID);

            harmony.Patch(typeof(BlockFruitTreeBranch).GetMethod("TryPlaceBlockForWorldGen"), typeof(HarmonyPatches).GetMethod("TryPlaceBlockForWorldGenPrefix"));
        }

        public static void Shutdown()
        {
            harmony.UnpatchAll(biomesMod.Mod.Info.ModID);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(BlockFruitTreeBranch), "TryPlaceBlockForWorldGen")]
        public static bool TryPlaceBlockForWorldGenPrefix(ref BlockFruitTreeBranch __instance, out FruitTreeWorldGenConds[] __state, IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, LCGRandom worldgenRandom)
        {
            __state = __instance.WorldGenConds;

            biomesMod.originalFruitTrees ??= __state;

            var filtered = new List<FruitTreeWorldGenConds>();
            foreach (var fruitTreeWorldGenCond in __state)
                if (biomesMod.AllowFruitTreeSpawn(blockAccessor.GetMapChunkAtBlockPos(pos), fruitTreeWorldGenCond, biomesMod.BiomeConfig.FruitTreeBiomes, pos))
                    filtered.Add(fruitTreeWorldGenCond);

            __instance.WorldGenConds = filtered.ToArray();
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(BlockFruitTreeBranch), "TryPlaceBlockForWorldGen")]
        public static void TryPlaceBlockForWorldGenPostfix(ref BlockFruitTreeBranch __instance, FruitTreeWorldGenConds[] __state, IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, IRandom worldgenRandom, BlockPatchAttributes attributes)
        {
            __instance.WorldGenConds = __state;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ForestFloorSystem), "GenPatches")]
        public static bool genPatchesTreePrefix(ref ForestFloorSystem __instance, out (List<BlockPatch>, List<BlockPatch>) __state, IBlockAccessor blockAccessor, BlockPos pos, float forestNess, EnumTreeType treetype, IRandom rnd)
        {
            var underTreeField = Traverse.Create(__instance).Field("underTreePatches");
            var underTreeValue = underTreeField.GetValue() as List<BlockPatch>;

            var onTreeField = Traverse.Create(__instance).Field("onTreePatches");
            var onTreeValue = onTreeField.GetValue() as List<BlockPatch>;

            __state = (underTreeValue, onTreeValue);

            var filtered = new List<BlockPatch>();
            foreach (var blockPatch in underTreeValue)
                if (biomesMod.AllowBlockPatchSpawn(blockAccessor.GetMapChunkAtBlockPos(pos), blockPatch, biomesMod.BiomeConfig.BlockPatchBiomes, pos))
                    filtered.Add(blockPatch);
            underTreeField.SetValue(filtered);

            var filtered2 = new List<BlockPatch>();
            foreach (var blockPatch in onTreeValue)
                if (biomesMod.AllowBlockPatchSpawn(blockAccessor.GetMapChunkAtBlockPos(pos), blockPatch, biomesMod.BiomeConfig.BlockPatchBiomes, pos))
                    filtered2.Add(blockPatch);
            onTreeField.SetValue(filtered2);

            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ForestFloorSystem), "GenPatches")]
        public static void genPatchesTreePostfix(ref ForestFloorSystem __instance, (List<BlockPatch>, List<BlockPatch>) __state, IBlockAccessor blockAccessor, BlockPos pos, float forestNess, EnumTreeType treetype, IRandom rnd)
        {
            Traverse.Create(__instance).Field("underTreePatches").SetValue(__state.Item1);
            Traverse.Create(__instance).Field("onTreePatches").SetValue(__state.Item2);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GenVegetationAndPatches), "genPatches")]
        public static bool genPatchesPrefix(ref GenVegetationAndPatches __instance, out BlockPatch[] __state, int chunkX, int chunkZ, bool postPass)
        {
            var blockAccessor = Traverse.Create(__instance).Field("blockAccessor").GetValue() as IWorldGenBlockAccessor;
            var bpc = Traverse.Create(__instance).Field("bpc").GetValue() as BlockPatchConfig;
            __state = bpc.PatchesNonTree;

            var filtered = new List<BlockPatch>();
            foreach (var blockPatch in __state)
                if (biomesMod.AllowBlockPatchSpawn(blockAccessor.GetMapChunk(chunkX, chunkZ), blockPatch, biomesMod.BiomeConfig.BlockPatchBiomes))
                    filtered.Add(blockPatch);

            bpc.PatchesNonTree = filtered.ToArray();
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GenVegetationAndPatches), "genPatches")]
        public static void genPatchesPostfix(ref GenVegetationAndPatches __instance, BlockPatch[] __state, int chunkX, int chunkZ, bool postPass)
        {
            var bpc = Traverse.Create(__instance).Field("bpc").GetValue() as BlockPatchConfig;
            bpc.PatchesNonTree = __state;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GenVegetationAndPatches), "genShrubs")]
        public static bool genShrubsPrefix(ref GenVegetationAndPatches __instance, out List<TreeVariant> __state, int chunkX, int chunkZ)
        {
            var blockAccessor = Traverse.Create(__instance).Field("blockAccessor").GetValue() as IWorldGenBlockAccessor;
            var treeSupplier = Traverse.Create(__instance).Field("treeSupplier").GetValue() as WgenTreeSupplier;
            var treeGenProps = Traverse.Create(treeSupplier).Field("treeGenProps").GetValue() as TreeGenProperties;
            __state = treeGenProps.ShrubGens.ToList();

            var filtered = new List<TreeVariant>();
            foreach (var generator in treeGenProps.ShrubGens)
                if (biomesMod.AllowTreeShrubSpawn(blockAccessor.GetMapChunk(chunkX, chunkZ), generator, biomesMod.BiomeConfig.TreeBiomes))
                    filtered.Add(generator);

            treeGenProps.ShrubGens = filtered.ToArray();
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

            var filtered = new List<TreeVariant>();
            foreach (var generator in treeGenProps.TreeGens)
                if (biomesMod.AllowTreeShrubSpawn(blockAccessor.GetMapChunk(chunkX, chunkZ), generator, biomesMod.BiomeConfig.TreeBiomes))
                    filtered.Add(generator);

            treeGenProps.TreeGens = filtered.ToArray();
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
        public static bool CanSpawnAtPosition(ref bool __result, IBlockAccessor blockAccessor, EntityProperties type, BlockPos pos, BaseSpawnConditions sc)
        {
            return __result = biomesMod.AllowEntitySpawn(blockAccessor.GetMapChunkAtBlockPos(pos), type, pos);
        }

        // Run-time spawn
        [HarmonyPrefix]
        [HarmonyPatch(typeof(ServerSystemEntitySpawner), "CanSpawnAt_offthread")]
        public static bool CanSpawnAt_offthread(ref Vec3d __result, EntityProperties type, Vec3i spawnPosition, RuntimeSpawnConditions sc, IWorldChunk[] chunkCol)
        {
            return chunkCol.Any() && biomesMod.AllowEntitySpawn(chunkCol[0].MapChunk, type, spawnPosition.AsBlockPos);
        }
    }
}
