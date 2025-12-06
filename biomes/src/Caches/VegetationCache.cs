using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Vintagestory.API.Common;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Vintagestory.ServerMods.NoObf;

namespace Biomes.Caches;

public class VegetationCache
{
    public const string KeySeparator = ";";

    // fruit trees are special because the parent code can have multiple fruit tree variants
    // realm : tree base code: filtered variant genconds
    private readonly Dictionary<string, Dictionary<AssetLocation, FruitTreeWorldGenConds[]>> _fruitTreeCache =
        new(StringComparer.Ordinal);

    private readonly Dictionary<string, BlockPatch[]> _patchCache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TreeVariant[]> _shrubCache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TreeVariant[]> _treeCache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<BlockPatch>> _treePatchCache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<BlockPatch>> _underTreePatch = new(StringComparer.Ordinal);

    // There's a massive amount of duplication going on here, but nearly every implementation is subtly different.
    // You could in theory abstract this to an interface or something but the effort felt like it would be wasted
    // and just convolute the code.
    private void GenBlockPatchCacheEntry(List<string> realms, ref BlockPatch[] blockPatches,
        ref Dictionary<string, ConfigItem> biomeConfig)
    {
        List<BlockPatch> validList = [];

        foreach (var blockPatch in blockPatches)
        foreach (var item in biomeConfig)
        {
            if (!blockPatch.blockCodes.Select(x => x.Path).Any(x => WildcardUtil.Match(item.Key, x))) continue;
            if (!item.Value.biorealm.Intersect(realms).Any()) continue;
            validList.Add(blockPatch);
            break;
        }

        _patchCache[ToCacheKey(realms)] = validList.ToArray();
    }

    private void GenTreePatchCacheEntry(List<string> realms, ref List<BlockPatch> blockPatches,
        ref Dictionary<string, ConfigItem> biomeConfig)
    {
        List<BlockPatch> validList = [];

        foreach (var blockPatch in blockPatches)
        foreach (var item in biomeConfig)
        {
            if (!blockPatch.blockCodes.Select(x => x.Path).Any(x => WildcardUtil.Match(item.Key, x))) continue;
            if (!item.Value.biorealm.Intersect(realms).Any()) continue;
            validList.Add(blockPatch);
            break;
        }

        _treePatchCache[ToCacheKey(realms)] = validList;
    }

    private void GenUnderTreePatchCacheEntry(List<string> realms, ref List<BlockPatch> blockPatches,
        ref Dictionary<string, ConfigItem> biomeConfig)
    {
        List<BlockPatch> validList = [];

        foreach (var blockPatch in blockPatches)
        foreach (var item in biomeConfig)
        {
            if (!blockPatch.blockCodes.Select(x => x.Path).Any(x => WildcardUtil.Match(item.Key, x))) continue;
            if (!item.Value.biorealm.Intersect(realms).Any()) continue;
            validList.Add(blockPatch);
            break;
        }

        _underTreePatch[ToCacheKey(realms)] = validList;
    }

    private void GenShrubCacheEntry(List<string> realms, ref TreeVariant[] treeVariants,
        ref Dictionary<string, ConfigItem> biomeConfig)
    {
        List<TreeVariant> validList = [];

        foreach (var treeVariant in treeVariants)
        foreach (var item in biomeConfig)
        {
            if (!WildcardUtil.Match(item.Key, treeVariant.Generator.GetName())) continue;
            if (!item.Value.biorealm.Intersect(realms).Any()) continue;
            validList.Add(treeVariant);
            break;
        }

        _shrubCache[ToCacheKey(realms)] = validList.ToArray();
    }

    private void GenFruitTreeCacheEntry(List<string> realms, AssetLocation treecode,
        ref FruitTreeWorldGenConds[] treeVariants,
        ref Dictionary<string, ConfigItem> biomeConfig)
    {
        List<FruitTreeWorldGenConds> validList = [];

        foreach (var treeVariant in treeVariants)
        foreach (var item in biomeConfig)
        {
            if (!WildcardUtil.Match(item.Key, treeVariant.Type)) continue;
            if (!item.Value.biorealm.Intersect(realms).Any()) continue;
            validList.Add(treeVariant);
            break;
        }

        _fruitTreeCache[ToCacheKey(realms)][treecode] = validList.ToArray();
    }

    private void GenTreeCacheEntry(List<string> realms, ref TreeVariant[] treeVariants,
        ref Dictionary<string, ConfigItem> biomeConfig)
    {
        List<TreeVariant> validList = [];

        foreach (var treeVariant in treeVariants)
        foreach (var item in biomeConfig)
        {
            if (!WildcardUtil.Match(item.Key, treeVariant.Generator.GetName())) continue;
            if (!item.Value.biorealm.Intersect(realms).Any()) continue;
            validList.Add(treeVariant);
            break;
        }

        _treeCache[ToCacheKey(realms)] = validList.ToArray();
    }

