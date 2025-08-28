// NuGet:
//   - Google.Apis.Auth
//   - System.Net.Http.Json (if targeting < .NET 7; otherwise use System.Text.Json APIs below)
//
// What this does:
//   1) Loads your Earth Engine–registered Service Account key (JSON).
//   2) Mints an OAuth2 access token for the Earth Engine scope.
//   3) Calls the Earth Engine REST endpoint: POST https://earthengine.googleapis.com/v1/projects/{project}/table:computeFeatures
//   4) Returns the resulting GeoJSON FeatureCollection (each input geometry gets a row with your computed fields).
//
// IMPORTANT: You MUST supply a valid serialized Earth Engine "Expression" graph as JSON for `expressionJson`.
// The expression describes the server-side computation (e.g., ee.Image(pop).reduceRegions(… ee.Reducer.sum() …)).
// Easiest path: prototype in the JS/Python client and serialize with `ee.serializer.encode(...)`,
// then paste the resulting JSON string into your app, or load it from a file.
// Docs: projects.table.computeFeatures (v1): https://developers.google.com/earth-engine/reference/rest/v1/projects.table/computeFeatures
// Expression schema: https://developers.google.com/earth-engine/reference/rest/v1/Expression
// Zonal stats API surface (concept): https://developers.google.com/earth-engine/apidocs/ee-image-reduceregions

using Google.Apis.Auth.OAuth2;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace GptOssHackathonPocs.Core.Models.Enrichment;
public sealed class EarthEngineRestClient : IDisposable
{
    private static readonly Uri BaseUri = new("https://earthengine.googleapis.com/");
    private static readonly string[] Scopes = new[]
    {
        // Either of these works; earthengine.readonly if you don't need to write assets etc.
        "https://www.googleapis.com/auth/earthengine",
        "https://www.googleapis.com/auth/cloud-platform"
    };

    private readonly string _projectPath; // "projects/{projectId}"
    private readonly GoogleCredential _credential;
    private readonly HttpClient _http;

    private EarthEngineRestClient(string projectId, GoogleCredential credential, HttpClient? http = null)
    {
        _projectPath = $"projects/{projectId}";
        _credential = credential;
        _http = http ?? new HttpClient { BaseAddress = BaseUri };
    }

    public static async Task<EarthEngineRestClient> CreateAsync(string projectId, string serviceAccountKeyJsonPath, HttpClient? http = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(projectId)) throw new ArgumentException("projectId is required", nameof(projectId));
        if (string.IsNullOrWhiteSpace(serviceAccountKeyJsonPath)) throw new ArgumentException("serviceAccountKeyJsonPath is required", nameof(serviceAccountKeyJsonPath));

        var credential = GoogleCredential.FromFile(serviceAccountKeyJsonPath).CreateScoped(Scopes);
        // Prime the token once to fail fast if misconfigured:
        _ = await credential.UnderlyingCredential.GetAccessTokenForRequestAsync(cancellationToken: ct).ConfigureAwait(false);

        return new EarthEngineRestClient(projectId, credential, http);
    }
    
    /// <summary>
    /// Sends a POST to /v1/projects/{project}/table:computeFeatures with the supplied serialized Expression JSON.
    /// Returns the raw response as a JsonDocument (GeoJSON FeatureCollection with an optional nextPageToken).
    /// </summary>
    public async Task<JsonDocument> ComputeFeaturesAsync(JsonObject expressionJson,
        int? pageSize = null,
        string? pageToken = null,
        string? workloadTag = null,
        CancellationToken ct = default)
    {
        

        // Build request body
        var expressionDoc = expressionJson;
        var body = new Dictionary<string, object?>
        {
            ["expression"] = JsonSerializer.Deserialize<object>(expressionJson["expression"]!.ToJsonString()),
            ["pageSize"] = pageSize,
            ["pageToken"] = pageToken,
            ["workloadTag"] = workloadTag
        };

        // Remove nulls to keep payload clean
        var options = new JsonSerializerOptions { IgnoreNullValues = true, WriteIndented = false };
        var payload = JsonSerializer.Serialize(body, options);
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");

        // Auth header per request (token refresh safe)
        var token = await _credential.UnderlyingCredential.GetAccessTokenForRequestAsync("https://www.googleapis.com/auth/earthengine", ct).ConfigureAwait(false);
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Endpoint (v1). v1beta also exists; prefer v1 unless you need beta-only features.
        var url = $"v1/{_projectPath}/table:computeFeatures";

        using var resp = await _http.PostAsync(url, content, ct).ConfigureAwait(false);
        var respText = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!resp.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Earth Engine computeFeatures failed ({(int)resp.StatusCode} {resp.ReasonPhrase}). Body:\n{respText}");
        }

        return JsonDocument.Parse(respText);
    }

    /// <summary>
    /// Convenience helper to auto-paginate computeFeatures until completion.
    /// Yields each page's GeoJSON features as JsonElement.
    /// </summary>
    public async IAsyncEnumerable<JsonElement> ComputeFeaturesAllAsync(JsonObject expressionJson,
        int pageSize = 1000,
        string? workloadTag = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken ct = default)
    {
        string? token = null;

        do
        {
            using var doc = await ComputeFeaturesAsync(expressionJson, pageSize, token, workloadTag, ct).ConfigureAwait(false);
            var root = doc.RootElement;

            if (root.TryGetProperty("features", out var features))
            {
                foreach (var feature in features.EnumerateArray())
                {
                    yield return feature;
                }
            }

            token = root.TryGetProperty("nextPageToken", out var np) ? np.GetString() : null;
        }
        while (!string.IsNullOrEmpty(token));
    }

    public void Dispose() => _http.Dispose();
}

