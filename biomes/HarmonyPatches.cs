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
        [HarmonyPatch(typeof(ForestFloorSystem), "GenPatches")]
        public static bool genPatchesPrefix(ref ForestFloorSystem __instance, out List<BlockPatch> __state, IBlockAccessor blockAccessor, BlockPos pos, float forestNess, EnumTreeType treetype, LCGRandom rnd)
        {
            var underTreePatches = Traverse.Create(__instance).Field("underTreePatches").GetValue() as List<BlockPatch>;
            __state = underTreePatches;

            var mapChunk = blockAccessor.GetMapChunkAtBlockPos(pos);
            var chunkRealms = new List<string>();
            BiomesMod.getModProperty(mapChunk, BiomesModSystem.RealmPropertyName, ref chunkRealms);

            var undertreeBlockPatches = new List<BlockPatch>();
            foreach (var gen in underTreePatches)
            {
                var allNamesForThisBP = gen.blockCodes.Select(x => x.Path);
                var allBiomesForThisBP = BiomesMod.ModConfig.ForestBlockPatchBiomes.Where(x => allNamesForThisBP.Contains(x.Key)).SelectMany(x => x.Value).Distinct();
                if (allBiomesForThisBP.Intersect(chunkRealms).Any())
                {
                    undertreeBlockPatches.Add(gen);
                    BiomesMod.sapi.BroadcastMessageToAllGroups(allBiomesForThisBP.Join(delimiter: ", "), EnumChatType.Notification);
                }
            }

            underTreePatches = undertreeBlockPatches;

            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ForestFloorSystem), "GenPatches")]
        public static void genPatchesPostfix(ref ForestFloorSystem __instance, List<BlockPatch> __state, IBlockAccessor blockAccessor, BlockPos pos, float forestNess, EnumTreeType treetype, LCGRandom rnd)
        {
            Traverse.Create(__instance).Field("underTreePatches").SetValue(__state);
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

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GenCreatures), "CanSpawnAtPosition")]
        public static bool CanSpawnAtPosition(GenCreatures __instance, ref bool __result, IBlockAccessor blockAccessor, EntityProperties type, BlockPos pos, BaseSpawnConditions sc)
        {
            return __result = BiomesMod.AllowEntitySpawn(blockAccessor.GetMapChunkAtBlockPos(pos), type);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ServerSystemEntitySpawner), "CanSpawnAt")]
        public static bool CanSpawnAt(ServerSystemEntitySpawner __instance, ref Vec3d __result, EntityProperties type, Vec3i spawnPosition, RuntimeSpawnConditions sc, IWorldChunk[] chunkCol)
        {
            return BiomesMod.AllowEntitySpawn(chunkCol[0].MapChunk, type);
        }

    }
}
