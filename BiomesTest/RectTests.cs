using Biomes.RealmGen;


namespace BiomesTest;

public class RectTests
{
    [Test]
    public async Task PointInsideWorks()
    {
        var rect = new BlendedRealmGen.Rect(
            new BlendedRealmGen.Point(0.0, 1.0),
            new BlendedRealmGen.Point(1.0, 0.0),
            "doesn't matter"
        );

        var pointThatShouldBeInside = new BlendedRealmGen.Point(0.5, 0.5);
        await Assert.That(rect.PointInside(pointThatShouldBeInside)).IsTrue();

        var pointThatShouldntBeInside = new BlendedRealmGen.Point(1.5, -0.5);
        await Assert.That(rect.PointInside(pointThatShouldntBeInside)).IsFalse();
    }
}
