using System.Text.Json;
using System.Web;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NetTopologySuite.Precision;
using NetTopologySuite.Simplify;

// Namespace of your choice
namespace GptOssHackathonPocs.Core.Models.Enrichment;

public sealed class WorldPopPopulationIndex : IPopulationIndex
{
    private readonly HttpClient http;
    private readonly string endpoint;
    private readonly string dataset;
    private readonly int year;

    // Tuning knobs
    private readonly int maxUrlLength;          // URL length guard before attempting request
    private readonly int targetMaxCoords;       // try to keep polygons under this coord count before GET
    private readonly int roundDecimals;         // coordinate rounding to shrink JSON
    private readonly double[] simplifyTolerancesDeg; // progressively loosen until under URL limit
    private readonly double[] tileCellSizesDeg; // grid sizes for tiling fallback

    private readonly GeoJsonWriter _geoWriter;

    public WorldPopPopulationIndex(
        HttpClient? httpClient = null,
        string endpoint = "https://api.worldpop.org/v1/services/stats",
        string dataset = "wpgppop",
        int year = 2020,
        int maxUrlLength = 7000,          // many proxies choke ~8k; 7k is a safer default
        int targetMaxCoords = 8000,
        int roundDecimals = 5)
    {
        http = httpClient ?? new HttpClient();
        this.endpoint = endpoint.TrimEnd('/');
        this.dataset = dataset;
        this.year = year;

        this.maxUrlLength = maxUrlLength;
        this.targetMaxCoords = targetMaxCoords;
        this.roundDecimals = roundDecimals;

        // ~1m..100m-ish tolerances in degrees (order of magnitude)
        simplifyTolerancesDeg = new[] { 1e-5, 5e-4};
        // fallback grid sizes (degrees)
        tileCellSizesDeg = new[] { 0.5, 0.10 };

        _geoWriter = new GeoJsonWriter
        {
            // keep feature geometry only — we wrap into a Feature ourselves when needed
            // GeoJsonWriter doesn’t pretty-print, which keeps payload compact
        };
    }

    public async Task<double> EstimatePopulation(Geometry? g)
    {
        if (g == null || g.IsEmpty) return 0;

        // 1) ensure polygonal pieces
        var polys = PreparePolygonParts(g, bufferMetersForNonPolygon: 2000).ToList();
        if (polys.Count == 0) return 0;

        // 2) sum each polygon’s population
        double total = 0;
        foreach (var p in polys)
        {
            var val = await QueryPolygonAsync(p).ConfigureAwait(false);
            if (val.HasValue) total += val.Value;
        }
        return total;
    }

    // ---- Core GET flow ------------------------------------------------------

    private async Task<double?> QueryPolygonAsync(Polygon poly, CancellationToken ct = default)
    {
        // A) try progressively simplified single-shot GET
        foreach (var attempt in BuildSimplifiedCandidates(poly))
        {
            var jGeom = _geoWriter.Write(attempt);
            var featureJson = $"{{\"type\":\"Feature\",\"properties\":{{}},\"geometry\":{jGeom}}}";
            var url = BuildUrl(featureJson);

            if (url.Length > maxUrlLength) continue; // skip; would 414

            var v = await GetValueOrNull(url, ct).ConfigureAwait(false);
            if (v.status == GetStatus.Ok) return v.value;
            if (v.status == GetStatus.UriTooLong) break; // go tile
            // For other HTTP errors, we continue to next simplification level or tile.
        }

        // B) fallback: tile & sum (progressively smaller cells)
        foreach (var cellDeg in tileCellSizesDeg)
        {
            double sum = 0;
            bool gotAny = false;
            var tiles = Tile(poly, cellDeg);

            foreach (var tile in tiles)
            {
                // Each tile gets its own simplified single-shot GET
                var piece = ProgressiveSimplifyForUrl(tile);
                var jGeom = _geoWriter.Write(piece);
                var featureJson = $"{{\"type\":\"Feature\",\"properties\":{{}},\"geometry\":{jGeom}}}";
                var url = BuildUrl(featureJson);

                if (url.Length > maxUrlLength)
                {
                    // If a tiny tile still exceeds limit, hit it with stronger simplify
                    var forced = ForceUrlFit(tile, maxUrlLength);
                    if (forced == null) continue; // skip tiny sliver
                    jGeom = _geoWriter.Write(forced);
                    featureJson = $"{{\"type\":\"Feature\",\"properties\":{{}},\"geometry\":{jGeom}}}";
                    url = BuildUrl(featureJson);
                    if (url.Length > maxUrlLength) continue; // give up on this sliver
                }

                var v = await GetValueOrNull(url, ct).ConfigureAwait(false);
                if (v.status == GetStatus.Ok && v.value.HasValue)
                {
                    sum += v.value.Value;
                    gotAny = true;
                }
                else if (v.status == GetStatus.UriTooLong)
                {
                    // If we hit 414 even here, we’ll rely on the next, smaller cell size
                    gotAny = false;
                    break;
                }
            }
            if (gotAny) return sum;
        }

        return null;
    }

