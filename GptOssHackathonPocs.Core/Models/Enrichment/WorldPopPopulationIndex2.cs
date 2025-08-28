using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GptOssHackathonPocs.Core.Models.Enrichment
{
    public interface IEePopulationExpressionFactory
    {
        /// <summary>
        /// Build a serialized Earth Engine Expression JSON (string) that returns a
        /// FeatureCollection whose properties include a numeric field (e.g. "population")
        /// representing the population sum for the provided geometry.
        /// 
        /// The returned JSON MUST be a valid EE Expression graph as expected by
        /// /v1/projects/{project}/table:computeFeatures.
        /// </summary>
        string BuildExpressionJson(string geometryGeoJson, int scaleMeters);

        /// <summary>
        /// The name of the numeric property produced by your reducer (e.g. "population").
        /// </summary>
        string OutputPropertyName { get; }
    }

    /// <summary>
    /// Simple template-based factory.
    /// Your template should be the exact output of ee.serializer.encode(...) for something like:
    /// 
    ///   var fc = ee.FeatureCollection([ee.Feature(ee.Geometry(<GEOJSON>), {})]);
    ///   var img = ee.ImageCollection('JRC/GHSL/P2023A/GHS_POP/100m')
    ///               .filter(ee.Filter.eq('epoch', 2020))
    ///               .select('population_count')
    ///               .mosaic();
    ///   var outFc = img.reduceRegions({
    ///       collection: fc,
    ///       reducer: ee.Reducer.sum().setOutputs(['population']),
    ///       scale: <SCALE>
    ///   });
    ///   outFc
    /// 
    /// Then, in the serialized JSON string, replace the literal geometry object with {{GEOJSON}}
    /// and the scale literal with {{SCALE}}. Keep everything else untouched.
    /// </summary>
    public sealed class TemplateEePopulationExpressionFactory : IEePopulationExpressionFactory
    {
        private readonly string _template;
        public string OutputPropertyName { get; }

        /// <param name="templateExpressionJson">
        /// The serialized EE Expression JSON with tokens {{GEOJSON}} and {{SCALE}}.
        /// IMPORTANT: The token {{GEOJSON}} must stand where a JSON object is expected
        /// (not inside quotes). Likewise {{SCALE}} must be a number placeholder.
        /// </param>
        /// <param name="outputPropertyName">The reducer output field name (e.g. "population").</param>
        public TemplateEePopulationExpressionFactory(string templateExpressionJson, string outputPropertyName = "population")
        {
            _template = templateExpressionJson ?? throw new ArgumentNullException(nameof(templateExpressionJson));
            OutputPropertyName = string.IsNullOrWhiteSpace(outputPropertyName) ? "population" : outputPropertyName;
        }

        public string BuildExpressionJson(string geometryGeoJson, int scaleMeters)
        {
            if (string.IsNullOrWhiteSpace(geometryGeoJson))
                throw new ArgumentException("GeoJSON is required", nameof(geometryGeoJson));

            // Replace tokens. We intentionally DON'T JSON-escape the geometry,
            // because the placeholder should be a JSON object position in the template.
            var s = _template.Replace("{{\"GEOJSON\"}}", geometryGeoJson)
                             .Replace("{{\"SCALE\"}}", scaleMeters.ToString());
            return s;
        }
    }

    /// <summary>
    /// Earth Engine-backed population index.
    /// Requires a valid EE computeFeatures expression (provided by factory)
    /// that performs a reduceRegions(sum) over the supplied geometry.
    /// </summary>
    public sealed class WorldPopPopulationIndex2 : IPopulationIndex
    {
        private readonly EarthEngineRestClient _ee;
        private readonly IEePopulationExpressionFactory _factory;
        private readonly int _scaleMeters;
        private readonly GeoJsonWriter _geoWriter;

        /// <param name="ee">Configured EarthEngineRestClient (project + creds).</param>
        /// <param name="factory">Builds the serialized Expression per geometry.</param>
        /// <param name="scaleMeters">Pixel scale at which to reduce (e.g., 100 for 100m GHSL).</param>
        public WorldPopPopulationIndex2(
            EarthEngineRestClient ee
            )
        {
            var scaleMeters = 100; // default for WorldPop/GHSL
            var template =  File.ReadAllText(@"C:\Users\adamh\source\repos\GptOssHackathonPocs\GptOssHackathonPocs.Core\Models\Enrichment\Templates\ghsl_population_sum_template.json");
            var factory = new TemplateEePopulationExpressionFactory(template, "population");
            _ee = ee ?? throw new ArgumentNullException(nameof(ee));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _scaleMeters = scaleMeters;
            _geoWriter = new GeoJsonWriter();
        }

        public async Task<double> EstimatePopulation(Geometry? g)
            => await EstimatePopulation(g, CancellationToken.None);

        private async Task<double> EstimatePopulation(Geometry? g, CancellationToken ct)
        {
            if (g == null || g.IsEmpty) return 0d;

            // EE reduceRegions over WorldPop/GHSL expects area-like geometries.
            // If we get non-polygon input, coerce to polygon-ish:
            var geom = EnsurePolygonal(g);

            // Build a lean GeoJSON geometry (no Feature wrapper)
            string geomJson = _geoWriter.Write(geom);

            // Build serialized EE expression for this geometry
            string expr = _factory.BuildExpressionJson(geomJson, _scaleMeters);
            var body = ConvertCodeEditorCompoundToExpression(expr);
            // Call EE and accumulate the output property
            double total = 0d;
            await foreach (var feature in _ee.ComputeFeaturesAllAsync(body, pageSize: 1000, workloadTag: "pop-sum", ct: ct))
            {
                if (feature.TryGetProperty("properties", out var props) &&
                    props.TryGetProperty(_factory.OutputPropertyName, out var val) &&
                    val.ValueKind == JsonValueKind.Number)
                {
                    total += val.GetDouble();
                }
            }

            return total;
        }

        private static Geometry EnsurePolygonal(Geometry g)
        {
            return g switch
            {
                Polygon or MultiPolygon => g,
                _ => g.ConvexHull() is Geometry hull && (hull is Polygon or MultiPolygon) ? hull : g.Buffer(50) // 50m fallback
            };
        }
        static JsonObject ConvertCodeEditorCompoundToExpression(string compoundJson)
        {
            using var doc = JsonDocument.Parse(compoundJson);
            var root = doc.RootElement;

            // Build "values" map from the ["id", node] pairs in "scope"
            var valuesObj = new JsonObject();
            foreach (var pair in root.GetProperty("scope").EnumerateArray())
            {
                var id = pair[0].GetString()!;
                // Preserve node structure exactly
                var node = JsonNode.Parse(pair[1].GetRawText());
                valuesObj[id] = node;
            }

            // The "result" is the inner "value" of the top-level { "type":"ValueRef", "value":"<id>" }
            var resultId = root.GetProperty("value").GetProperty("value").GetString()!;

            // Return { "expression": { "values": {...}, "result": "<id>" } }
            return new JsonObject
            {
                ["expression"] = new JsonObject
                {
                    ["values"] = valuesObj,
                    ["result"] = resultId
                }
            };
        }
    }
}
