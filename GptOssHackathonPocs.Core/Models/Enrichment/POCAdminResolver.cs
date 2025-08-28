using NetTopologySuite.Geometries;

namespace GptOssHackathonPocs.Core.Models.Enrichment;

public sealed class POCAdminResolver : IAdminResolver
{
    public string[] ResolveAdminAreas(Geometry? g)
    {
        if (g is null || g.IsEmpty) return [];
        var c = g.Centroid;
        return [$"Lat{c.Y:0.00}_Lon{c.X:0.00}"];
    }
}