    private enum GetStatus { Ok, UriTooLong, OtherError }

    private async Task<(GetStatus status, double? value)> GetValueOrNull(string url, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Accept.ParseAdd("application/json");
            using var resp = await http.SendAsync(req, ct).ConfigureAwait(false);

            if ((int)resp.StatusCode == 414) return (GetStatus.UriTooLong, null);
            if (!resp.IsSuccessStatusCode) return (GetStatus.OtherError, null);

            var payload = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var val = TryExtractSum(payload);
            return (GetStatus.Ok, val);
        }
        catch (UriFormatException)
        {
            return (GetStatus.UriTooLong, null);
        }
        catch
        {
            return (GetStatus.OtherError, null);
        }
    }

    private string BuildUrl(string featureJson)
    {
        // WorldPop expects the *geometry* wrapped in a Feature as the `geojson` param.
        // Keep dataset & year in query.
        var qs = HttpUtility.ParseQueryString(string.Empty);
        qs["dataset"] = dataset;
        qs["year"] = year.ToString();
        qs["geojson"] = featureJson; // HttpUtility will encode

        return $"{endpoint}?{qs}";
    }

    // ---- Response parsing (robust to slight schema variations) --------------

    private static double? TryExtractSum(string json)
    {
        // Try to be resilient: look for common keys across WorldPop variants
        // e.g., {"data":{"total_population":123.45}} or {"total_population":...} or {"sum":...}
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // If they wrap in {"status": "...", "error": false, "data": {...}}
            if (root.TryGetProperty("data", out var data))
            {
                if (TryGetDouble(data, "total_population", out var tp)) return tp;
                if (TryGetDouble(data, "population", out var pop)) return pop;
                if (TryGetDouble(data, "sum", out var sum)) return sum;
            }

            if (TryGetDouble(root, "total_population", out var tp2)) return tp2;
            if (TryGetDouble(root, "population", out var pop2)) return pop2;
            if (TryGetDouble(root, "sum", out var sum2)) return sum2;

            // Some responses may contain an array of features with statistics attached
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in root.EnumerateArray())
                {
                    if (TryGetDouble(el, "total_population", out var arrTp)) return arrTp;
                    if (TryGetDouble(el, "population", out var arrPop)) return arrPop;
                    if (TryGetDouble(el, "sum", out var arrSum)) return arrSum;
                }
            }
        }
        catch { /* swallow */ }
        return null;

        static bool TryGetDouble(JsonElement el, string name, out double value)
        {
            value = 0;
            if (!el.TryGetProperty(name, out var v)) return false;
            switch (v.ValueKind)
            {
                case JsonValueKind.Number:
                    return v.TryGetDouble(out value);
                case JsonValueKind.String:
                    return double.TryParse(v.GetString(), out value);
                default: return false;
            }
        }
    }

    // ---- Geometry prep ------------------------------------------------------

    private IEnumerable<Polygon> PreparePolygonParts(Geometry g, double bufferMetersForNonPolygon)
    {
        if (g is Polygon p)
        {
            var pp = CleanAndRound(p);
            if (pp != null && !pp.IsEmpty) yield return pp;
            yield break;
        }

        if (g is MultiPolygon mp)
        {
            for (int i = 0; i < mp.NumGeometries; i++)
            {
                var p2 = mp.GetGeometryN(i) as Polygon;
                if (p2 == null) continue;
                var pp = CleanAndRound(p2);
                if (pp != null && !pp.IsEmpty) yield return pp;
            }
            yield break;
        }

        // Buffer points/lines to polygon in degrees (approx)
        double deg = MetersToDegrees(bufferMetersForNonPolygon);
        var poly = g.Buffer(deg);
        if (poly is Polygon pb)
        {
            var pp = CleanAndRound(pb);
            if (pp != null && !pp.IsEmpty) yield return pp;
        }
        else if (poly is MultiPolygon mp2)
        {
            for (int i = 0; i < mp2.NumGeometries; i++)
            {
                var p2 = mp2.GetGeometryN(i) as Polygon;
                if (p2 == null) continue;
                var pp = CleanAndRound(p2);
                if (pp != null && !pp.IsEmpty) yield return pp;
            }
        }
    }

    private Polygon? CleanAndRound(Polygon p)
    {
        // reduce precision to shrink payloads
        var scale = Math.Pow(10, roundDecimals);
        var pm = new PrecisionModel(scale);
        var reduced = (Polygon)new GeometryPrecisionReducer(pm).Reduce(p);
        if (reduced == null || reduced.IsEmpty) return null;
        return reduced;
    }

    private static double MetersToDegrees(double meters) => meters / 111_320.0;

    // ---- Simplification & URL fitting ---------------------------------------

    private IEnumerable<Polygon> BuildSimplifiedCandidates(Polygon p)
    {
        // 1) start with precision-reduced original
        var first = p;
        yield return first;

        // 2) then progressively simplify topology-preserving
        foreach (var tol in simplifyTolerancesDeg)
        {
            var s = (Polygon)TopologyPreservingSimplifier.Simplify(first, tol);
            s = (Polygon)new GeometryPrecisionReducer(new PrecisionModel(Math.Pow(10, roundDecimals))).Reduce(s);
            if (!s.IsEmpty) yield return s;
        }
    }

    private Polygon ProgressiveSimplifyForUrl(Polygon p)
    {
        var candidate = p;
        if (CountCoords(candidate) <= targetMaxCoords) return candidate;

        foreach (var tol in simplifyTolerancesDeg)
        {
            var s = (Polygon)TopologyPreservingSimplifier.Simplify(candidate, tol);
            s = (Polygon)new GeometryPrecisionReducer(new PrecisionModel(Math.Pow(10, roundDecimals))).Reduce(s);
            if (!s.IsEmpty && CountCoords(s) <= targetMaxCoords)
                return s;
        }
        return candidate;
    }

    private Polygon? ForceUrlFit(Polygon p, int limit)
    {
        // Try increasingly aggressive simplification until the encoded URL fits
        foreach (var tol in new[] { 1e-3, 2e-3, 5e-3, 1e-2 })
        {
            var s = (Polygon)TopologyPreservingSimplifier.Simplify(p, tol);
            s = (Polygon)new GeometryPrecisionReducer(new PrecisionModel(Math.Pow(10, roundDecimals))).Reduce(s);
            if (s.IsEmpty) continue;

            var feat = $"{{\"type\":\"Feature\",\"properties\":{{}},\"geometry\":{_geoWriter.Write(s)}}}";
            var url = BuildUrl(feat);
            if (url.Length <= limit) return s;
        }
        return null;
    }

    private static int CountCoords(Geometry g)
    {
        int n = 0;
        for (int i = 0; i < g.NumGeometries; i++)
        {
            var gi = g.GetGeometryN(i);
            switch (gi)
            {
                case Polygon poly:
                    n += poly.ExteriorRing?.NumPoints ?? 0;
                    for (int r = 0; r < poly.NumInteriorRings; r++) n += poly.GetInteriorRingN(r).NumPoints;
                    break;
                case LineString ls:
                    n += ls.NumPoints; break;
                default:
                    n += gi.NumPoints; break;
            }
        }
        return n;
    }

    // ---- Tiling -------------------------------------------------------------

    private IEnumerable<Polygon> Tile(Polygon poly, double cellDeg)
    {
        var env = poly.EnvelopeInternal;
        double startX = Math.Floor(env.MinX / cellDeg) * cellDeg;
        double startY = Math.Floor(env.MinY / cellDeg) * cellDeg;

        for (double x = startX; x < env.MaxX; x += cellDeg)
        for (double y = startY; y < env.MaxY; y += cellDeg)
        {
            var ring = new[]
            {
                new Coordinate(x, y),
                new Coordinate(x + cellDeg, y),
                new Coordinate(x + cellDeg, y + cellDeg),
                new Coordinate(x, y + cellDeg),
                new Coordinate(x, y),
            };
            var ls = poly.Factory.CreateLinearRing(ring);
            var cell = poly.Factory.CreatePolygon(ls);

            var inter = poly.Intersection(cell);
            if (inter.IsEmpty) continue;

            if (inter is Polygon p) yield return p;
            else if (inter is MultiPolygon mp)
            {
                for (int i = 0; i < mp.NumGeometries; i++)
                {
                    var pi = mp.GetGeometryN(i) as Polygon;
                    if (pi != null && !pi.IsEmpty) yield return pi;
                }
            }
        }
    }
}