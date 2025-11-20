using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
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
    private static BiomesModSystem biomesMod = null!;

    public static void Init(BiomesModSystem mod)
    {
        biomesMod = mod;
        harmony = new Harmony(biomesMod.Mod.Info.ModID);
        harmony.PatchAll();
    }

    public static void Shutdown()
    {
        harmony.UnpatchAll(biomesMod.Mod.Info.ModID);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(BlockFruitTreeBranch), "TryPlaceBlockForWorldGen")]
    public static bool TryPlaceBlockForWorldGenPrefix(ref BlockFruitTreeBranch __instance,
        out FruitTreeWorldGenConds[] __state, IBlockAccessor blockAccessor, BlockPos pos)
    {
        __state = __instance.WorldGenConds;

        biomesMod.originalFruitTrees ??= __state;
        var chunk = blockAccessor.GetMapChunkAtBlockPos(pos);
        var realms = BiomesModSystem.GetChunkRealms(chunk);

        var cached =
            biomesMod.Cache.GetCachedFruitTrees(realms, ref __state, ref biomesMod.BiomeConfig.FruitTreeBiomes);

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

        var chunk = blockAccessor.GetMapChunkAtBlockPos(pos);
        var realms = BiomesModSystem.GetChunkRealms(chunk);
        if (realms == null) return true;

        var cachedUnderTree = biomesMod.Cache.GetCachedUnderTreePatches(realms, ref underTreeValue,
            ref biomesMod.BiomeConfig.BlockPatchBiomes);
        underTreeField.SetValue(cachedUnderTree);

        var cachedOnTree =
            biomesMod.Cache.GetCachedTreePatches(realms, ref onTreeValue, ref biomesMod.BiomeConfig.BlockPatchBiomes);
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
        var blockAccessor = Traverse.Create(__instance).Field("blockAccessor").GetValue() as IWorldGenBlockAccessor;
        var bpc = Traverse.Create(__instance).Field("bpc").GetValue() as BlockPatchConfig;
        __state = bpc!.PatchesNonTree;

        var chunk = blockAccessor.GetMapChunk(chunkX, chunkZ);
        var realms = BiomesModSystem.GetChunkRealms(chunk);
        if (realms == null) return true;

        bpc.PatchesNonTree = biomesMod.Cache.GetCachedGroundPatches(realms, ref bpc.PatchesNonTree,
            ref biomesMod.BiomeConfig.BlockPatchBiomes);
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
        var blockAccessor = Traverse.Create(__instance).Field("blockAccessor").GetValue() as IWorldGenBlockAccessor;
        var treeSupplier = Traverse.Create(__instance).Field("treeSupplier").GetValue() as WgenTreeSupplier;
        var treeGenProps = Traverse.Create(treeSupplier).Field("treeGenProps").GetValue() as TreeGenProperties;
        __state = treeGenProps!.ShrubGens;

        var chunk = blockAccessor.GetMapChunk(chunkX, chunkZ);
        var realms = BiomesModSystem.GetChunkRealms(chunk);
        if (realms == null) return true;

        realms.Sort(StringComparer.Ordinal);
        treeGenProps.ShrubGens =
            biomesMod.Cache.GetCachedShrubs(realms, ref treeGenProps.ShrubGens, ref biomesMod.BiomeConfig.TreeBiomes);
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
        var blockAccessor = Traverse.Create(__instance).Field("blockAccessor").GetValue() as IWorldGenBlockAccessor;
        var treeSupplier = Traverse.Create(__instance).Field("treeSupplier").GetValue() as WgenTreeSupplier;
        var treeGenProps = Traverse.Create(treeSupplier).Field("treeGenProps").GetValue() as TreeGenProperties;
        __state = treeGenProps!.TreeGens;

        var chunk = blockAccessor.GetMapChunk(chunkX, chunkZ);
        var realms = BiomesModSystem.GetChunkRealms(chunk);
        if (realms == null) return true;

        treeGenProps.TreeGens =
            biomesMod.Cache.GetCachedTrees(realms, ref treeGenProps.TreeGens, ref biomesMod.BiomeConfig.TreeBiomes);
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
        return __result = biomesMod.AllowEntitySpawn(blockAccessor.GetMapChunkAtBlockPos(pos), type, pos);
    }

    // Run-time spawn
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ServerSystemEntitySpawner), "CanSpawnAt")]
    public static bool CanSpawnAt(ref Vec3d __result, EntityProperties type, Vec3i spawnPosition,
        RuntimeSpawnConditions sc, IWorldChunk[] chunkCol)
    {
        return chunkCol.Any() && biomesMod.AllowEntitySpawn(chunkCol[0].MapChunk, type, spawnPosition.AsBlockPos);
    }
}