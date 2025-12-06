using Biomes.RealmGen;
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

public static class BioRiverExtensions
{
    public static BioRiver FromString(string value)
    {
        var lowercase = value.ToLowerInvariant();
        return lowercase switch
        {
            "false" or "noriver" => BioRiver.NoRiver,
            "both" => BioRiver.Both,
            "true" or "riveronly" => BioRiver.RiverOnly,
            _ => throw new JsonSerializationException($"Unknown bioriver value: {value}")
        };
    }
}

public struct ConfigItem
{
    public List<string> biorealm = [];
    public BioRiver river = BioRiver.Both;

    public ConfigItem()
    {
    }

    public BiomeData ToBiomeData(BiomesConfig config)
    {
        var biomeDataValue = 0;
        foreach (var realm in biorealm)
        {
            var mask = 1 << config.ValidRealmIndexes[realm];
            biomeDataValue |= mask;
        }

        var riverMask = river switch
        {
            BioRiver.NoRiver => BiomeData.NoRiverMask,
            BioRiver.Both => BiomeData.BothMask,
            BioRiver.RiverOnly => BiomeData.RiverMask,
            _ => throw new ArgumentOutOfRangeException("What?")
        };
        biomeDataValue |= riverMask;

        return new BiomeData(biomeDataValue);
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
    public readonly bool AutoMigrateOldData = true;
    public readonly bool Debug = false;
    public readonly List<string> EntitySpawnWhiteList = [];

    public readonly RealmGenConfig RealmGenerationConfig = new BlendedRealmConfig
    {
        ChunkHorizontalBlendThreshold = 0.001,
        ChunkLatBlendThreshold = 0.01,
        NorthernRealms = DefaultRealmOrder.Northern,
        SouthernRealms = DefaultRealmOrder.Southern
    };

    [JsonConverter(typeof(StringEnumConverter))]
    public readonly NoSupportSpawningMode SpawnMode = NoSupportSpawningMode.AllowButWarn;
}

public class BiomesConfig
{
    public const int MaxValidRealms = BiomeData.SeasonsBitOffset;

    public Dictionary<string, ConfigItem> BlockPatches = [];
    public Dictionary<string, ConfigItem> FruitTrees = [];

    public Dictionary<string, ConfigItem> Trees = [];


    public UserConfig User = new();
    public Dictionary<string, int> ValidRealmIndexes = new();
    public List<string> ValidRealms = [];

    public List<string> Whitelist = [];

    private void LoadUserConfig(ICoreAPI api)
    {
        User = api.LoadModConfig<UserConfig>("biomes.json");
        User ??= new UserConfig();
        api.StoreModConfig(User, "biomes.json");
    }

    public void LoadConfigs(BiomesModSystem mod, ICoreAPI api)
    {
        LoadValidRealms(mod, api);
        LoadLegacyConfigs(mod, api);
        LoadUserConfig(api);
        LoadWhitelist(mod, api);
    }

    public void LoadValidRealms(BiomesModSystem mod, ICoreAPI api)
    {
        var asset = api.Assets.Get($"{mod.Mod.Info.ModID}:config/realms.json").ToText()!;
        ValidRealms = JsonConvert.DeserializeObject<List<string>>(asset)!;
        // Arbitrary magic number picked to give 8 bits of headroom
        if (ValidRealms.Count > MaxValidRealms)
            throw new Exception($"Realms has too many specified realms, must be < {MaxValidRealms}");

        for (var i = 0; i < ValidRealms.Count; i += 1) ValidRealmIndexes[ValidRealms[i]] = i;
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