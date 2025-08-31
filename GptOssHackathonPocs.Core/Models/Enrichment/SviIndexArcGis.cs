using System.ComponentModel;
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
[Description("Client for querying CDC/ATSDR SVI FeatureServer and returning population-weighted SVI percentiles.")]
public sealed class SviIndexArcGis : ISviIndex
{
    private readonly HttpClient _http;
    private readonly string _layerUrl;
    private readonly string _field;

    /// <summary>
    /// Creates a new instance of <see cref="SviIndexArcGis"/>.
    /// </summary>
    /// <param name="http">Configured HttpClient used to query the ArcGIS FeatureServer.</param>
    /// <param name="layerUrl">FeatureServer layer URL (defaults to CDC SVI 2022 archive layer).</param>
    /// <param name="field">Field name containing SVI percentile values (defaults to "RPL_THEMES").</param>
    [Description("Initializes the SviIndexArcGis client with an HttpClient, FeatureServer layer URL and SVI field name.")]
    public SviIndexArcGis(HttpClient http,
        string layerUrl =
            "https://services2.arcgis.com/FiaPA4ga0iQKduv3/arcgis/rest/services/CDC_SVI_2022_(Archive)/FeatureServer/2",
        string field = "RPL_THEMES")
    {
        _http = http;
        _layerUrl = layerUrl.TrimEnd('/');
        _field = field;
    }

    /// <summary>
    /// Synchronously-friendly wrapper that gets the SVI percentile for the provided geometry.
    /// </summary>
    /// <param name="g">Geometry (e.g., point or polygon) to query against SVI tracts. Must be non-null and non-empty.</param>
    /// <returns>A <see cref="PopulationSvi"/> containing the total population and average SVI percentile for intersecting tracts.</returns>
    [Description("Synchronous wrapper returning population and average SVI percentile for the given geometry.")]
    public async Task<PopulationSvi> GetSviPercentile(Geometry? g)
        => await GetPercentileAsync(g, CancellationToken.None);

    /// <summary>
    /// Queries the configured ArcGIS FeatureServer for intersecting features, extracts SVI percentiles and populations,
    /// and returns an aggregated <see cref="PopulationSvi"/> that contains the total population and the average SVI percentile.
    /// </summary>
    /// <param name="g">Geometry to query. Must not be null or empty.</param>
    /// <param name="ct">Cancellation token for the asynchronous operation.</param>
    /// <returns>A <see cref="PopulationSvi"/> with total population and average SVI percentile (0..1).</returns>
    /// <exception cref="Exception">Thrown when geometry is null/empty or when no valid features are returned.</exception>
    [Description("Asynchronously queries ArcGIS for features intersecting the geometry and computes population-weighted SVI statistics.")]
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

        
        var qs2 = new Dictionary<string, string>
        {
            ["f"] = "json",
            ["where"] = "1=1",
            ["geometryType"] = "esriGeometryEnvelope",
            ["geometry"] = geometryJson,          
            ["inSR"] = "4326",                    
            ["spatialRel"] = "esriSpatialRelIntersects",
            ["returnGeometry"] = "false",
            ["outFields"] = "RPL_THEMES,E_TOTPOP", // RPL_THEMES = Svi data,E_TOTPOP = population data
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
/// <summary>
/// Result type representing the total population and the average SVI percentile (0..1) for that population.
/// </summary>
[Description("Represents total population and average SVI percentile computed from intersecting features.")]
public record PopulationSvi
{
    /// <summary>
    /// Initializes a new instance of <see cref="PopulationSvi"/>.
    /// </summary>
    /// <param name="TotalPopulation">Total population for intersecting features.</param>
    /// <param name="AverageSviPercentile">Average SVI percentile (range 0..1).</param>
    [Description("Constructs a PopulationSvi with total population and average SVI percentile.")]
    public PopulationSvi(int TotalPopulation, double AverageSviPercentile)
    {
        this.TotalPopulation = TotalPopulation;
        this.AverageSviPercentile = AverageSviPercentile;
    }

    /// <summary>
    /// Total population covered by the intersecting features.
    /// </summary>
    [Description("Total population covered by intersecting features.")]
    public int TotalPopulation { get; init; }

    /// <summary>
    /// Average SVI percentile for the intersecting features (value between 0 and 1).
    /// </summary>
    [Description("Average SVI percentile for the intersecting features (0..1).")]
    public double AverageSviPercentile { get; init; }

}