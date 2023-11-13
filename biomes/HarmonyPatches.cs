using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
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
        [HarmonyPatch(typeof(BlockFruitTreeBranch), "TryPlaceBlockForWorldGen")]
        public static bool TryPlaceBlockForWorldGenPrefix(ref BlockFruitTreeBranch __instance, out FruitTreeWorldGenConds[] __state, IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, LCGRandom worldgenRandom)
        {
            __state = __instance.WorldGenConds;

            var mapChunk = blockAccessor.GetMapChunkAtBlockPos(pos);
            var chunkRealms = new List<string>();
            if (BiomesMod.getModProperty(mapChunk, BiomesModSystem.RealmPropertyName, ref chunkRealms) == EnumCommandStatus.Error)
                return true;

            var newConds = new List<FruitTreeWorldGenConds>();
            foreach (var cond in __state)
            {
                foreach (var item in BiomesMod.ModConfig.FruitTreeBiomes)
                {
                    if (WildcardUtil.Match(item.Key, cond.Type) && item.Value.Intersect(chunkRealms).Any())
                    {
                        newConds.Add(cond);
                        break;
                    }
                }
            }

            __instance.WorldGenConds = newConds.ToArray();
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(BlockFruitTreeBranch), "TryPlaceBlockForWorldGen")]
        public static void TryPlaceBlockForWorldGenPostfix(ref BlockFruitTreeBranch __instance, FruitTreeWorldGenConds[] __state, IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, LCGRandom worldgenRandom)
        {
            __instance.WorldGenConds = __state;
        }

        [HarmonyPrefix]
        [HarmonyPriority(398)]
        [HarmonyPatch(typeof(ForestFloorSystem), "GenPatches")]
        public static bool genPatchesUnderTreePrefix(ref ForestFloorSystem __instance, out List<BlockPatch> __state, IBlockAccessor blockAccessor, BlockPos pos, float forestNess, EnumTreeType treetype, LCGRandom rnd)
        {
            var blockPatchesField = Traverse.Create(__instance).Field("underTreePatches");
            var blockPatchesValue = blockPatchesField.GetValue() as List<BlockPatch>;
            __state = blockPatchesValue;

            var mapChunk = blockAccessor.GetMapChunkAtBlockPos(pos);
            var chunkRealms = new List<string>();
            if (BiomesMod.getModProperty(mapChunk, BiomesModSystem.RealmPropertyName, ref chunkRealms) == EnumCommandStatus.Error)
                return true;

            var blockPatches = new List<BlockPatch>();
            foreach (var bp in __state)
            {
                foreach (var item in BiomesMod.ModConfig.BlockPatchBiomes)
                {
                    if (bp.blockCodes.Select(x => x.Path).Any(x => WildcardUtil.Match(item.Key, x)) && item.Value.Intersect(chunkRealms).Any())
                    {
                        blockPatches.Add(bp);
                        break;
                    }
                }
            }

            blockPatchesField.SetValue(blockPatches);
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPriority(399)]
        [HarmonyPatch(typeof(ForestFloorSystem), "GenPatches")]
        public static void genPatchesUnderTreePostfix(ref ForestFloorSystem __instance, List<BlockPatch> __state, IBlockAccessor blockAccessor, BlockPos pos, float forestNess, EnumTreeType treetype, LCGRandom rnd)
        {
            Traverse.Create(__instance).Field("underTreePatches").SetValue(__state);
        }

        [HarmonyPrefix]
        [HarmonyPriority(399)]
        [HarmonyPatch(typeof(ForestFloorSystem), "GenPatches")]
        public static bool genPatchesOnTreePrefix(ref ForestFloorSystem __instance, out List<BlockPatch> __state, IBlockAccessor blockAccessor, BlockPos pos, float forestNess, EnumTreeType treetype, LCGRandom rnd)
        {
            var blockPatchesField = Traverse.Create(__instance).Field("onTreePatches");
            var blockPatchesValue = blockPatchesField.GetValue() as List<BlockPatch>;
            __state = blockPatchesValue;

            var mapChunk = blockAccessor.GetMapChunkAtBlockPos(pos);
            var chunkRealms = new List<string>();
            if (BiomesMod.getModProperty(mapChunk, BiomesModSystem.RealmPropertyName, ref chunkRealms) == EnumCommandStatus.Error)
                return true;

            var blockPatches = new List<BlockPatch>();
            foreach (var bp in __state)
            {
                foreach (var item in BiomesMod.ModConfig.BlockPatchBiomes)
                {
                    if (bp.blockCodes.Select(x => x.Path).Any(x => WildcardUtil.Match(item.Key, x)) && item.Value.Intersect(chunkRealms).Any())
                    {
                        blockPatches.Add(bp);
                        break;
                    }
                }
            }

            blockPatchesField.SetValue(blockPatches);
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPriority(398)]
        [HarmonyPatch(typeof(ForestFloorSystem), "GenPatches")]
        public static void genPatchesOnTreePostfix(ref ForestFloorSystem __instance, List<BlockPatch> __state, IBlockAccessor blockAccessor, BlockPos pos, float forestNess, EnumTreeType treetype, LCGRandom rnd)
        {
            Traverse.Create(__instance).Field("onTreePatches").SetValue(__state);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GenVegetationAndPatches), "genPatches")]
        public static bool genPatchesPrefix(ref GenVegetationAndPatches __instance, out BlockPatch[] __state, int chunkX, int chunkZ, bool postPass)
        {
            var blockAccessor = Traverse.Create(__instance).Field("blockAccessor").GetValue() as IWorldGenBlockAccessor;
            var bpc = Traverse.Create(__instance).Field("bpc").GetValue() as BlockPatchConfig;
            __state = bpc.PatchesNonTree;

            var mapChunk = blockAccessor.GetMapChunk(chunkX, chunkZ);
            var chunkRealms = new List<string>();
            if (BiomesMod.getModProperty(mapChunk, BiomesModSystem.RealmPropertyName, ref chunkRealms) == EnumCommandStatus.Error)
                return true;

            var blockPatches = new List<BlockPatch>();
            foreach (var bp in __state)
            {
                foreach (var item in BiomesMod.ModConfig.BlockPatchBiomes)
                {
                    if (bp.blockCodes.Select(x => x.Path).Any(x => WildcardUtil.Match(item.Key, x)) && item.Value.Intersect(chunkRealms).Any())
                    {
                        blockPatches.Add(bp);
                        break;
                    }
                }
            }

            bpc.PatchesNonTree = blockPatches.ToArray();
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
            
            var mapChunk = blockAccessor.GetMapChunk(chunkX, chunkZ);
            var chunkRealms = new List<string>();
            if (BiomesMod.getModProperty(mapChunk, BiomesModSystem.RealmPropertyName, ref chunkRealms) == EnumCommandStatus.Error)
                return true;

            var treeVariants = new List<TreeVariant>();
            foreach (var gen in treeGenProps.ShrubGens)
            {
                var name = gen.Generator.GetName();
                foreach (var item in BiomesMod.ModConfig.TreeBiomes)
                {
                    if (WildcardUtil.Match(item.Key, name) && item.Value.Intersect(chunkRealms).Any())
                    {
                        treeVariants.Add(gen);
                        break;
                    }
                }
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
            if (BiomesMod.getModProperty(mapChunk, BiomesModSystem.RealmPropertyName, ref chunkRealms) == EnumCommandStatus.Error)
                return true;

            var treeVariants = new List<TreeVariant>();
            foreach (var gen in treeGenProps.TreeGens)
            {
                var name = gen.Generator.GetName();
                foreach (var item in BiomesMod.ModConfig.TreeBiomes)
                {
                    if (WildcardUtil.Match(item.Key, name) && item.Value.Intersect(chunkRealms).Any())
                    {
                        treeVariants.Add(gen);
                        break;
                    }
                }
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
