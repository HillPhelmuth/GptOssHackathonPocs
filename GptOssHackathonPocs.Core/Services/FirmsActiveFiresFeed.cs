using CsvHelper;
using CsvHelper.Configuration;
using GptOssHackathonPocs.Core.Models;
using GptOssHackathonPocs.Core.Services;
using System.Formats.Asn1;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace GptOssHackathonPocs.Core.Services;

public sealed class FirmsActiveFiresFeed : IIncidentFeed
{
    private readonly HttpClient _http;
    private readonly FirmsOptions _opt = new();

    public FirmsActiveFiresFeed(HttpClient httpClient)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        

        
        if (string.IsNullOrWhiteSpace(_opt.Dataset))
            throw new ArgumentException("FIRMS Dataset is required (e.g., VIIRS_SNPP_NRT).");
        if (_opt.DayRange != 1) _opt.DayRange = 1;
    }

    public async Task<IReadOnlyList<Incident>> FetchAsync(CancellationToken ct = default)
    {
        var url = BuildAreaApiUrl(_opt);
        await using var stream = await _http.GetStreamAsync(url, ct);

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
            DetectDelimiter = true
        };

        var results = new List<Incident>();

        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, config);
        csv.Context.RegisterClassMap<FirmsCsvMap>();

        await foreach (var row in csv.GetRecordsAsync<FirmsCsvRow>(ct))
        {
            // optional confidence filter
            if (_opt.MinConfidence.HasValue && !MeetsConfidence(row.confidence, _opt.MinConfidence.Value))
                continue;

            var timestamp = ParseTimestampUtc(row.acq_date, row.acq_time); // FIRMS dates are GMT
            var severity = MapSeverity(row.confidence);

            // Build a Feature with properties similar to NWS feed: source, severity, title (+ FIRMS metadata)
            var title = $"Active fire (FIRMS {row.satellite}/{row.instrument}) at {row.latitude:F4}, {row.longitude:F4}";
            var feature = new
            {
                type = "Feature",
                geometry = new
                {
                    type = "Point",
                    coordinates = new[] { row.longitude, row.latitude } // [lon, lat]
                },
                properties = new
                {
                    // standard properties for popup and styling
                    source = nameof(IncidentSource.NasaFirms),
                    severity = severity.ToString().ToLowerInvariant(),
                    title,
                    // FIRMS-specific metadata
                    dataset = _opt.Dataset,
                    row.confidence,
                    row.frp,
                    row.daynight,
                    row.satellite,
                    row.instrument,
                    row.acq_date,
                    row.acq_time
                }
            };

            var geoJson = JsonSerializer.Serialize(feature);

            var id = $"{_opt.Dataset}:{row.acq_date}:{row.acq_time}:{row.latitude:F4},{row.longitude:F4}";

            results.Add(new Incident
            {
                Id = id,
                Source = IncidentSource.NasaFirms, // add this to your enum if it isn't there yet
                Severity = severity,
                Title = title,
                Description = $"Confidence: {row.confidence?.ToUpperInvariant() ?? "n/a"}, Fire Radiative Power:  {row.frp:F1} MW, Day/Night: {row.daynight}",
                Timestamp = timestamp,
                GeoJson = geoJson,
                Link = url // points back to the query used
            });
        }

        return results;
    }

    private static string BuildAreaApiUrl(FirmsOptions opt)
    {
        // area can be: "world" OR "minLon,minLat,maxLon,maxLat" (comma-separated, no spaces)
        var usAreaLatLong = "-125.0,24.0,-66.5,49.5"; // CONUS bounding box
        opt.Area = usAreaLatLong;
        var area = string.IsNullOrWhiteSpace(opt.Area) ? "world" : opt.Area.Trim();
        // Example: https://firms.modaps.eosdis.nasa.gov/api/area/csv/{MAP_KEY}/{DATASET}/{AREA}/{DAY_RANGE}
        var sb = new StringBuilder("https://firms.modaps.eosdis.nasa.gov/api/area/csv/");
        sb.Append(Uri.EscapeDataString(opt.MapKey)).Append('/')
          .Append(Uri.EscapeDataString(opt.Dataset)).Append('/')
          .Append(area).Append('/')
          .Append(opt.DayRange.ToString(CultureInfo.InvariantCulture));
        return sb.ToString();
    }

    private static bool MeetsConfidence(string? conf, char min)
    {
        // FIRMS confidence letters typically: l (low), n (nominal), h (high)
        // Order: l < n < h
        int Rank(char c) => c switch { 'h' or 'H' => 3, 'n' or 'N' => 2, 'l' or 'L' => 1, _ => 0 };
        if (string.IsNullOrWhiteSpace(conf)) return false;
        return Rank(conf[0]) >= Rank(char.ToLowerInvariant(min));
    }

    private static DateTimeOffset ParseTimestampUtc(string date, string time)
    {
        // date: "YYYY-MM-DD"; time is minutes after midnight or "HHMM" (e.g., "1", "1425")
        var day = DateTime.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        var hhmm = (time ?? "0").PadLeft(4, '0');
        var hours = int.Parse(hhmm[..2], CultureInfo.InvariantCulture);
        var minutes = int.Parse(hhmm.Substring(2, 2), CultureInfo.InvariantCulture);
        return new DateTimeOffset(day.Year, day.Month, day.Day, hours, minutes, 0, TimeSpan.Zero);
    }

    private static IncidentSeverity MapSeverity(string? conf) =>
        (conf?.ToLowerInvariant()) switch
        {
            "h" => IncidentSeverity.Severe,
            "n" => IncidentSeverity.Moderate,
            "l" => IncidentSeverity.Minor,
            _ => IncidentSeverity.Unknown
        };

    // --- CSV shape for FIRMS "area" endpoint ---
    private sealed class FirmsCsvRow
    {
        public double latitude { get; set; }
        public double longitude { get; set; }
        public string acq_date { get; set; } = "";
        public string acq_time { get; set; } = "";
        public string satellite { get; set; } = "";
        public string instrument { get; set; } = "";
        public string? confidence { get; set; }
        public double frp { get; set; }
        public string daynight { get; set; } = "";
    }

    private sealed class FirmsCsvMap : ClassMap<FirmsCsvRow>
    {
        public FirmsCsvMap()
        {
            Map(m => m.latitude).Name("latitude");
            Map(m => m.longitude).Name("longitude");
            Map(m => m.acq_date).Name("acq_date");
            Map(m => m.acq_time).Name("acq_time");
            Map(m => m.satellite).Name("satellite");
            Map(m => m.instrument).Name("instrument");
            Map(m => m.confidence).Name("confidence");
            Map(m => m.frp).Name("frp");
            Map(m => m.daynight).Name("daynight");
        }
    }
}

// Options you can bind from appsettings.json or configure in DI
public sealed class FirmsOptions
{
    /// <summary>FIRMS MAP_KEY (free API key)</summary>
    public string MapKey { get; init; } = "d0e588bd902f973ecdfa2d4b961dbd4b";

    /// <summary>Dataset id, e.g., VIIRS_SNPP_NRT, VIIRS_NOAA20_NRT, VIIRS_NOAA21_NRT, MODIS_NRT</summary>
    public string Dataset { get; init; } = "VIIRS_SNPP_NRT";

    /// <summary>"world" or "minLon,minLat,maxLon,maxLat" (WGS84)</summary>
    public string Area { get; set; } = "world";

    /// <summary>Number of days to include (e.g., 1 = last 24h)</summary>
    public int DayRange { get; set; } = 1;

    /// <summary>Optional confidence filter: 'l', 'n', or 'h'</summary>
    public char? MinConfidence { get; init; } = 'h';
}
