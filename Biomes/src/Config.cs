using Biomes.RealmGen;
using Biomes.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Vintagestory.API.Common;

namespace Biomes;

public class Configv1
{
    public readonly Dictionary<string, List<string>> BlockPatchBiomes = new(new Fnv1aStringComparer());
    public readonly List<string> EntitySpawnWhiteList = new();
    public readonly Dictionary<string, List<string>> FruitTreeBiomes = new(new Fnv1aStringComparer());
    public readonly Dictionary<string, List<string>> TreeBiomes = new(new Fnv1aStringComparer());
}

public class Configv2
{
    public readonly Dictionary<string, ConfigItem> BlockPatchBiomes = new(new Fnv1aStringComparer());
    public readonly List<string> EntitySpawnWhiteList = new();
    public readonly Dictionary<string, ConfigItem> FruitTreeBiomes = new(new Fnv1aStringComparer());
    public readonly Dictionary<string, ConfigItem> TreeBiomes = new(new Fnv1aStringComparer());
}

internal class OldFormatEnumConverter : JsonConverter<BioRiver>
{
    public override BioRiver ReadJson(
        JsonReader reader,
        Type objectType,
        BioRiver existingValue,
        bool hasExistingValue,
        JsonSerializer serializer
    )
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

public struct ConfigItem()
{
    public List<string> biorealm = [];
    public BioRiver bioriver = BioRiver.Both;

    public BiomeData ToBiomeData(BiomesConfig config)
    {
        var biomeDataValue = 0;
        foreach (var realm in biorealm)
        {
            var mask = 1 << config.ValidRealmIndexes[realm];
            biomeDataValue |= mask;
        }

        var riverMask = bioriver switch
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
    public bool AutoMigrateOldData = true;
    public bool Debug;
    public List<string> EntitySpawnWhiteList = [];

    public RealmGenConfig RealmGenerationConfig = new BlendedRealmConfig
    {
        ChunkHorizontalBlendThreshold = 0.001,
        ChunkLatBlendThreshold = 0.01,
        NorthernRealms = [],
        SouthernRealms = []
    };

    [JsonConverter(typeof(StringEnumConverter))]
    public NoSupportSpawningMode SpawnMode = NoSupportSpawningMode.AllowButWarn;

    // json.net is a stupid library that is bad and sucks
    // it merges lists by default instead of overwriting keys. Why.
    // if you use class defaults like any normal person would expect to work, it creates a new instance then
    // helpfully merges the new defaults with what was parsed and then returns that. On a pure function.
    // What the fuck.
    public static UserConfig DefaultConfig()
    {
        return new UserConfig
        {
            AutoMigrateOldData = true,
            Debug = false,
            EntitySpawnWhiteList = [],
            RealmGenerationConfig = new BlendedRealmConfig
            {
                ChunkHorizontalBlendThreshold = 0.001,
                ChunkLatBlendThreshold = 0.01,
                NorthernRealms = DefaultRealmOrder.Northern,
                SouthernRealms = DefaultRealmOrder.Southern
            },
            SpawnMode = NoSupportSpawningMode.Allow
        };
    }
}

public class BlockConfig
{
    public readonly Dictionary<string, ConfigItem> BlockPatches = new(new Fnv1aStringComparer());
    public readonly Dictionary<string, ConfigItem> FruitTrees = new(new Fnv1aStringComparer());
    public readonly Dictionary<string, ConfigItem> Trees = new(new Fnv1aStringComparer());
}

public class BiomesConfig
{
    public const int MaxValidRealms = BiomeData.SeasonsBitOffset;

    public readonly Dictionary<string, ConfigItem> BlockPatches = new(new Fnv1aStringComparer());
    public readonly Dictionary<string, ConfigItem> FruitTrees = new(new Fnv1aStringComparer());
    public readonly Dictionary<string, ConfigItem> Trees = new(new Fnv1aStringComparer());
    public readonly Dictionary<string, int> ValidRealmIndexes = new(new Fnv1aStringComparer());

    public readonly List<string> Whitelist = [];


    public UserConfig User = new();
    public List<string> ValidRealms = [];

