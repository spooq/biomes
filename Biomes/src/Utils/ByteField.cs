using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Biomes.Utils;

public struct ByteField(byte value) : IEquatable<ByteField>
{
    public byte Value = value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool GetBit(int bit)
    {
        return (Value & (1 << bit)) == 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetBit(int bit, bool value)
    {
        Value = (byte)(value ? Value | (1 << bit) : Value & ~(1 << bit));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ToggleBit(int bit)
    {
        Value = (byte)(Value ^ (1 << bit));
    }

    public override string ToString()
    {
        return Value.ToString();
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return base.Equals(obj);
    }

    public bool Equals(ByteField other)
    {
        return Value == other.Value;
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    public static bool operator ==(ByteField left, ByteField right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ByteField left, ByteField right)
    {
        return !(left == right);
    }
}