using Vintagestory.API.Common;

namespace Biomes.Caches;

internal readonly struct Cache(BiomesModSystem mod, ICoreAPI api)
{
    internal ChunkDataCache ChunkData { get; } = new(mod, api);
    internal VegetationCache Vegetation { get; } = new(mod);
    internal EntityCache Entities { get; } = new(mod, api);
}