// ---------------------------
// Example usage (Program.cs):
// ---------------------------
// This shows the flow end-to-end. You still need to provide `expressionJson`—the serialized graph
// that performs something like: ee.Image(pop).reduceRegions(fc, ee.Reducer.sum(), scale=100).
// For population totals, pick a population *count* image (e.g., GHSL P2023A or WorldPop count) and sum.
// Reduce at the raster's native scale for correct counts.
//
// To generate the Expression JSON:
//   - Prototype in JS/Python, then `ee.serializer.encode(computation)`.
//   - Paste the resulting JSON string as `expressionJson` below or load from a file.
//
// Reference guides used while wiring this up:
//   - Compute features REST: https://developers.google.com/earth-engine/reference/rest/v1/projects.table/computeFeatures
//   - reduceRegions docs:    https://developers.google.com/earth-engine/apidocs/ee-image-reduceregions
//   - Service accounts:      https://developers.google.com/earth-engine/guides/service_account
//
// NOTE: Your service account must have Earth Engine project-level permission (e.g., roles/earthengine.viewer)
// and the EE API must be enabled on your Cloud project.

//public static class Example
//{
//    public static async Task RunAsync(CancellationToken ct = default)
//    {
//        // 1) Set these:
//        var projectId = "searchwithsemantickernel";                 // e.g., "my-ee-prod"
//        var keyPath = @"C:\Users\adamh\source\repos\GptOssHackathonPocs\GptOssHackathonPocs.Core\searchwithsemantickernel-a3b6f34079f4.json"; // downloaded key for your EE-registered service account

//        // 2) Build or load your serialized Expression JSON (see notes above).
//        //    Common pattern for population totals:
//        //      - Choose an image with counts (e.g., GHSL P2023A population_count at 100m)
//        //      - Clip/mask as needed
//        //      - reduceRegions(collection = your polygons, reducer = ee.Reducer.sum().setOutputs(['population']), scale = 100)
//        //
//        //    You can store the JSON in a file and load it, e.g.:
//        //    var expressionJson = await System.IO.File.ReadAllTextAsync("expression.population_sum.json", ct);
//        //
//        //    Minimal placeholder here (INVALID until you replace with a real serialized graph):
//        var expressionJson = /* load your ee.serializer.encode(...) output here */ "{}";

//        // 3) Create the client and call computeFeatures.
//        var client = await EarthEngineRestClient.CreateAsync(projectId, keyPath, ct: ct);

//        // If you expect many features, use the paginator to stream them:
//        await foreach (var feature in client.ComputeFeaturesAllAsync(expressionJson, pageSize: 1000, workloadTag: "pop-sum", ct))
//        {
//            // Each feature is a GeoJSON feature with properties from your reducer.
//            // Example: extract "population" property (change to your output name).
//            if (feature.TryGetProperty("properties", out var props) &&
//                props.TryGetProperty("population", out var popEl) &&
//                popEl.ValueKind is JsonValueKind.Number)
//            {
//                var pop = popEl.GetDouble();
//                // Do something useful, e.g., write to DB or console:
//                Console.WriteLine(pop);
//            }
//        }

//        // Or if you just want the first page as a JsonDocument:
//        // using var page = await client.ComputeFeaturesAsync(expressionJson, pageSize: 1000, ct: ct);
//        // Console.WriteLine(page.RootElement);
//    }
//}
