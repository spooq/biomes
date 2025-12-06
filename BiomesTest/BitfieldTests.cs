using Biomes.Utils;

namespace BiomesTest;

public class BitfieldTests
{
    [Fact]
    public void GetsBackSetValues()
    {
        var field = new ByteField();
        Assert.Equal(new ByteField(0), field);
        for (var i = 0; i < 8; i++) field.SetBit(i, true);
        Assert.Equal(new ByteField(0b1111_1111), field);
        for (var i = 0; i < 8; i += 2) field.SetBit(i, false);
        Assert.Equal(new ByteField(0b1010_1010), field);
        Assert.NotEqual(new ByteField(0b0101_0101), field);
    }

    [Fact]
    public void ToggleWorksCorrectly()
    {
        var beforeFlip = new ByteField(0b1010_1010);
        var expectedAfterFlip = new ByteField(0b0101_0101);
        var toFlip = new ByteField(0b1010_1010);
        // flip once
        for (var i = 0; i < 8; i++) toFlip.ToggleBit(i);
        Assert.Equal(expectedAfterFlip, toFlip);
        for (var i = 0; i < 8; i++) toFlip.ToggleBit(i);
        Assert.Equal(beforeFlip, toFlip);
    }
}