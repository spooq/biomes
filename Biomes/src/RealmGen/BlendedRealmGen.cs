using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Biomes.RealmGen;

internal class BlendedRealmConfig : RealmGenConfig
{
    public const string TypeKey = "blended";
    public double ChunkHorizontalBlendThreshold = 0.001;
    public double ChunkLatBlendThreshold = 0.01;
    public List<string> NorthernRealms = [];
    public List<string> SouthernRealms = [];
}

internal class BlendedRealmGen : IRealmGen
{
    private readonly List<Rect> _rects = [];

    public BlendedRealmGen(BlendedRealmConfig blendedRealmConfig)
    {
        var index = 0;
        var northernCount = blendedRealmConfig.NorthernRealms.Count;
        var northSlice = 1.0 / northernCount;
        foreach (var northern in blendedRealmConfig.NorthernRealms)
        {
            var xMin = Math.Max(0, index * northSlice - blendedRealmConfig.ChunkHorizontalBlendThreshold);
            var xMax = Math.Min(1.0, (index + 1) * northSlice + blendedRealmConfig.ChunkHorizontalBlendThreshold);
            _rects.Add(
                new Rect(new Point(xMin, 1.0), new Point(xMax, 0 - blendedRealmConfig.ChunkLatBlendThreshold), northern)
            );
            index += 1;
        }

        var southernCount = blendedRealmConfig.SouthernRealms.Count;
        var southSlice = 1.0 / southernCount;
        index = 0;
        foreach (var southern in blendedRealmConfig.SouthernRealms)
        {
            var xMin = Math.Max(0, index * southSlice - blendedRealmConfig.ChunkHorizontalBlendThreshold);
            var xMax = Math.Min(1.0, (index + 1) * southSlice + blendedRealmConfig.ChunkHorizontalBlendThreshold);
            _rects.Add(
                new Rect(
                    new Point(xMin, 0 + blendedRealmConfig.ChunkLatBlendThreshold),
                    new Point(xMax, -1.0),
                    southern
                )
            );
            index += 1;
        }
    }

    public List<string> GetRealmsForBlockPos(ICoreServerAPI api, BlockPos blockPos)
    {
        var latitude = api.World.Calendar.OnGetLatitude(blockPos.Z);
        var farAlong = blockPos.X / (double)api.WorldManager.MapSizeX;
        var point = new Point(farAlong, latitude);
        var numHits = 0;
        List<string> hits = new();
        foreach (var rect in _rects)
            if (rect.PointInside(point))
            {
                hits.Add(rect.realm);
                numHits += 1;
                if (numHits >= 4) break;
            }

        return hits;
    }

    public struct Point(double x, double y)
    {
        public readonly double x = x;
        public readonly double y = y;
    }

    public readonly struct Rect(Point a, Point b, string realm)
    {
        public readonly Point a = a;
        public readonly Point b = b;
        public readonly string realm = realm;

        public bool PointInside(Point p)
        {
            return p.x > a.x && p.y < a.y && p.x < b.x && p.y > b.y;
        }
    }
}
