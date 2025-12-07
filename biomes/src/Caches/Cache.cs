using Vintagestory.API.Common;

namespace Biomes.Caches;

public readonly struct Cache(BiomesModSystem mod, ICoreAPI api)
{
    public ChunkDataCache ChunkData { get; } = new(mod, api);
    public VegetationCache Vegetation { get; } = new(mod);
    public EntityCache Entities { get; } = new(mod, api);
}