using Microsoft.AspNetCore.Components;

namespace GptOssHackathonPocs.Client.TriageComponents;

public partial class DataFeedsList
{
    public sealed record Feed(string Name, string Url, string PollInterval, string Type);

    [Parameter]
    public Feed[] Feeds { get; set; } =
    [
        new Feed("National Weather Service Alerts", "https://api.weather.gov/alerts/active", "2 min", "REST"),
        new Feed("USGS Earthquake Feed",
            "https://earthquake.usgs.gov/earthquakes/feed/v1.0/summary/significant_hour.geojson", "5 min", "GeoJSON"),
        new Feed("NASA FIRMS Active Fires", "https://firms.modaps.eosdis.nasa.gov/api", "15 min", "REST"),
        new Feed("National Hurricane Center", "https://www.nhc.noaa.gov/CurrentStorms.json", "30 min", "JSON")
    ];
}