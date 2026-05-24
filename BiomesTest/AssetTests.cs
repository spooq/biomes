using Newtonsoft.Json.Linq;

namespace BiomesTest;

// sloppy homecooked schema validation for json files

public static class DataSources
{
    public static IEnumerable<Func<string>> BlockConfigSources()
    {
        var enumerable = Directory.EnumerateFiles($"{BiomeNameTests.BIOMES_ASSETS_PATH}/config/biomes/blockconfig", "*.*", SearchOption.AllDirectories);
        return enumerable.Select<string, Func<string>>(x => () => x);
    }

    public static IEnumerable<Func<string>> PatchSources()
    {
        var enumerable = Directory.EnumerateFiles($"{BiomeNameTests.BIOMES_ASSETS_PATH}/patches", "*.*", SearchOption.AllDirectories);
        return enumerable.Select<string, Func<string>>(x => () => x);
    }
}

public class BiomeNameTests
{
    public const string BIOMES_ASSETS_PATH = "../../../../Biomes/assets/biomes";
    const string REALMS_PATH = $"{BIOMES_ASSETS_PATH}/config/realms.json";
    static HashSet<string> _validRealms = [];

    [Before(Class)]
    public static async Task SetupValidRealms()
    {
        if (_validRealms.Count != 0) return;
        
        var realmsJson = JArray.Parse(await File.ReadAllTextAsync(REALMS_PATH));
        foreach (var realm in realmsJson)
        {
            _validRealms.Add(realm.ToString());
        }
    }
    
    [Test]
    [MethodDataSource(typeof(DataSources), nameof(DataSources.BlockConfigSources))]
    public async Task CheckConfigRealmNames(string path)
    {
        var badRegions = new List<string>();
        var badRivers = new List<string>();
        JObject config = JObject.Parse(await File.ReadAllTextAsync(path));

        async void CheckFor(JToken token)
        {
            if (token is not JObject obj) return;
            foreach (var (key, value) in obj)
            {
                if (value is not JObject itemObj || itemObj["biorealm"] is not JArray realmArray) continue;
                foreach (var realm in realmArray)
                {
                    if (!_validRealms.Contains(realm.ToString()))
                        badRegions.Add($"{path}:{key}:{realm}");
                }

                var riverValue = itemObj["bioriver"]?.ToString().ToLower();
                if (riverValue is not null)
                {
                    if (riverValue != "both" && riverValue != "riveronly" && riverValue != "noriver")
                        badRivers.Add($"{path}:{key}:{riverValue}");
                }
            }
        }

        CheckFor(config["BlockPatches"]!);
        CheckFor(config["Trees"]!);
        CheckFor(config["FruitTrees"]!);
        await Assert.That(badRegions).IsEmpty().Because("Some blockconfigs contained invalid realm names!");
        await Assert.That(badRivers).IsEmpty().Because("Some blockconfigs contained invalid bioriver values!");
        
    }
    
    [Test]
    [MethodDataSource(typeof(DataSources), nameof(DataSources.PatchSources))]
    public async Task CheckPatchRealmNames(string path)
    {
        var badRealms = new List<string>();
        var badRivers = new List<string>();
        var badSeasons = new List<string>();
        var patchset = JArray.Parse(await File.ReadAllTextAsync(path));
        
        // I don't like this but it's very easy to follow
        foreach (var patch in patchset)
        {
            var patchTarget = patch["path"].ToString();
            // checking base attrs and variant attrs is the same, so just use a list and push the one root to the list
            var toCheck = new List<(string, JToken)>();

            if (patchTarget == "/attributes")
            {
                toCheck.Add(("root", patch["value"]));
            } else if (patchTarget == "/attributesByType")
            {
                var valueObj = (JObject)patch["value"];
                foreach (var (key, value) in valueObj)
                {
                    toCheck.Add((key, value));
                }
            }

            foreach (var (key, value) in toCheck)
            {
                if (value is not JObject itemObj || itemObj["biorealm"] is not JArray realmArray) throw new Exception("Something is *really* wrong with the patch file");
                foreach (var realm in realmArray)
                {
                    if (!_validRealms.Contains(realm.ToString()))
                        badRealms.Add($"{path}:{key}:{realm}");
                }
                var riverValue = itemObj["bioriver"]?.ToString().ToLower();
                if (riverValue is not null)
                {
                    if (riverValue != "both" && riverValue != "riveronly" && riverValue != "noriver")
                        badRivers.Add($"{path}:{key}:{riverValue}");
                }

                if (itemObj["season"] is JArray seasonArray)
                {
                    var validSeasons = new HashSet<string> { "spring", "summer", "fall", "winter" };
                    foreach (var season in seasonArray)
                    {
                        if (!validSeasons.Contains(season.ToString().ToLower()))
                            badSeasons.Add($"{path}:{key}:{season}");
                    }        
                }
            }
        }
        
        await Assert.That(badRealms).IsEmpty().Because("Some patches contained invalid realm names!");
        await Assert.That(badRivers).IsEmpty().Because("Some patches contained invalid bioriver values!");
        await Assert.That(badSeasons).IsEmpty().Because("Some patches contained invalid seasons!");
    }
}
