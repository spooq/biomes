using System.Runtime.CompilerServices;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Biomes.RealmGen;

public class ClassicGenConfig : RealmGenConfig
{
    public const string TypeKey = "classic";

    public List<string> NorthernRealms = DefaultRealmOrder.Northern;

    public List<string> SouthernRealms = DefaultRealmOrder.Southern;
}

// original generation style from 1.0. Slow and deeply flawed, included purely as a compatibility measure for those
// who really don't want odd realm generation at borders of existing world.
public class ClassicRealmGen(ClassicGenConfig config) : IRealmGen
{
    public List<string> GetRealmsForBlockPos(ICoreServerAPI api, BlockPos blockPos)
    {
        var chunkX = blockPos.X / api.WorldManager.ChunkSize;
        var chunkZ = blockPos.Z / api.WorldManager.ChunkSize;
        var hemisphere = api.World.Calendar.GetHemisphere(blockPos);
        var realmNames = new List<string>(4);
        var currentRealm = CalculateValues(api, chunkX - 1, chunkZ, hemisphere);
        realmNames.Add(NorthOrSouth(hemisphere, currentRealm));
        currentRealm = CalculateValues(api, chunkX + 1, chunkZ, hemisphere);
        realmNames.Add(NorthOrSouth(hemisphere, currentRealm));
        currentRealm = CalculateValues(api, chunkX, chunkZ - 1, hemisphere);
        realmNames.Add(NorthOrSouth(hemisphere, currentRealm));
        currentRealm = CalculateValues(api, chunkX, chunkZ + 1, hemisphere);
        realmNames.Add(NorthOrSouth(hemisphere, currentRealm));
        currentRealm = CalculateValues(api, chunkX, chunkZ, hemisphere);
        realmNames.Add(NorthOrSouth(hemisphere, currentRealm));
        realmNames = realmNames.Distinct().ToList();

        realmNames.Capacity = realmNames.Count;
        realmNames.Sort(StringComparer.Ordinal);
        return realmNames;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string NorthOrSouth(EnumHemisphere hemisphere, int realm)
    {
        // modconfig.flipworld exchanges the lists, so we always choose the same here no matter what
        return hemisphere == EnumHemisphere.North
            ? config.NorthernRealms[realm]
            : config.SouthernRealms[realm];
    }


    private int CalculateValues(ICoreServerAPI api, int chunkX, int chunkZ, EnumHemisphere hemisphere)
    {
        var realmCount = hemisphere == EnumHemisphere.North
            ? config.NorthernRealms.Count
            : config.SouthernRealms.Count;

        var worldWidthInChunks = api.WorldManager.MapSizeX / api.WorldManager.ChunkSize;
        var realmWidthInChunks = worldWidthInChunks / (float)realmCount;
        var currentRealm = 0;
        if (realmWidthInChunks != 0)
            currentRealm = (int)(chunkX / realmWidthInChunks);
        if (currentRealm >= realmCount)
            currentRealm = realmCount - 1;
        if (currentRealm < 0)
            currentRealm = 0;
        return currentRealm;
    }
}