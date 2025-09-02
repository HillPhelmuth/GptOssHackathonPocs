using System.Text.Json;
using NetTopologySuite.Geometries;

namespace GptOssHackathonPocs.Core.Models.Enrichment;

/// <summary>
/// HIFLD Hospitals FeatureServer client. Returns facility strings like
/// "Ben Taub Hospital (Houston, TX) — General Acute Care, beds: 496".
/// Default layer points at the HIFLD Hospitals (layer 0).
/// </summary>
public sealed class FacilityIndexHospitals(
    HttpClient http,
    string layerUrl =
        "https://services2.arcgis.com/FiaPA4ga0iQKduv3/arcgis/rest/services/Hospitals/FeatureServer/0")
    : IFacilityIndex
{
    private readonly string _layerUrl = layerUrl.TrimEnd('/');

    public async Task<NearbyHospital[]> NearbyFacilities(Geometry? g)
        => await NearbyFacilitiesAsync(g, CancellationToken.None);
    //ToDo NOT WORKING
    public async Task<NearbyHospital[]> NearbyFacilitiesAsync(Geometry? g, CancellationToken ct)
    {
        if (g is null || g.IsEmpty) throw new Exception("Geometry is null");

        var env = g.EnvelopeInternal;

        // Expand the envelope by ~10 miles in all directions while keeping the same geometry object shape
        double milesBuffer = 10.0;
        var hospitalsWithMilesBuffer = await GetHospitalsWithMilesBuffer(milesBuffer, env, ct);
        while (!hospitalsWithMilesBuffer.Any() && milesBuffer < 200.0)
        {
            milesBuffer *= 2.0;
            hospitalsWithMilesBuffer = await GetHospitalsWithMilesBuffer(milesBuffer, env, ct);
        }
        return hospitalsWithMilesBuffer.ToArray();

        // Optionally clamp to valid WGS84 bounds
    }

    private async Task<IEnumerable<NearbyHospital>> GetHospitalsWithMilesBuffer(double milesBuffer, Envelope env, CancellationToken ct)
    {
        const double metersPerMile = 1609.344;
        var radiusMeters = milesBuffer * metersPerMile;

        // Approximate meters per degree at the envelope's center latitude
        var centerLat = (env.MinY + env.MaxY) / 2.0;
        const double metersPerDegLat = 111_320.0; // average
        var metersPerDegLon = metersPerDegLat * Math.Cos(centerLat * Math.PI / 180.0);
        if (metersPerDegLon <= 0) metersPerDegLon = 1e-9; // guard near poles

        var dLat = radiusMeters / metersPerDegLat;
        var dLon = radiusMeters / metersPerDegLon;

        var xmin = Clamp(env.MinX - dLon, -180.0, 180.0);
        var xmax = Clamp(env.MaxX + dLon, -180.0, 180.0);
        var ymin = Clamp(env.MinY - dLat, -90.0, 90.0);
        var ymax = Clamp(env.MaxY + dLat, -90.0, 90.0);
        
            
        var geometryObj = new
        {
            xmin,
            ymin,
            xmax,
            ymax,
            spatialReference = new { wkid = 4326 }
        };
        var geometryJson = JsonSerializer.Serialize(geometryObj);
        Console.WriteLine($"Expanded Geometry: {geometryJson}");
        var qs = new Dictionary<string, string>
        {
            ["f"] = "json",
            ["where"] = "1=1",
            ["geometryType"] = "esriGeometryEnvelope",
            ["geometry"] = geometryJson,
            ["inSR"] = "4326",
            ["spatialRel"] = "esriSpatialRelIntersects",
            ["returnGeometry"] = "false",
            ["outFields"] = "NAME,CITY,STATE,BEDS,TYPE,WEBSITE,LATITUDE,LONGITUDE",
            ["resultRecordCount"] = "2000"
        };

        var url = $"{_layerUrl}/query?{BuildQuery(qs)}";
        Console.WriteLine($"Facility Query: {url}");
        using var resp = await http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();

        var readAsStreamAsync = await resp.Content.ReadAsStringAsync(ct);
        Console.WriteLine($"Facility Response: {readAsStreamAsync}");
        using var doc = JsonDocument.Parse(readAsStreamAsync);

        if (!doc.RootElement.TryGetProperty("features", out var feats) || feats.GetArrayLength() == 0)
            return [];

        var list = new List<NearbyHospital>();
        foreach (var f in feats.EnumerateArray())
        {
            var a = f.GetProperty("attributes");
            var name = GetStr(a, "NAME") ?? "Hospital";
            var city = GetStr(a, "CITY") ?? string.Empty;
            var state = GetStr(a, "STATE") ?? string.Empty;
            var type = GetStr(a, "TYPE") ?? string.Empty;
            var website = GetStr(a, "WEBSITE") ?? string.Empty;

            int beds = 0;
            if (a.TryGetProperty("BEDS", out var bd) && bd.ValueKind == JsonValueKind.Number)
            {
                try { beds = bd.GetInt32(); }
                catch { beds = (int)Math.Round(bd.GetDouble()); }
            }

            double latitude = 0, longitude = 0;
            if (a.TryGetProperty("LATITUDE", out var lat) && lat.ValueKind == JsonValueKind.Number)
                latitude = lat.GetDouble();
            if (a.TryGetProperty("LONGITUDE", out var lon) && lon.ValueKind == JsonValueKind.Number)
                longitude = lon.GetDouble();

            list.Add(new NearbyHospital
            {
                Name = name,
                City = city,
                State = state,
                Beds = beds,
                Type = type,
                Website = website,
                Latitude = latitude,
                Longitude = longitude
            });
        }
        return list.ToArray();

        static double Clamp(double v, double min, double max)
        {
            // Temp Try without clamp
            return v;
            return v < min ? min : (v > max ? max : v);
        }
    }

    private static string? GetStr(JsonElement attrs, string key)
        => attrs.TryGetProperty(key, out var v) && v.ValueKind != JsonValueKind.Null ? v.ToString() : null;

    private static string BuildQuery(Dictionary<string, string> kv)
        => string.Join("&", kv.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
}

public class NearbyHospital
{
    public string Name { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    public int Beds { get; set; }
    public string Type { get; set; }
    public string Website { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public override string ToString()
        => $"{Name} ({City}, {State}) — {Type}" + (Beds > 0 ? $", beds: {Beds}" : "") + (string.IsNullOrWhiteSpace(Website) ? "" : $", website: {Website}");
}