using GptOssHackathonPocs.Core.Models;
using GptOssHackathonPocs.Core.Models.Enrichment;
using Microsoft.AspNetCore.Components;

namespace GptOssHackathonPocs.Client.TriageComponents;

public partial class IncidentDetailsPanel
{
    [Parameter] public Incident? Incident { get; set; }
    [Parameter] public NearbyHospital[] CriticalFacilities { get; set; } = [];
    [Parameter] public double PopulationAffected { get; set; }
    [Parameter] public double SviPercentile { get; set; }
    [Parameter] public EventCallback<NearbyHospital> OnHospitalSelected { get; set; }
    private static string SeverityToColor(IncidentSeverity s) => s switch
    {
        IncidentSeverity.Extreme => "danger",
        IncidentSeverity.Severe => "warning",
        IncidentSeverity.Moderate => "info",
        _ => "secondary"
    };

    private void SelectHospital(NearbyHospital hospital)
    {
        OnHospitalSelected.InvokeAsync(hospital);
    }
}