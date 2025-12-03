using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Vintagestory.API.Common;

namespace Biomes;

public class Configv1
{
    public readonly Dictionary<string, List<string>> BlockPatchBiomes = new();
    public readonly List<string> EntitySpawnWhiteList = new();
    public readonly Dictionary<string, List<string>> FruitTreeBiomes = new();
    public readonly Dictionary<string, List<string>> TreeBiomes = new();
}

public class Configv2
{
    public readonly Dictionary<string, ConfigItem> BlockPatchBiomes = new();
    public readonly List<string> EntitySpawnWhiteList = new();
    public readonly Dictionary<string, ConfigItem> FruitTreeBiomes = new();
    public readonly Dictionary<string, ConfigItem> TreeBiomes = new();
}

internal class OldFormatEnumConverter : JsonConverter<BioRiver>
{
    public override BioRiver ReadJson(JsonReader reader, Type objectType, BioRiver existingValue, bool hasExistingValue,
        JsonSerializer serializer)
    {
        var value = reader.Value?.ToString()?.ToLowerInvariant();
        return value switch
        {
            "false" or "noriver" => BioRiver.NoRiver,
            "both" => BioRiver.Both,
            "true" or "riveronly" => BioRiver.RiverOnly,
            _ => throw new JsonSerializationException($"Unknown bioriver value: {value}")
        };
    }

    public override void WriteJson(JsonWriter writer, BioRiver value, JsonSerializer serializer)
    {
        writer.WriteValue(value.ToString());
    }
}

[JsonConverter(typeof(OldFormatEnumConverter))]
public enum BioRiver
{
    NoRiver,
    Both,
    RiverOnly
}

public struct ConfigItem
{
    public List<string> biorealm = [];
    public BioRiver river = BioRiver.Both;

    public ConfigItem()
    {
    }
}

public class RealmsConfig(List<string> northernRealms, List<string> southernRealms)
{
    public readonly List<string> NorthernRealms = northernRealms;
    public readonly List<string> SouthernRealms = southernRealms;

    public List<string> AllRealms()
    {
        return NorthernRealms.Union(SouthernRealms).ToList();
    }

    public RealmsConfig Flipped()
    {
        return new RealmsConfig(SouthernRealms, NorthernRealms);
    }
}

public enum NoSupportSpawningMode
{
    Allow,
    AllowButWarn,
    DenyButWarn,
    Deny
}

public static class SpawningModeMethods
{
    public static bool ShouldWarn(this NoSupportSpawningMode mode)
    {
        return mode switch
        {
            NoSupportSpawningMode.DenyButWarn or NoSupportSpawningMode.AllowButWarn => true,
            _ => false
        };
    }

    public static bool ShouldAllowByDefaut(this NoSupportSpawningMode mode)
    {
        return mode switch
        {
            NoSupportSpawningMode.Allow or NoSupportSpawningMode.AllowButWarn => true,
            _ => false
        };
    }
}

public class UserConfig
{
    public readonly bool Debug = false;
    public readonly List<string> EntitySpawnWhiteList = [];
    public readonly bool FlipNorthSouth = false;
    [JsonConverter(typeof(StringEnumConverter))]
    public readonly NoSupportSpawningMode SpawnMode = NoSupportSpawningMode.AllowButWarn;
}

public class BiomesConfig
{
    public Dictionary<string, ConfigItem> BlockPatches = [];
    public Dictionary<string, ConfigItem> FruitTrees = [];

    public RealmsConfig Realms = new([], []);
    public Dictionary<string, ConfigItem> Trees = [];
    public UserConfig User = new();

    public List<string> Whitelist = [];

    private void LoadUserConfig(ICoreAPI api)
    {
        User = api.LoadModConfig<UserConfig>("biomes.json");
        User ??= new UserConfig();
        api.StoreModConfig(User, "biomes.json");

        if (User.FlipNorthSouth) Realms = Realms.Flipped();
    }

    public void LoadConfigs(BiomesModSystem mod, ICoreAPI api)
    {
        LoadRealms(mod, api);
        LoadLegacyConfigs(mod, api);
        LoadUserConfig(api);
        LoadWhitelist(mod, api);
    }

    public void LoadRealms(BiomesModSystem mod, ICoreAPI api)
    {
        var asset = api.Assets.Get($"{mod.Mod.Info.ModID}:config/realms.json").ToText()!;
        Realms = JsonConvert.DeserializeObject<RealmsConfig>(asset)!;
    }

    public void LoadLegacyConfigs(BiomesModSystem mod, ICoreAPI api)
    {
        // Version 1 config file format
        foreach (var biomeAsset in api.Assets.GetMany("config/biomes.json"))
        {
            var tmp = JsonConvert.DeserializeObject<Configv1>(biomeAsset.ToText())!;

            foreach (var item in tmp.EntitySpawnWhiteList)
                Whitelist.Add(item);
            foreach (var item in tmp.TreeBiomes)
                Trees[item.Key] = new ConfigItem { biorealm = item.Value, river = BioRiver.Both };
            foreach (var item in tmp.FruitTreeBiomes)
                FruitTrees[item.Key] = new ConfigItem { biorealm = item.Value, river = BioRiver.Both };
            foreach (var item in tmp.BlockPatchBiomes)
                BlockPatches[item.Key] = new ConfigItem { biorealm = item.Value, river = BioRiver.Both };
        }

        // Version 2 config file format
        foreach (var biomeAsset in api.Assets.GetMany("config/biomes2.json"))
        {
            var tmp = JsonConvert.DeserializeObject<Configv2>(biomeAsset.ToText())!;

            foreach (var item in tmp.EntitySpawnWhiteList)
                Whitelist.Add(item);
            foreach (var item in tmp.TreeBiomes)
                Trees[item.Key] = item.Value;
            foreach (var item in tmp.FruitTreeBiomes)
                FruitTrees[item.Key] = item.Value;
            foreach (var item in tmp.BlockPatchBiomes)
                BlockPatches[item.Key] = item.Value;
        }
    }

    private void LoadWhitelist(BiomesModSystem mod, ICoreAPI api)
    {
        var assets = api.Assets.GetMany("config/whitelist.json");
        foreach (var whitelistFile in assets)
        {
            var parsed = JsonConvert.DeserializeObject<List<string>>(whitelistFile.ToText());
            if (parsed != null) Whitelist.AddRange(parsed);
        }
    }
}