using GptOssHackathonPocs.Core.Models;
using Microsoft.AspNetCore.Components;

namespace GptOssHackathonPocs.Client.TriageComponents;

public partial class DataFeedsList
{
    public sealed record Feed(string Name, string Url, string PollInterval, string Type, int Count);
    [Parameter]
    public IEnumerable<Incident> Incidents { get; set; } = [];

    [Parameter]
    public List<Feed> Feeds { get; set; } =
    [
        new Feed("National Weather Service Alerts", "https://api.weather.gov/alerts/active", "2 min", "REST",0),
        new Feed("USGS Earthquake Feed",
            "https://earthquake.usgs.gov/earthquakes/feed/v1.0/summary/significant_hour.geojson", "5 min", "GeoJSON",0),
        new Feed("NASA FIRMS Active Fires", "https://firms.modaps.eosdis.nasa.gov/api", "15 min", "REST",0),
        new Feed("National Hurricane Center", "https://www.nhc.noaa.gov/CurrentStorms.json", "30 min", "JSON",0)
    ];

    protected override Task OnParametersSetAsync()
    {
        Feeds.Clear();
        Feeds.Add(new Feed("National Weather Service Alerts", "https://api.weather.gov/alerts/active", "2 min", "REST", Incidents.Count(x => x.Source == IncidentSource.NwsAlert)));
        Feeds.Add(new Feed("USGS Earthquake Feed",
            "https://earthquake.usgs.gov/earthquakes/feed/v1.0/summary/significant_hour.geojson", "5 min", "REST", Incidents.Count(x => x.Source == IncidentSource.UsgsQuake)));
        Feeds.Add(new Feed("NASA FIRMS Active Fires", "https://firms.modaps.eosdis.nasa.gov/api", "15 min", "REST", Incidents.Count(x => x.Source == IncidentSource.NasaFirms)));
        Feeds.Add(new Feed("National Hurricane Center", "https://www.nhc.noaa.gov/CurrentStorms.json", "30 min", "REST", Incidents.Count(x => x.Source == IncidentSource.NhcStorm)));
        return base.OnParametersSetAsync();
    }
}