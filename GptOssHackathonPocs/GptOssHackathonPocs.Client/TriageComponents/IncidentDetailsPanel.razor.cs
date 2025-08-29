using GptOssHackathonPocs.Core.Models;
using Microsoft.AspNetCore.Components;

namespace GptOssHackathonPocs.Client.TriageComponents;

public partial class IncidentDetailsPanel
{
    [Parameter] public Incident? Incident { get; set; }
    [Parameter] public string[] CriticalFacilities { get; set; } = [];
    [Parameter] public double PopulationAffected { get; set; }
    [Parameter] public double SviPercentile { get; set; }
    private static string SeverityToColor(IncidentSeverity s) => s switch
    {
        IncidentSeverity.Extreme => "danger",
        IncidentSeverity.Severe => "warning",
        IncidentSeverity.Moderate => "info",
        _ => "secondary"
    };
}