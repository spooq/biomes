using Newtonsoft.Json;

namespace Biomes.Api;

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
