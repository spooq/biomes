using Biomes.Api;
using Biomes.Utils;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Biomes.Caches;

internal class ChunkDataCache(BiomesModSystem mod, ICoreAPI api)
{
    private readonly Dictionary<FastVec2i, BiomeData> _cache = new();

    public void CacheData(FastVec2i pos, BiomeData biomeData)
    {
        _cache[pos] = biomeData;
    }

    public BiomeData GetBiomeData(FastVec2i pos)
    {
        if (!_cache.ContainsKey(pos))
        {
            var mapChunk = api.World.BlockAccessor.GetMapChunk(new Vec2i(pos.X, pos.Y));
            CacheData(pos, mapChunk.GetModdata(ModPropName.MapChunk.BiomeData, new BiomeData(0)));
        }

        return _cache[pos];
    }

    public BiomeData GetBiomeData(BlockPos pos)
    {
        var chunkPosX = pos.X / 32;
        var chunkPosZ = pos.Z / 32;
        return GetBiomeData(new FastVec2i(chunkPosX, chunkPosZ));
    }

    public BiomeData GetBiomeData(int chunkX, int chunkZ)
    {
        return GetBiomeData(new FastVec2i(chunkX, chunkZ));
    }

    public void Evict(FastVec2i index)
    {
        _cache.Remove(index);
    }
}
