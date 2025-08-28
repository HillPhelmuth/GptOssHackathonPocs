using System.Text.Json;
using NetTopologySuite.Geometries;

namespace GptOssHackathonPocs.Core.Models.Enrichment
{
    /// <summary>
    /// HIFLD Hospitals FeatureServer client. Returns facility strings like
    /// "Ben Taub Hospital (Houston, TX) — General Acute Care, beds: 496".
    /// Default layer points at the HIFLD Hospitals (layer 0).
    /// </summary>
    public sealed class FacilityIndexHospitals(
        HttpClient http,
        string layerUrl =
            "https://services7.arcgis.com/RtX7tFjxuxerbFfd/arcgis/rest/services/HospitalsHospitals/FeatureServer/0")
        : IFacilityIndex
    {
        private readonly string _layerUrl = layerUrl.TrimEnd('/');

        public async Task<string[]> NearbyFacilities(Geometry? g)
            => await NearbyFacilitiesAsync(g, CancellationToken.None);
        //ToDo NOT WORKING
        public async Task<string[]> NearbyFacilitiesAsync(Geometry? g, CancellationToken ct)
        {
            if (g is null || g.IsEmpty) throw new Exception("Geometry is null");

            var env = g.EnvelopeInternal;
            var qs = new Dictionary<string, string>
            {
                ["f"] = "json",
                ["where"] = "1=1",
                ["geometryType"] = "esriGeometryEnvelope",
                ["geometry"] = $"{env.MinX},{env.MinY},{env.MaxX},{env.MaxY}",
                ["inSR"] = "4326",
                ["spatialRel"] = "esriSpatialRelIntersects",
                ["returnGeometry"] = "false",
                ["outFields"] = "NAME,CITY,STATE,BEDS,TYPE,WEBSITE",
                ["resultRecordCount"] = "2000"
            };

            var url = $"{_layerUrl}/query?{BuildQuery(qs)}";
            Console.WriteLine($"Facility Query: {url}");
            using var resp = await http.GetAsync(url, ct);
            resp.EnsureSuccessStatusCode();

            using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            if (!doc.RootElement.TryGetProperty("features", out var feats) || feats.GetArrayLength() == 0)
                return [];

            var list = new List<string>();
            foreach (var f in feats.EnumerateArray())
            {
                var a = f.GetProperty("attributes");
                string name = GetStr(a, "NAME") ?? "Hospital";
                string city = GetStr(a, "CITY");
                string state = GetStr(a, "STATE");
                string type = GetStr(a, "TYPE");
                string beds = a.TryGetProperty("BEDS", out var bd) && bd.ValueKind == JsonValueKind.Number
                                ? bd.GetDouble().ToString("0") : "";

                var parts = new List<string> { name };
                var loc = string.Join(", ", new[] { city, state }.Where(s => !string.IsNullOrWhiteSpace(s)));
                if (!string.IsNullOrWhiteSpace(loc)) parts.Add($"({loc})");
                if (!string.IsNullOrWhiteSpace(type)) parts.Add($"— {type}");
                if (!string.IsNullOrWhiteSpace(beds)) parts.Add($", beds: {beds}");

                list.Add(string.Join(" ", parts));
            }
            return list.ToArray();
        }

        private static string? GetStr(JsonElement attrs, string key)
            => attrs.TryGetProperty(key, out var v) && v.ValueKind != JsonValueKind.Null ? v.ToString() : null;

        private static string BuildQuery(Dictionary<string, string> kv)
            => string.Join("&", kv.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
    }
}
