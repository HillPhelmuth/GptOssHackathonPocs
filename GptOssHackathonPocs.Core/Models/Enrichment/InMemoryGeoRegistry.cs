using NetTopologySuite;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NetTopologySuite.Operation.Union;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json;

namespace GptOssHackathonPocs.Core.Models.Enrichment;

/// <summary>
/// Robust registry: accepts Feature / FeatureCollection / GeometryCollection / Geometry,
/// normalizes to a single NTS Geometry (SRID 4326), and stores a canonical geometry JSON.
/// </summary>
public sealed class InMemoryGeoRegistry : IGeoRegistry
{
    private readonly ConcurrentDictionary<string, string> _geomJsonByKey = new();
    private readonly ConcurrentDictionary<string, Geometry> _geomByKey = new();

    private readonly GeometryFactory _gf;
    private readonly GeoJsonReader _reader;   // reads geometry JSON only
    private readonly GeoJsonWriter _writer;   // writes geometry JSON only

    public InMemoryGeoRegistry()
    {
        _gf = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        _reader = new GeoJsonReader(_gf, new JsonSerializerSettings());
        _writer = new GeoJsonWriter();
    }

    public string RegisterGeoJson(string geojson)
    {
        var geom = NormalizeToGeometry(geojson);
        if (geom is null || geom.IsEmpty)
            throw new ArgumentException("Invalid or empty GeoJSON.", nameof(geojson));

        // Canonicalize by writing back geometry-only JSON (stable ordering/precision).
        var geomJson = _writer.Write(geom);
        var key = "geom:" + Sha256Hex(geomJson).Substring(0, 16).ToLowerInvariant();

        _geomJsonByKey[key] = geomJson;
        _geomByKey[key] = geom;
        return key;
    }

    public string? GetGeoJson(string geometry_ref)
    {
        // Return a Feature wrapper so front-ends can draw it directly.
        if (_geomJsonByKey.TryGetValue(geometry_ref, out var geomJson))
            return $"{{\"type\":\"Feature\",\"geometry\":{geomJson},\"properties\":{{}}}}";
        return null;
    }

    public Geometry? GetGeometry(string geometry_ref) =>
        _geomByKey.GetValueOrDefault(geometry_ref);

    // -------- internals --------

    private Geometry? NormalizeToGeometry(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // If it looks like a bare geometry (no "type" or not a string), try read directly.
        if (!root.TryGetProperty("type", out var tEl) || tEl.ValueKind != JsonValueKind.String)
        {
            try { return _reader.Read<Geometry>(json); } catch { return null; }
        }

        var t = tEl.GetString()?.ToLowerInvariant();
        switch (t)
        {
            case "feature":
                if (root.TryGetProperty("geometry", out var gEl) && gEl.ValueKind != JsonValueKind.Null)
                    return NormalizeToGeometry(gEl.GetRawText());
                return null;

            case "featurecollection":
                if (!root.TryGetProperty("features", out var feats) || feats.ValueKind != JsonValueKind.Array)
                    return null;
                var partsFC = new List<Geometry>();
                foreach (var f in feats.EnumerateArray())
                {
                    if (f.TryGetProperty("geometry", out var fg) && fg.ValueKind != JsonValueKind.Null)
                    {
                        var pg = NormalizeToGeometry(fg.GetRawText());
                        if (pg is { IsEmpty: false }) partsFC.Add(pg);
                    }
                }
                return Union(partsFC);

            case "geometrycollection":
                if (!root.TryGetProperty("geometries", out var gs) || gs.ValueKind != JsonValueKind.Array)
                    return null;
                var partsGC = new List<Geometry>();
                foreach (var ge in gs.EnumerateArray())
                {
                    var pg = NormalizeToGeometry(ge.GetRawText());
                    if (pg is { IsEmpty: false }) partsGC.Add(pg);
                }
                return Union(partsGC);

            // Polygon / MultiPolygon / LineString / Point, etc.
            default:
                try { return _reader.Read<Geometry>(json); } catch { return null; }
        }
    }

    private Geometry? Union(List<Geometry> parts)
    {
        if (parts.Count == 0) return null;
        if (parts.Count == 1) return parts[0];
        // UnaryUnion handles mixed types and is robust for large sets.
        var u = UnaryUnionOp.Union(parts);
        // Ensure SRID
        if (u != null && u.SRID == 0) u.SRID = 4326;
        return u;
    }

    private static string Sha256Hex(string s)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("X2"));
        return sb.ToString();
    }
}

