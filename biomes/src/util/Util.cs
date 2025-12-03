using Vintagestory.API.Common;

namespace Biomes.util;

public class Util
{
    public static List<string>? GetChunkRealms(IMapChunk chunk)
    {
        var realms = new List<string>();
        if (ModProperty.Get(chunk, ModPropName.Map.Realm, ref realms) == EnumCommandStatus.Error)
            return null;
        return realms;
    }
}