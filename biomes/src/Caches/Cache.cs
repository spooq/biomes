using Vintagestory.API.Common;

namespace Biomes.Caches;

public class Cache(BiomesModSystem mod, ICoreAPI api)
{
    public VegetationCache Vegetation { get; } = new();
    public EntityCache Entities { get; } = new(mod, api);
}