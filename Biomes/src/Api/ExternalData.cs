using Biomes.Utils;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Biomes.Api;

/// <summary>
///     This class provides a mechanism for getting raw biome data externally for more advanced usages.
///     You can always just get the data yourself, but it is cached to allow faster lookups so this provides some
///     mechanisms for that.
/// </summary>
public static class ExternalData
{
    private static BiomesModSystem _mod;
    private static ICoreAPI _api;

    public static void Initialize(BiomesModSystem mod, ICoreAPI api)
    {
        _mod = mod;
        _api = api;
    }

    public static BiomeData GetBiomeData(int chunkX, int chunkZ)
    {
        return _mod.Cache.ChunkData.GetBiomeData(chunkX, chunkZ);
    }

    public static BiomeData GetBiomeData(BlockPos pos)
    {
        return _mod.Cache.ChunkData.GetBiomeData(pos);
    }

    // If you are using these externally, you probably should be caching your `biomeData` you input
    public static bool EntityIsValid(BlockPos pos, BiomeData entityData)
    {
        var chunkData = _mod.Cache.ChunkData.GetBiomeData(pos);
        var season = Util.FastInlinedGetSeason(_api, pos);
        chunkData.SetSeason(season, true);
        return chunkData.CheckAgainst(entityData);
    }

    public static bool VegetationIsValid(BlockPos pos, BiomeData vegetationData)
    {
        var chunkData = _mod.Cache.ChunkData.GetBiomeData(pos);
        return chunkData.CheckRealmAndRiverAgainst(vegetationData);
    }
}