    private void LoadUserConfig(ICoreAPI api)
    {
        User = api.LoadModConfig<UserConfig>("biomes.json");
        User ??= UserConfig.DefaultConfig();

        // TODO: User config needs an overhaul in general, hack to fix a critical issue
        if (User.RealmGenerationConfig is BlendedRealmConfig realmConf)
            // this doesn't make sense as a config and means you likely have an old config
            if (realmConf.NorthernRealms.Count == 0 && realmConf.SouthernRealms.Count == 0)
            {
                realmConf.NorthernRealms = DefaultRealmOrder.Northern;
                realmConf.SouthernRealms = DefaultRealmOrder.Southern;
            }

        api.StoreModConfig(User, "biomes.json");
    }

    public void LoadConfigs(BiomesModSystem mod, ICoreAPI api)
    {
        LoadValidRealms(mod, api);
        LoadLegacyConfigs(mod, api);
        LoadBlockConfigs(mod, api);
        LoadUserConfig(api);
        LoadWhitelist(mod, api);
    }

    private void LoadValidRealms(BiomesModSystem mod, ICoreAPI api)
    {
        var asset = api.Assets.Get($"{mod.Mod.Info.ModID}:config/realms.json").ToText()!;
        ValidRealms = JsonConvert.DeserializeObject<List<string>>(asset)!;
        // Arbitrary magic number picked to give 8 bits of headroom
        if (ValidRealms.Count > MaxValidRealms)
            throw new Exception($"Realms has too many specified realms, must be < {MaxValidRealms}");

        for (var i = 0; i < ValidRealms.Count; i += 1) ValidRealmIndexes[ValidRealms[i]] = i;
    }

    private void LoadLegacyConfigs(BiomesModSystem mod, ICoreAPI api)
    {
        // Version 1 config file format
        foreach (var biomeAsset in api.Assets.GetMany("config/biomes.json"))
        {
            var tmp = JsonConvert.DeserializeObject<Configv1>(biomeAsset.ToText())!;

            foreach (var item in tmp.EntitySpawnWhiteList) Whitelist.Add(item);
            foreach (var item in tmp.TreeBiomes)
                Trees[item.Key] = new ConfigItem { biorealm = item.Value, bioriver = BioRiver.Both };
            foreach (var item in tmp.FruitTreeBiomes)
                FruitTrees[item.Key] = new ConfigItem { biorealm = item.Value, bioriver = BioRiver.Both };
            foreach (var item in tmp.BlockPatchBiomes)
                BlockPatches[item.Key] = new ConfigItem { biorealm = item.Value, bioriver = BioRiver.Both };
        }

        // Version 2 config file format
        foreach (var biomeAsset in api.Assets.GetMany("config/biomes2.json"))
        {
            var tmp = JsonConvert.DeserializeObject<Configv2>(biomeAsset.ToText())!;

            foreach (var item in tmp.EntitySpawnWhiteList) Whitelist.Add(item);
            foreach (var item in tmp.TreeBiomes) Trees[item.Key] = item.Value;
            foreach (var item in tmp.FruitTreeBiomes) FruitTrees[item.Key] = item.Value;
            foreach (var item in tmp.BlockPatchBiomes) BlockPatches[item.Key] = item.Value;
        }
    }

    private void LoadBlockConfigs(BiomesModSystem mod, ICoreAPI api)
    {
        var blockconfigs = api.Assets.GetMany("config/biomes/blockconfig");
        foreach (var blockconfig in blockconfigs)
        {
            var parsed = JsonConvert.DeserializeObject<BlockConfig>(blockconfig.ToText());
            if (parsed == null) continue;
            foreach (var item in parsed.Trees) Trees[item.Key] = item.Value;
            foreach (var item in parsed.FruitTrees) FruitTrees[item.Key] = item.Value;
            foreach (var item in parsed.BlockPatches) BlockPatches[item.Key] = item.Value;
        }
    }

    private void LoadWhitelist(BiomesModSystem mod, ICoreAPI api)
    {
        // There's no real harm in checking all whitelists, mildly slows down whitelist generation phase but there isn't
        // many whitelists and this is only a startup cost
        var folder = api.Assets.GetMany("config/biomes/whitelist/");
        foreach (var whitelistFile in folder)
        {
            var parsed = JsonConvert.DeserializeObject<List<string>>(whitelistFile.ToText());
            if (parsed != null) Whitelist.AddRange(parsed);
        }

        // get single whitelist files now
        var legacy = api.Assets.GetMany("config/biomes/whitelist.json");
        foreach (var whitelistFile in legacy)
        {
            var parsed = JsonConvert.DeserializeObject<List<string>>(whitelistFile.ToText());
            if (parsed != null) Whitelist.AddRange(parsed);
        }
    }
}
