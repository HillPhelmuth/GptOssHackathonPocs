using NetTopologySuite.Geometries;

namespace GptOssHackathonPocs.Core.Models.Enrichment;

public interface IGeoRegistry
{
    string RegisterGeoJson(string geojson);  // returns geometry_ref
    string? GetGeoJson(string geometry_ref);
    Geometry? GetGeometry(string geometry_ref);
}
