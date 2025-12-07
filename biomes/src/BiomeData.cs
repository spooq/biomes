using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using ProtoBuf;
using Vintagestory.API.Common;

namespace Biomes;

[ProtoContract]
public struct BiomeDataSurrogate
{
    [ProtoMember(1)] public int Data { get; set; }

    public static implicit operator BiomeDataSurrogate(BiomeData value)
    {
        return new BiomeDataSurrogate { Data = value.Value.Data };
    }

    public static implicit operator BiomeData(BiomeDataSurrogate surrogate)
    {
        return new BiomeData(surrogate.Data);
    }
}

public struct BiomeData(int initialValue)
{
    // mask which corresponds to all possible regions set if you need to mask it out
    // realm data occupies bits 0-15
    // realms aren't manually specified because they're given indexes internally
    public const int AllRealmsMask = 0b00000000_00000000_11111111_11111111;

    public const int SeasonsBitOffset = 16;

    // mask which corresponds to all set seasons, season data occupies bits 16-19
    public const int AllSeasonsMask = 0b00000000_00001111_00000000_00000000;
    public const int SpringMask = 1 << (SeasonsBitOffset + (int)EnumSeason.Spring);
    public const int SummerMask = 1 << (SeasonsBitOffset + (int)EnumSeason.Summer);
    public const int FallMask = 1 << (SeasonsBitOffset + (int)EnumSeason.Fall);
    public const int WinterMask = 1 << (SeasonsBitOffset + (int)EnumSeason.Winter);

    // this is two seperate fields despite seeming like opposite values as a trick for
    // things configured to spawn both in rivers and not in rivers. ie, they can have the both mask set
    // and then a single operation of these two bits in the chunk can be & to get whether the spawn is valid or not
    public const int BothMask = 0b00000000_00110000_00000000_00000000;
    public const int NoRiverMask = 1 << 20;
    public const int RiverMask = 1 << 21;
    // remaining fields 22-31 unoccupied

    public BitVector32 Value = new(initialValue);

    public static int SeasonStringToMask(string season)
    {
        var lowercased = season.ToLowerInvariant();
        return lowercased switch
        {
            "spring" => SpringMask,
            "summer" => SummerMask,
            "fall" => FallMask,
            "winter" => WinterMask,
            _ => throw new ArgumentOutOfRangeException(nameof(season))
        };
    }

    public static int SeasonEnumToMask(EnumSeason season)
    {
        return season switch
        {
            EnumSeason.Spring => SpringMask,
            EnumSeason.Summer => SummerMask,
            EnumSeason.Fall => FallMask,
            EnumSeason.Winter => WinterMask,
            _ => throw new ArgumentOutOfRangeException(nameof(season), season, null)
        };
    }

    public bool IsNullData()
    {
        return Value.Data == new BitVector32(0).Data;
    }

    public bool CheckAgainst(BiomeData biomeData)
    {
        return (Value.Data & biomeData.Value.Data) != 0;
    }

    public bool CheckRealmAndRiverAgainst(BiomeData biomeData)
    {
        var ourNoSeasons = Value.Data & ~AllSeasonsMask;
        var theirsNoSeasons = biomeData.Value.Data & ~AllSeasonsMask;

        return (ourNoSeasons & theirsNoSeasons) != 0;
    }


    public void SetRealm(int realmId, bool value)
    {
        var mask = 1 << realmId;
        Value[mask] = value;
    }

    public bool GetRealm(int realmId)
    {
        var mask = 1 << realmId;
        return Value[mask];
    }

    public void SetAllSeasons(bool value)
    {
        SetSeason(EnumSeason.Spring, value);
        SetSeason(EnumSeason.Summer, value);
        SetSeason(EnumSeason.Fall, value);
        SetSeason(EnumSeason.Winter, value);
    }

    public void SetSeason(EnumSeason season, bool value)
    {
        var mask = SeasonEnumToMask(season);
        Value[mask] = value;
    }

    public void SetSeason(string season, bool value)
    {
        var mask = SeasonStringToMask(season);
        Value[mask] = value;
    }

    public bool GetSeason(EnumSeason season)
    {
        var mask = SeasonEnumToMask(season);
        return Value[mask];
    }

    public void SetRiver(bool hasRiver)
    {
        Value[RiverMask] = hasRiver;
    }

    public bool GetRiver()
    {
        return Value[RiverMask];
    }

    public void SetNoRiver(bool noRiver)
    {
        Value[NoRiverMask] = noRiver;
    }

    public bool GetNoRiver()
    {
        return Value[NoRiverMask];
    }

    public void SetFromBioRiver(BioRiver bioRiver)
    {
        switch (bioRiver)
        {
            case BioRiver.NoRiver:
                SetNoRiver(true);
                break;
            case BioRiver.Both:
                SetNoRiver(true);
                SetRiver(true);
                break;
            case BioRiver.RiverOnly:
                SetRiver(true);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(bioRiver), bioRiver, null);
        }
    }

    public bool IsAllSeason()
    {
        return (Value.Data & AllSeasonsMask) == AllSeasonsMask;
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var currentValue = Value.Data;
            var reinterp = Unsafe.As<int, uint>(ref currentValue);
            var result = reinterp * 2654435761;
            return Unsafe.As<uint, int>(ref result);
        }
    }

    public List<string> RealmNames(BiomesConfig config)
    {
        List<string> outList = [];

        foreach (var (name, index) in config.ValidRealmIndexes)
            if (GetRealm(index))
                outList.Add(name);

        return outList;
    }
}