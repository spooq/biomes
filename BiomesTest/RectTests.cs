using Biomes.RealmGen;

namespace BiomesTest;

public class RectTests
{
    [Fact]
    public void PointInsideWorks()
    {
        var Rect = new BlendedRealmGen.Rect(new BlendedRealmGen.Point(0.0, 1.0), new BlendedRealmGen.Point(1.0, 0.0),
            "doesn't matter");

        var pointThatShouldBeInside = new BlendedRealmGen.Point(0.5, 0.5);
        Assert.True(Rect.PointInside(pointThatShouldBeInside));

        var pointThatShouldntBeInside = new BlendedRealmGen.Point(1.5, -0.5);
        Assert.False(Rect.PointInside(pointThatShouldntBeInside));
    }
}