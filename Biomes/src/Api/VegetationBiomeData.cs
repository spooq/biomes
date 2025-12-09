namespace Biomes.Api;

public struct VegetationBiomeData()
{
    public List<string> realms = [];
    public BioRiver river = BioRiver.Both;

    public BiomeData toBiomeData()
    {
        var outData = new BiomeData(0);

        foreach (var realm in realms) outData.SetRealm(ExternalRegistry.RealmIndexes[realm], true);
        outData.SetFromBioRiver(river);
        return outData;
    }
}
