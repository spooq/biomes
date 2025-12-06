using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Biomes.RealmGen;

public static class DefaultRealmOrder
{
    public static readonly List<string> Northern =
    [
        "pacific nearctic",
        "atlantic nearctic",
        "atlantic palearctic",
        "central palearctic",
        "eastern palearctic",
        "pacific palearctic"
    ];

    public static readonly List<string> Southern =
    [
        "pacific neotropic",
        "atlantic neotropic",
        "atlantic afrotropic",
        "eastern afrotropic",
        "australasian",
        "oceanic"
    ];
}

// this is required to not result in an infinite recursion when reading, json.net sucks
public class NoAttributeConvertersContractResolver : DefaultContractResolver
{
    protected override JsonContract CreateContract(Type objectType)
    {
        var contract = base.CreateContract(objectType);
        contract.Converter = null;
        return contract;
    }

    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
    {
        var prop = base.CreateProperty(member, memberSerialization);
        prop.Converter = null;
        return prop;
    }
}

[JsonConverter(typeof(RealmGenConfigConverter))]
public abstract class RealmGenConfig;

public class RealmGenConfigConverter : JsonConverter<RealmGenConfig>
{
    public override void WriteJson(JsonWriter writer, RealmGenConfig? value, JsonSerializer serializer)
    {
        var resolver = new NoAttributeConvertersContractResolver();
        var clearedSerializer = new JsonSerializer
        {
            ContractResolver = resolver
        };
        // Also clear any global converters just in case:
        serializer.Converters.Clear();

        var obj = JObject.FromObject(value, clearedSerializer);

        var type = value switch
        {
            ClassicGenConfig => ClassicGenConfig.TypeKey,
            BlendedRealmConfig => BlendedRealmConfig.TypeKey,
            _ => throw new JsonSerializationException($"Unknown shape type {value.GetType().Name}")
        };

        obj["type"] = type;

        obj.WriteTo(writer);
    }

    public override RealmGenConfig ReadJson(JsonReader reader, Type objectType, RealmGenConfig? existingValue,
        bool hasExistingValue,
        JsonSerializer serializer)
    {
        var obj = JObject.Load(reader);
        var type = obj["type"]?.Value<string>()?.ToLowerInvariant();
        RealmGenConfig result = type switch
        {
            ClassicGenConfig.TypeKey => new ClassicGenConfig(),
            BlendedRealmConfig.TypeKey => new BlendedRealmConfig(),
            _ => throw new JsonSerializationException($"Unknown shape type '{type}'")
        };

        serializer.Populate(obj.CreateReader(), result);

        return result;
    }
}

public interface IRealmGen
{
    public List<string> GetRealmsForBlockPos(ICoreServerAPI api, BlockPos blockPos);

    public static IRealmGen BuildGenerator(BiomesConfig config)
    {
        return config.User.RealmGenerationConfig switch
        {
            BlendedRealmConfig blendedRealmConfig => new BlendedRealmGen(blendedRealmConfig),
            ClassicGenConfig classicGenConfig => new ClassicRealmGen(classicGenConfig),
            _ => throw new ArgumentOutOfRangeException(nameof(config))
        };
    }
}