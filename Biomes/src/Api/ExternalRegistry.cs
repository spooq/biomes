using System.Collections.ObjectModel;
using Vintagestory.API.Common;

namespace Biomes.Api;

/// <summary>
///     Use this if you want to programatically register generation compatibility with Biomes. Biomes internally
///     uses a lot of caching and awkward data formats, and manually checking spawn validity is clunky so instead
///     you should register your types with this class and let Biomes handle the rest.
///     If you decide instead to manually deal with the biome data itself, you're on your own.
///     Registration should be done in your main mod start, as this registry initializes at the end of
///     AssetsFinalize and you want to make sure registration is done before world gen starts.
/// </summary>
public static class ExternalRegistry
{
    private const string TooEarlyError
        = "Attempted to register too early! Make sure you're registering some time after AssetsFinalize in your main Start or later!";

    private static BiomesModSystem? _mod;

    public static ReadOnlyCollection<string> ValidRealms => _mod!.Config.ValidRealms.AsReadOnly();
    public static ReadOnlyDictionary<string, int> RealmIndexes => _mod!.Config.ValidRealmIndexes.AsReadOnly();

    internal static void Initialize(BiomesModSystem mod)
    {
        _mod = mod;
    }

    public static void RegisterEntity(AssetLocation location, EntityBiomeData entityBiomeData)
    {
        if (_mod == null) throw new Exception(TooEarlyError);

        _mod.Cache.Entities.RegisterEntity(location, entityBiomeData.ToBiomeData());
    }

    public static void RegisterWhitelist(AssetLocation location)
    {
        if (_mod == null) throw new Exception(TooEarlyError);
        _mod.Cache.Entities.RegisterWhitelist(location);
    }

    public static void RegisterBlockpatch(string wildcardNames, VegetationBiomeData biomeData)
    {
        if (_mod == null) throw new Exception(TooEarlyError);
        _mod.Config.FruitTrees[wildcardNames] = new ConfigItem
        {
            biorealm = biomeData.realms, bioriver = biomeData.river
        };
    }

    public static void RegisterTree(string wildcardNames, VegetationBiomeData biomeData)
    {
        if (_mod == null) throw new Exception(TooEarlyError);

        _mod.Config.Trees[wildcardNames] = new ConfigItem { biorealm = biomeData.realms, bioriver = biomeData.river };
    }

    public static void RegisterFruitTree(string wildcardNames, VegetationBiomeData biomeData)
    {
        if (_mod == null) throw new Exception(TooEarlyError);
        _mod.Config.BlockPatches[wildcardNames] = new ConfigItem
        {
            biorealm = biomeData.realms, bioriver = biomeData.river
        };
    }
}
