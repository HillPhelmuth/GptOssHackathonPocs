using NetTopologySuite.Geometries;

namespace GptOssHackathonPocs.Core.Models.Enrichment;

public static class GeometryHelper
{
    // Very rough buffer for point geometries by radius in kilometers (WGS84 ~ 1 deg ≈ 111 km).
    public static Geometry? BufferPoint(Geometry? g, double radiusKm)
    {
        if (g is null || g.IsEmpty) return g;
        var deg = radiusKm / 111.0;
        return g.Buffer(deg);
    }
}
