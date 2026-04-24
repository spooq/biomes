using System.Collections.Concurrent;
using Biomes.Api;
using Biomes.Utils;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Biomes.Caches;

internal class ChunkDataCache(BiomesModSystem mod, ICoreAPI api)
{
    private readonly ConcurrentDictionary<FastVec2i, BiomeData> _cache = new();

    public void CacheData(FastVec2i pos, BiomeData biomeData)
    {
        _cache[pos] = biomeData;
    }

    public BiomeData GetBiomeData(FastVec2i pos)
    {
        if (!_cache.ContainsKey(pos))
        {
            var mapChunk = api.World.BlockAccessor.GetMapChunk(new Vec2i(pos.X, pos.Y));
            //TODO: This is a bad hack that should probably be fixed eventually.
            // in 1.22, something changed with the load order or something where occasionally chunks that don't exist yet
            // queried for spawns that don't exist yet.
            // I have absolutely no idea why these spawns are being queried or what's happening here, so I instead just
            // return "yeah the spawn is allowed by biomes"
            return new BiomeData(int.MaxValue);
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
        _cache.Remove(index, out _);
    }
}
