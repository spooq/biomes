using Vintagestory.API.Common;

namespace Biomes.Api;

public struct EntityBiomeData()
{
    public List<string> realms = [];
    public BioRiver river = BioRiver.Both;
    public List<EnumSeason> seasons = [];

    public BiomeData ToBiomeData()
    {
        var outData = new BiomeData(0);
        foreach (var realm in realms) outData.SetRealm(ExternalRegistry.RealmIndexes[realm], true);
        outData.SetFromBioRiver(river);
        if (seasons.Count > 0)
            foreach (var season in seasons)
                outData.SetSeason(season, true);
        else
            outData.SetAllSeasons(true);
        return outData;
    }
}
