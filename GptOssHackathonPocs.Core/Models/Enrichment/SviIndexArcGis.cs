using System.Text.Json;
using NetTopologySuite.Geometries;

namespace GptOssHackathonPocs.Core.Models.Enrichment;

/// <summary>
/// CDC/ATSDR SVI FeatureServer client; returns overall SVI percentile (RPL_THEMES in 0..1).
/// Default layer points at SVI 2020 (tracts). Swap the URL for 2022 when you like.
/// Examples:
///  - 2020 tracts: https://services2.arcgis.com/Eb8y2VjBgkqbQJwk/arcgis/rest/services/CDC_SVI2020/FeatureServer/0
///  - 2022 (archive): https://services2.arcgis.com/FiaPA4ga0iQKduv3/arcgis/rest/services/CDC_SVI_2022_%28Archive%29/FeatureServer/2
/// </summary>
public sealed class SviIndexArcGis : ISviIndex
{
    private readonly HttpClient _http;
    private readonly string _layerUrl;
    private readonly string _field;

    public SviIndexArcGis(HttpClient http,
        string layerUrl =
            "https://services2.arcgis.com/FiaPA4ga0iQKduv3/arcgis/rest/services/CDC_SVI_2022_(Archive)/FeatureServer/2",
        string field = "RPL_THEMES")
    {
        _http = http;
        _layerUrl = layerUrl.TrimEnd('/');
        _field = field;
    }

    public async Task<PopulationSvi> GetSviPercentile(Geometry? g)
        => await GetPercentileAsync(g, CancellationToken.None);

    public async Task<PopulationSvi> GetPercentileAsync(Geometry? g, CancellationToken ct)
    {
        if (g is null || g.IsEmpty) throw new Exception("Geometry is FUCKING null");

        var env = g.EnvelopeInternal;
        var geometryObj = new
        {
            xmin = env.MinX,
            ymin = env.MinY,
            xmax = env.MaxX,
            ymax = env.MaxY,
            spatialReference = new { wkid = 4326 }
        };
        var geometryJson = JsonSerializer.Serialize(geometryObj);

        // 1) Try server-side stats (avg + max) E_TOTPOP
        //var stats = new[] {
        //    new { statisticType = "avg", onStatisticField = "RPL_THEMES", outStatisticFieldName = "avg" },
        //    new { statisticType = "max", onStatisticField = "RPL_THEMES", outStatisticFieldName = "max" },
        //    new { statisticType = "tot", onStatisticField = "E_TOTPOP", outStatisticFieldName = "totpop" }// Unsure if this is correct
        //};

        //var qs = new Dictionary<string, string>
        //{
        //    ["f"] = "json",
        //    ["where"] = "1=1",
        //    ["geometryType"] = "esriGeometryEnvelope",
        //    ["geometry"] = geometryJson,          // <-- JSON envelope + SR
        //    ["inSR"] = "4326",                    // <-- include input SR
        //    ["spatialRel"] = "esriSpatialRelIntersects",
        //    ["returnGeometry"] = "false",
        //    ["outStatistics"] = JsonSerializer.Serialize(stats),
        //};

        //var url = $"{_layerUrl}/query?{BuildQuery(qs)}";
        //using var resp = await _http.GetAsync(url, ct);
        //resp.EnsureSuccessStatusCode();

        //using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        //if (doc.RootElement.TryGetProperty("features", out var feats) && feats.GetArrayLength() > 0)
        //{
        //    var attrs = feats[0].GetProperty("attributes");
        //    if (attrs.TryGetProperty("avg", out var avgToken) && avgToken.ValueKind == JsonValueKind.Number)
        //    {
        //        var percentileAsync = avgToken.GetDouble();
        //        if (percentileAsync is >= 0 and <= 1)
        //            return percentileAsync;
        //    }
        //    if (attrs.TryGetProperty("max", out var maxToken) && maxToken.ValueKind == JsonValueKind.Number)
        //    {
        //        var percentileAsync = maxToken.GetDouble();
        //        if (percentileAsync is >= 0 and <= 1)
        //            return percentileAsync;
        //    }
        //}

        // Fallback: fetch values and average client-side
        var qs2 = new Dictionary<string, string>
        {
            ["f"] = "json",
            ["where"] = "1=1",
            ["geometryType"] = "esriGeometryEnvelope",
            ["geometry"] = geometryJson,          // <-- same JSON geometry
            ["inSR"] = "4326",                    // <-- WAS MISSING BEFORE
            ["spatialRel"] = "esriSpatialRelIntersects",
            ["returnGeometry"] = "false",
            ["outFields"] = "RPL_THEMES,E_TOTPOP",               // RPL_THEMES,E_TOTPOP
            ["resultRecordCount"] = "2000"
        };
        var url2 = $"{_layerUrl}/query?{BuildQuery(qs2)}";
        Console.WriteLine($"SVI query URL: {url2}");
        using var resp2 = await _http.GetAsync(url2, ct);
        resp2.EnsureSuccessStatusCode();

        using var doc2 = await JsonDocument.ParseAsync(await resp2.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        if (!doc2.RootElement.TryGetProperty("features", out var feats2) || feats2.GetArrayLength() == 0)
            throw new Exception($"doc2 is FUCKING missing `features`\n\nsee here\n{doc2.RootElement.ToString()}");

        double sum = 0; 
        int n = 0;
        int pop = 0;
        foreach (var f in feats2.EnumerateArray())
        {
            if (f.TryGetProperty("attributes", out var a) &&
                a.TryGetProperty("RPL_THEMES", out var v) && a.TryGetProperty("E_TOTPOP", out var t) &&
                v.ValueKind == JsonValueKind.Number && t.ValueKind == JsonValueKind.Number)
            {
                var p = t.GetInt32();
                pop += p;
                var d = v.GetDouble();
                if (d is < 0 or > 1) continue; // invalid
                sum += d;
                n++;
            }
        }
        if (n == 0) throw new Exception("No FUCKING valid SVI values found in features.");
        var sviPercentile = n > 0 ? sum / n : 0.5;
        var sviPop = pop;
        return new PopulationSvi(sviPop, sviPercentile);
    }

    private static string BuildQuery(Dictionary<string, string> kv)
        => string.Join("&", kv.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
}
public record PopulationSvi(int TotalPopulation, double AverageSviPercentile);