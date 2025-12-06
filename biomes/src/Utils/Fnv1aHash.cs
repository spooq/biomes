using System.Runtime.CompilerServices;
using Vintagestory.API.Common;

namespace Biomes.Utils;

// Public domain very fast hashing algorithm for strings when cryptographic security nor hashdos is a concern

// Fast hashing for code lookup
public static class Fnv1aHash
{
    private const uint FnvPrime = 16777619;
    private const uint EmptyHash = 2166136261;

    public static int HashString(string s)
    {
        unchecked
        {
            var hash = EmptyHash;
            foreach (var c in s)
            {
                hash ^= c;
                hash *= FnvPrime;
            }

            return Unsafe.As<uint, int>(ref hash);
        }
    }
}

public class Fnv1aStringComparer : IEqualityComparer<string>
{
    public bool Equals(string? x, string? y)
    {
        return string.Equals(x, y);
    }

    public int GetHashCode(string obj)
    {
        return Fnv1aHash.HashString(obj);
    }
}

public class Fnv1aAssetLocationComparer : IEqualityComparer<AssetLocation>
{
    public bool Equals(AssetLocation? x, AssetLocation? y)
    {
        return AssetLocation.Equals(x, y);
    }

    public int GetHashCode(AssetLocation obj)
    {
        return Fnv1aHash.HashString(obj.Domain) ^ Fnv1aHash.HashString(obj.Path);
    }
}