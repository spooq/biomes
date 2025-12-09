using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Biomes.Api;
using Biomes.Utils;
using Vintagestory.API.Common;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Vintagestory.ServerMods.NoObf;

namespace Biomes.Caches;

internal class VegetationCache(BiomesModSystem mod)
{
    // fruit trees are special because the parent code can have multiple fruit tree variants
    // realm : tree base code: filtered variant genconds
    private readonly Dictionary<BiomeData, Dictionary<AssetLocation, FruitTreeWorldGenConds[]>> _fruitTreeCache = new();

    private readonly Dictionary<BiomeData, BlockPatch[]> _patchCache = new();
    private readonly Dictionary<BiomeData, TreeVariant[]> _shrubCache = new();
    private readonly Dictionary<BiomeData, TreeVariant[]> _treeCache = new();
    private readonly Dictionary<BiomeData, List<BlockPatch>> _treePatchCache = new();
    private readonly Dictionary<BiomeData, List<BlockPatch>> _underTreePatch = new();

    // There's a massive amount of duplication going on here, but nearly every implementation is subtly different.
    // You could in theory abstract this to an interface or something but the effort felt like it would be wasted
    // and just convolute the code.
    private void GenBlockPatchCacheEntry(BiomeData biomeData, ref BlockPatch[] blockPatches)
    {
        List<BlockPatch> validList = [];

        foreach (var blockPatch in blockPatches)
            foreach (var item in mod.Config.BlockPatches)
            {
                if (!blockPatch.blockCodes.Select(x => x.Path).Any(x => WildcardUtil.Match(item.Key, x))) continue;
                var validBiomeData = item.Value.ToBiomeData(mod.Config);
                if (!biomeData.CheckRealmAndRiverAgainst(validBiomeData)) continue;
                validList.Add(blockPatch);
                break;
            }

        _patchCache[biomeData] = validList.ToArray();
    }

    private void GenTreePatchCacheEntry(BiomeData biomeData, ref List<BlockPatch> blockPatches)
    {
        List<BlockPatch> validList = [];

        foreach (var blockPatch in blockPatches)
            foreach (var item in mod.Config.BlockPatches)
            {
                if (!blockPatch.blockCodes.Select(x => x.Path).Any(x => WildcardUtil.Match(item.Key, x))) continue;
                var validBiomeData = item.Value.ToBiomeData(mod.Config);
                if (!biomeData.CheckRealmAndRiverAgainst(validBiomeData)) continue;
                validList.Add(blockPatch);
                break;
            }

        _treePatchCache[biomeData] = validList;
    }

    private void GenUnderTreePatchCacheEntry(BiomeData biomeData, ref List<BlockPatch> blockPatches)
    {
        List<BlockPatch> validList = [];

        foreach (var blockPatch in blockPatches)
            foreach (var item in mod.Config.BlockPatches)
            {
                if (!blockPatch.blockCodes.Select(x => x.Path).Any(x => WildcardUtil.Match(item.Key, x))) continue;
                var validBiomeData = item.Value.ToBiomeData(mod.Config);
                if (!biomeData.CheckRealmAndRiverAgainst(validBiomeData)) continue;
                validList.Add(blockPatch);
                break;
            }

        _underTreePatch[biomeData] = validList;
    }

    private void GenShrubCacheEntry(BiomeData biomeData, ref TreeVariant[] treeVariants)
    {
        List<TreeVariant> validList = [];

        foreach (var treeVariant in treeVariants)
            foreach (var item in mod.Config.Trees)
            {
                if (!WildcardUtil.Match(item.Key, treeVariant.Generator.GetName())) continue;
                var validBiomeData = item.Value.ToBiomeData(mod.Config);
                if (!biomeData.CheckRealmAndRiverAgainst(validBiomeData)) continue;
                validList.Add(treeVariant);
                break;
            }

        _shrubCache[biomeData] = validList.ToArray();
    }

    private void GenFruitTreeCacheEntry(
        BiomeData biomeData,
        AssetLocation treecode,
        ref FruitTreeWorldGenConds[] treeVariants
    )

    {
        List<FruitTreeWorldGenConds> validList = [];

        foreach (var treeVariant in treeVariants)
            foreach (var item in mod.Config.FruitTrees)
            {
                if (!WildcardUtil.Match(item.Key, treeVariant.Type)) continue;
                var validBiomeData = item.Value.ToBiomeData(mod.Config);
                if (!biomeData.CheckRealmAndRiverAgainst(validBiomeData)) continue;
                validList.Add(treeVariant);
                break;
            }

        _fruitTreeCache[biomeData][treecode] = validList.ToArray();
    }

