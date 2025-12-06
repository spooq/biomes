using System.Runtime.CompilerServices;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Biomes.Utils;

public class Util
{
    public static List<string>? GetChunkRealms(IMapChunk chunk)
    {
        var realms = new List<string>();
        if (ModProperty.Get(chunk, ModPropName.Map.Realm, ref realms) == EnumCommandStatus.Error)
            return null;
        return realms;
    }

    // Everything possible to inline and remove indirection from inlined
    // I haven't actually benched this but it's *probably* faster?
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EnumSeason FastInlinedGetSeason(ICoreAPI api, BlockPos pos)
    {
        var hemisphere = api.World.Calendar.OnGetLatitude(pos.Z) <= 0.0 ? EnumHemisphere.South : EnumHemisphere.North;
        float seasonRel;
        if (api.World.Calendar.SeasonOverride.HasValue)
            seasonRel = api.World.Calendar.SeasonOverride.Value;
        else
            seasonRel = hemisphere != EnumHemisphere.North
                ? (float)((api.World.Calendar.YearRel + 0.5) % 1.0)
                : api.World.Calendar.YearRel;

        return (EnumSeason)(4.0 * GameMath.Mod(seasonRel - 0.21916668f, 1f));
    }
}