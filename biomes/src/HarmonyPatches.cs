using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Vintagestory.Server;
using Vintagestory.ServerMods;
using Vintagestory.ServerMods.NoObf;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global

namespace Biomes;

[HarmonyPatch]
public static class HarmonyPatches
{
    private static Harmony harmony = null!;
    private static BiomesModSystem _mod = null!;

    public static void Init(BiomesModSystem mod)
    {
        _mod = mod;
        harmony = new Harmony(_mod.Mod.Info.ModID);
        harmony.PatchAll();
    }

    public static void Shutdown()
    {
        harmony.UnpatchAll(_mod.Mod.Info.ModID);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(BlockFruitTreeBranch), "TryPlaceBlockForWorldGen")]
    public static bool TryPlaceBlockForWorldGenPrefix(ref BlockFruitTreeBranch __instance,
        out FruitTreeWorldGenConds[] __state, IBlockAccessor blockAccessor, BlockPos pos)
    {
        var code = __instance.Code!;
        __state = __instance.WorldGenConds;

        var biomeData = _mod.Cache.ChunkData.GetBiomeData(pos);
        if (biomeData.IsNullData()) return true;

        var cached =
            _mod.Cache.Vegetation.GetFruitTrees(biomeData, code, ref __state);

        __instance.WorldGenConds = cached;
        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BlockFruitTreeBranch), "TryPlaceBlockForWorldGen")]
    public static void TryPlaceBlockForWorldGenPostfix(ref BlockFruitTreeBranch __instance,
        FruitTreeWorldGenConds[] __state)
    {
        __instance.WorldGenConds = __state;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ForestFloorSystem), "GenPatches")]
    public static bool GenPatchesTreePrefix(ref ForestFloorSystem __instance,
        out (List<BlockPatch>, List<BlockPatch>) __state, IBlockAccessor blockAccessor, BlockPos pos)
    {
        var underTreeField = Traverse.Create(__instance).Field("underTreePatches");
        var underTreeValue = underTreeField.GetValue() as List<BlockPatch>;

        var onTreeField = Traverse.Create(__instance).Field("onTreePatches");
        var onTreeValue = onTreeField.GetValue() as List<BlockPatch>;

        __state = (underTreeValue, onTreeValue);

        var biomeData = _mod.Cache.ChunkData.GetBiomeData(pos);
        if (biomeData.IsNullData()) return true;

        var cachedUnderTree = _mod.Cache.Vegetation.GetUnderTreePatches(biomeData, ref underTreeValue);
        underTreeField.SetValue(cachedUnderTree);

        var cachedOnTree =
            _mod.Cache.Vegetation.GetTreePatches(biomeData, ref onTreeValue);
        onTreeField.SetValue(cachedOnTree);

        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ForestFloorSystem), "GenPatches")]
    public static void GenPatchesTreePostfix(ref ForestFloorSystem __instance,
        (List<BlockPatch>, List<BlockPatch>) __state)
    {
        Traverse.Create(__instance).Field("underTreePatches").SetValue(__state.Item1);
        Traverse.Create(__instance).Field("onTreePatches").SetValue(__state.Item2);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GenVegetationAndPatches), "genPatches")]
    public static bool genPatchesPrefix(ref GenVegetationAndPatches __instance, out BlockPatch[] __state, int chunkX,
        int chunkZ, bool postPass)
    {
        var bpc = Traverse.Create(__instance).Field("bpc").GetValue() as BlockPatchConfig;
        __state = bpc!.PatchesNonTree;

        var biomeData = _mod.Cache.ChunkData.GetBiomeData(chunkX, chunkZ);
        if (biomeData.IsNullData()) return true;

        bpc.PatchesNonTree = _mod.Cache.Vegetation.GetGroundPatches(biomeData, ref bpc.PatchesNonTree);
        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GenVegetationAndPatches), "genPatches")]
    public static void genPatchesPostfix(ref GenVegetationAndPatches __instance, BlockPatch[] __state, int chunkX,
        int chunkZ, bool postPass)
    {
        var bpc = Traverse.Create(__instance).Field("bpc").GetValue() as BlockPatchConfig;
        bpc.PatchesNonTree = __state;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GenVegetationAndPatches), "genShrubs")]
    public static bool genShrubsPrefix(ref GenVegetationAndPatches __instance, out TreeVariant[] __state,
        int chunkX, int chunkZ)
    {
        var treeSupplier = Traverse.Create(__instance).Field("treeSupplier").GetValue() as WgenTreeSupplier;
        var treeGenProps = Traverse.Create(treeSupplier).Field("treeGenProps").GetValue() as TreeGenProperties;
        __state = treeGenProps!.ShrubGens;

        var biomeData = _mod.Cache.ChunkData.GetBiomeData(chunkX, chunkZ);
        if (biomeData.IsNullData()) return true;

        treeGenProps.ShrubGens =
            _mod.Cache.Vegetation.GetShrubs(biomeData, ref treeGenProps.ShrubGens);
        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GenVegetationAndPatches), "genShrubs")]
    public static void genShrubsPostfix(ref GenVegetationAndPatches __instance, TreeVariant[] __state, int chunkX,
        int chunkZ)
    {
        var treeSupplier = Traverse.Create(__instance).Field("treeSupplier").GetValue() as WgenTreeSupplier;
        var treeGenProps = Traverse.Create(treeSupplier).Field("treeGenProps").GetValue() as TreeGenProperties;
        treeGenProps.ShrubGens = __state;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GenVegetationAndPatches), "genTrees")]
    public static bool genTreesPrefix(ref GenVegetationAndPatches __instance, out TreeVariant[] __state, int chunkX,
        int chunkZ)
    {
        var treeSupplier = Traverse.Create(__instance).Field("treeSupplier").GetValue() as WgenTreeSupplier;
        var treeGenProps = Traverse.Create(treeSupplier).Field("treeGenProps").GetValue() as TreeGenProperties;
        __state = treeGenProps!.TreeGens;

        var biomeData = _mod.Cache.ChunkData.GetBiomeData(chunkX, chunkZ);
        if (biomeData.IsNullData()) return true;

        treeGenProps.TreeGens =
            _mod.Cache.Vegetation.GetTrees(biomeData, ref treeGenProps.TreeGens);
        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GenVegetationAndPatches), "genTrees")]
    public static void GenTreesPostfix(ref GenVegetationAndPatches __instance, TreeVariant[] __state, int chunkX,
        int chunkZ)
    {
        var treeSupplier = Traverse.Create(__instance).Field("treeSupplier").GetValue() as WgenTreeSupplier;
        var treeGenProps = Traverse.Create(treeSupplier).Field("treeGenProps").GetValue() as TreeGenProperties;
        treeGenProps.TreeGens = __state;
    }

    // World-gen spawn
    [HarmonyPrefix]
    [HarmonyPatch(typeof(GenCreatures), "CanSpawnAtPosition")]
    public static bool CanSpawnAtPosition(ref bool __result, IBlockAccessor blockAccessor, EntityProperties type,
        BlockPos pos, BaseSpawnConditions sc)
    {
        __result = _mod.Cache.Entities.IsSpawnValid(type, pos);
        return __result;
    }

    // Run-time spawn
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ServerSystemEntitySpawner), "CanSpawnAt_offthread")]
    public static bool CanSpawnAt(ref Vec3d? __result, EntityProperties type, Vec3i spawnPosition,
        RuntimeSpawnConditions sc, IWorldChunk[] chunkCol)
    {
        return chunkCol.Length != 0 &&
               _mod.Cache.Entities.IsSpawnValid(type, spawnPosition.AsBlockPos);
    }
}