    // todo: kill this, eventually replace with storing cache lookup keys as the gen data
    private static string ToCacheKey(List<string> realms)
    {
        realms.Sort(StringComparer.Ordinal);
        return string.Join(KeySeparator, realms);
    }

    public ref TreeVariant[] GetTrees(List<string> realms, ref TreeVariant[] treeVariants,
        ref Dictionary<string, ConfigItem> biomeConfig)
    {
        var cacheKey = ToCacheKey(realms);
        ref var cached = ref CollectionsMarshal.GetValueRefOrNullRef(_treeCache, cacheKey);
        if (!Unsafe.IsNullRef(ref cached)) return ref cached;
        GenTreeCacheEntry(realms, ref treeVariants, ref biomeConfig);
        cached = ref CollectionsMarshal.GetValueRefOrNullRef(_treeCache, cacheKey);
        return ref cached;
    }

    // fruit trees are special, the base code can contain many variants within tree variants so we need to cache two
    // levels of realm : code : valid variantd 
    public ref FruitTreeWorldGenConds[] GetFruitTrees(List<string> realms, AssetLocation code,
        ref FruitTreeWorldGenConds[] treeVariants, ref Dictionary<string, ConfigItem> biomeConfig)
    {
        var cacheKey = ToCacheKey(realms);
        if (!_fruitTreeCache.ContainsKey(cacheKey))
            _fruitTreeCache[cacheKey] = new Dictionary<AssetLocation, FruitTreeWorldGenConds[]>();

        ref var variantCache = ref CollectionsMarshal.GetValueRefOrNullRef(_fruitTreeCache, cacheKey);
        ref var cached = ref CollectionsMarshal.GetValueRefOrNullRef(variantCache, code);
        if (!Unsafe.IsNullRef(ref cached)) return ref cached;
        GenFruitTreeCacheEntry(realms, code, ref treeVariants, ref biomeConfig);
        cached = ref CollectionsMarshal.GetValueRefOrNullRef(variantCache, code);
        return ref cached;
    }

    public ref TreeVariant[] GetShrubs(List<string> realms, ref TreeVariant[] treeVariants,
        ref Dictionary<string, ConfigItem> biomeConfig)
    {
        var cacheKey = ToCacheKey(realms);
        ref var cached = ref CollectionsMarshal.GetValueRefOrNullRef(_shrubCache, cacheKey);
        if (!Unsafe.IsNullRef(ref cached)) return ref cached;
        GenShrubCacheEntry(realms, ref treeVariants, ref biomeConfig);
        cached = ref CollectionsMarshal.GetValueRefOrNullRef(_shrubCache, cacheKey);
        return ref cached;
    }

    public ref BlockPatch[] GetGroundPatches(
        List<string> realms, ref BlockPatch[] blockPatches, ref Dictionary<string, ConfigItem> biomeConfig)
    {
        var cacheKey = ToCacheKey(realms);
        ref var cached = ref CollectionsMarshal.GetValueRefOrNullRef(_patchCache, cacheKey);
        if (!Unsafe.IsNullRef(ref cached)) return ref cached;
        GenBlockPatchCacheEntry(realms, ref blockPatches, ref biomeConfig);
        cached = ref CollectionsMarshal.GetValueRefOrNullRef(_patchCache, cacheKey);
        return ref cached;
    }

    public ref List<BlockPatch> GetTreePatches(
        List<string> realms, ref List<BlockPatch> blockPatches, ref Dictionary<string, ConfigItem> biomeConfig)
    {
        var cacheKey = ToCacheKey(realms);
        ref var cached = ref CollectionsMarshal.GetValueRefOrNullRef(_treePatchCache, cacheKey);
        if (!Unsafe.IsNullRef(ref cached)) return ref cached;
        GenTreePatchCacheEntry(realms, ref blockPatches, ref biomeConfig);
        cached = ref CollectionsMarshal.GetValueRefOrNullRef(_treePatchCache, cacheKey);
        return ref cached;
    }

    public ref List<BlockPatch> GetUnderTreePatches(
        List<string> realms, ref List<BlockPatch> blockPatches, ref Dictionary<string, ConfigItem> biomeConfig)
    {
        var cacheKey = ToCacheKey(realms);
        ref var cached = ref CollectionsMarshal.GetValueRefOrNullRef(_underTreePatch, cacheKey);
        if (!Unsafe.IsNullRef(ref cached)) return ref cached;
        GenUnderTreePatchCacheEntry(realms, ref blockPatches, ref biomeConfig);
        cached = ref CollectionsMarshal.GetValueRefOrNullRef(_underTreePatch, cacheKey);
        return ref cached;
    }
}