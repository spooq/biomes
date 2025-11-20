using Vintagestory.API.Common;

namespace Biomes.util;

internal static class ModProperty
{
    public static EnumCommandStatus Get<T>(IMapChunk? chunk, string name, ref T value)
    {
        if (chunk == null)
            return EnumCommandStatus.Error;
        
        value = chunk.GetModdata<T>(name);
        return value == null ? EnumCommandStatus.Error : EnumCommandStatus.Success;
    }

    public static EnumCommandStatus Get<T>(Caller caller, string name, ref T value)
    {
        var chunk = caller.Entity.World.BlockAccessor.GetMapChunkAtBlockPos(caller.Entity.Pos.AsBlockPos);
        return Get(chunk, name, ref value);
    }

    public static EnumCommandStatus Set<T>(IMapChunk? chunk, string name, ref T value)
    {
        if (chunk == null)
            return EnumCommandStatus.Error;
        
        chunk.SetModdata(name, value);
        chunk.MarkDirty();
        return EnumCommandStatus.Success;
    }
    
    public static EnumCommandStatus Set(Caller caller, string name, object value)
    {
        var chunk = caller.Entity.World.BlockAccessor.GetMapChunkAtBlockPos(caller.Entity.Pos.AsBlockPos);
        return Set(chunk, name, ref value);
    }
}