    private void GenTreeCacheEntry(BiomeData biomeData, ref TreeVariant[] treeVariants)
    {
        List<TreeVariant> validList = [];

        foreach (var treeVariant in treeVariants)
            foreach (var item in mod.Config.Trees)
            {
                if (!WildcardUtil.Match(item.Key, treeVariant.Generator.GetName())) continue;
                var validBiomeData = item.Value.ToBiomeData(mod.Config);
                if (!biomeData.CheckRealmAndRiverAgainst(validBiomeData)) continue;
                validList.Add(treeVariant);
                break;
            }

        _treeCache[biomeData] = validList.ToArray();
    }

    public ref TreeVariant[] GetTrees(BiomeData biomeData, ref TreeVariant[] treeVariants)
    {
        ref var cached = ref CollectionsMarshal.GetValueRefOrNullRef(_treeCache, biomeData);
        if (!Unsafe.IsNullRef(ref cached)) return ref cached;
        GenTreeCacheEntry(biomeData, ref treeVariants);
        cached = ref CollectionsMarshal.GetValueRefOrNullRef(_treeCache, biomeData);
        return ref cached;
    }

    // fruit trees are special, the base code can contain many variants within tree variants so we need to cache two
    // levels of realm : code : valid variantd 
    public ref FruitTreeWorldGenConds[] GetFruitTrees(
        BiomeData biomeData,
        AssetLocation code,
        ref FruitTreeWorldGenConds[] treeVariants
    )
    {
        if (!_fruitTreeCache.ContainsKey(biomeData))
            _fruitTreeCache[biomeData]
                = new Dictionary<AssetLocation, FruitTreeWorldGenConds[]>(new Fnv1aAssetLocationComparer());

        ref var variantCache = ref CollectionsMarshal.GetValueRefOrNullRef(_fruitTreeCache, biomeData);
        ref var cached = ref CollectionsMarshal.GetValueRefOrNullRef(variantCache, code);
        if (!Unsafe.IsNullRef(ref cached)) return ref cached;
        GenFruitTreeCacheEntry(biomeData, code, ref treeVariants);
        cached = ref CollectionsMarshal.GetValueRefOrNullRef(variantCache, code);
        return ref cached;
    }

    public ref TreeVariant[] GetShrubs(BiomeData biomeData, ref TreeVariant[] treeVariants)
    {
        ref var cached = ref CollectionsMarshal.GetValueRefOrNullRef(_shrubCache, biomeData);
        if (!Unsafe.IsNullRef(ref cached)) return ref cached;
        GenShrubCacheEntry(biomeData, ref treeVariants);
        cached = ref CollectionsMarshal.GetValueRefOrNullRef(_shrubCache, biomeData);
        return ref cached;
    }

    public ref BlockPatch[] GetGroundPatches(BiomeData biomeData, ref BlockPatch[] blockPatches)
    {
        ref var cached = ref CollectionsMarshal.GetValueRefOrNullRef(_patchCache, biomeData);
        if (!Unsafe.IsNullRef(ref cached)) return ref cached;
        GenBlockPatchCacheEntry(biomeData, ref blockPatches);
        cached = ref CollectionsMarshal.GetValueRefOrNullRef(_patchCache, biomeData);
        return ref cached;
    }

    public ref List<BlockPatch> GetTreePatches(BiomeData biomeData, ref List<BlockPatch> blockPatches)
    {
        ref var cached = ref CollectionsMarshal.GetValueRefOrNullRef(_treePatchCache, biomeData);
        if (!Unsafe.IsNullRef(ref cached)) return ref cached;
        GenTreePatchCacheEntry(biomeData, ref blockPatches);
        cached = ref CollectionsMarshal.GetValueRefOrNullRef(_treePatchCache, biomeData);
        return ref cached;
    }

    public ref List<BlockPatch> GetUnderTreePatches(BiomeData biomeData, ref List<BlockPatch> blockPatches)
    {
        ref var cached = ref CollectionsMarshal.GetValueRefOrNullRef(_underTreePatch, biomeData);
        if (!Unsafe.IsNullRef(ref cached)) return ref cached;
        GenUnderTreePatchCacheEntry(biomeData, ref blockPatches);
        cached = ref CollectionsMarshal.GetValueRefOrNullRef(_underTreePatch, biomeData);
        return ref cached;
    }
}
