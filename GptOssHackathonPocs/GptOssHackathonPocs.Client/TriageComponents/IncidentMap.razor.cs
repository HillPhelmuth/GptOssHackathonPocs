using GptOssHackathonPocs.Core.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace GptOssHackathonPocs.Client.TriageComponents;

public partial class IncidentMap
{
    [Parameter] public IReadOnlyList<Incident>? Incidents { get; set; }
    private readonly string _mapId = $"map-{Guid.NewGuid():N}";
    private bool _ready;
    private bool _hasRendered;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await JS.InvokeVoidAsync("triageMap.init", _mapId);
            _ready = true;
            await PushIncidentsAsync();
            _hasRendered = true;
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if(_hasRendered) 
            await PushIncidentsAsync();
    }

    private async Task PushIncidentsAsync()
    {
        if (!_ready) return;
        var geo = Incidents?.Where(x => !string.IsNullOrWhiteSpace(x.GeoJson))
            .Select(x => x.GeoJson!)
            .ToArray() ?? Array.Empty<string>();
        await JS.InvokeVoidAsync("triageMap.setIncidents", (object)geo);
    }
}