using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;


namespace GptOssHackathonPocs.Core.Services;

public sealed class NwsGeometryResolver
{
    private readonly HttpClient _http;
    private readonly GeoJsonReader _geoJsonReader;
    private readonly GeometryFactory _gf;
    private readonly IMemoryCache _cache;

    public NwsGeometryResolver(HttpClient http, IMemoryCache cache)
    {
        _http = http;
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/geo+json");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "triage-copilot/1.0");
        _geoJsonReader = new GeoJsonReader();
        _gf = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        _cache = cache;
    }

    public async Task<Geometry?> ResolveAsync(JsonElement feature, CancellationToken ct = default)
    {
        // 1) Direct GeoJSON geometry on the alert
        if (feature.TryGetProperty("geometry", out var geomElem) && geomElem.ValueKind != JsonValueKind.Null)
        {
            var geomJson = geomElem.GetRawText();
            
            var ntsGeom = _geoJsonReader.Read<Geometry>(geomJson);
            
            if (!(ntsGeom?.IsEmpty ?? true))
            {
                Console.WriteLine($"nts Geom: {ntsGeom}");
                return ntsGeom;
            }
        }

        // 2) CAP polygon string(s) in properties.parameters.Polygon (lat,lon pairs)
        if (TryBuildFromCapPolygon(feature, out var fromCap))
        {
            Console.WriteLine($"fromCap Geom: {fromCap}");
            return fromCap;
        }

        // 3) Aggregate affectedZones
        if (feature.TryGetProperty("properties", out var props) &&
            props.TryGetProperty("affectedZones", out var zonesElem) &&
            zonesElem.ValueKind == JsonValueKind.Array)
        {
            var parts = new List<Geometry>();
            foreach (var z in zonesElem.EnumerateArray())
            {
                if (z.ValueKind != JsonValueKind.String) continue;
                var url = z.GetString();
                if (string.IsNullOrWhiteSpace(url)) continue;

                var zoneGeom = await GetZoneGeometryAsync(url!, ct);
                if (zoneGeom != null && !zoneGeom.IsEmpty)
                    parts.Add(zoneGeom);
            }

            if (parts.Count == 1)
            {
                Console.WriteLine($"single zone Geom: {parts[0]}");
                return parts[0];

            }

            if (parts.Count > 1)
            {
                var resolveAsync = _gf.BuildGeometry(parts).Union();
                Console.WriteLine($"multi zone Geom: {parts.Count} parts");
                return resolveAsync;
                
            }
        }

        // Nothing found
        return null;
    }

    private bool TryBuildFromCapPolygon(JsonElement feature, out Geometry? geometry)
    {
        geometry = null;
        if (!feature.TryGetProperty("properties", out var props)) return false;
        if (!props.TryGetProperty("parameters", out var parms)) return false;

        // CAP parameter keys sometimes vary in case: "polygon" or "Polygon"
        if (!TryGetCaseInsensitiveArray(parms, "polygon", out var vals) &&
            !TryGetCaseInsensitiveArray(parms, "Polygon", out vals))
            return false;

        // NWS often provides a single string; sometimes an array of strings.
        // Each value is "lat,lon lat,lon ..."
        foreach (var v in vals)
        {
            var s = v.GetString();
            if (string.IsNullOrWhiteSpace(s)) continue;

            var coords = ParseCapLatLonPairs(s!);  // returns lon/lat as NTS Coordinates
            if (coords.Count >= 4) // polygon ring must close; we'll close if needed
            {
                // close ring if not closed
                if (!coords[0].Equals2D(coords[^1])) coords.Add(coords[0]);
                var ring = _gf.CreateLinearRing(coords.ToArray());
                if (!ring.IsValid) continue;
                geometry = _gf.CreatePolygon(ring);
                return true;
            }
        }
        return false;
    }

    private static bool TryGetCaseInsensitiveArray(JsonElement obj, string keyLower, out List<JsonElement> values)
    {
        values = new();
        foreach (var p in obj.EnumerateObject())
        {
            if (!string.Equals(p.Name, keyLower, StringComparison.OrdinalIgnoreCase)) continue;
            if (p.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in p.Value.EnumerateArray()) values.Add(e);
                return values.Count > 0;
            }
            // Some feeds return a single string, not an array
            if (p.Value.ValueKind == JsonValueKind.String)
            {
                values.Add(p.Value);
                return true;
            }
        }
        return false;
    }

    private static List<Coordinate> ParseCapLatLonPairs(string capPolygon)
    {
        var coords = new List<Coordinate>();
        var pairs = capPolygon.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var pair in pairs)
        {
            var parts = pair.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 2) continue;
            if (double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lat) &&
                double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lon))
            {
                // CAP is lat,lon — convert to GeoJSON/NTS order (lon, lat)
                coords.Add(new Coordinate(lon, lat));
            }
        }
        return coords;
    }

    private readonly GeoJsonReader _reader = new();
    private readonly MemoryCacheEntryOptions _zoneTtl = new MemoryCacheEntryOptions()
        .SetAbsoluteExpiration(TimeSpan.FromHours(12))    // hard ceiling
        .SetSlidingExpiration(TimeSpan.FromHours(2))      // stays warm while in use
        .SetSize(1);                                      // 1 "unit" per zone geometry

    private async Task<Geometry?> GetZoneGeometryAsync(string url, CancellationToken ct)
    {
        return await _cache.GetOrCreateAsync(url, async entry =>
        {
            entry.SetOptions(_zoneTtl);

            using var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return null;

            using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            if (!doc.RootElement.TryGetProperty("geometry", out var geomElem) || geomElem.ValueKind == JsonValueKind.Null)
                return null;

            return _reader.Read<Geometry>(geomElem.GetRawText());
        });
    }
}