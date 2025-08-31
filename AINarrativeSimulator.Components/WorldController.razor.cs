using GptOssHackathonPocs.Narrative.Core;
using Microsoft.AspNetCore.Components;

namespace AINarrativeSimulator.Components;

public partial class WorldController
{
    [Parameter] public bool IsRunning { get; set; }
    [Parameter] public EventCallback OnStart { get; set; }
    [Parameter] public EventCallback OnStop { get; set; }
    [Parameter] public EventCallback OnReset { get; set; }
    [Parameter] public EventCallback<string> InjectRumor { get; set; }
    [Parameter] public EventCallback<string> InjectEvent { get; set; }
    [Parameter] public WorldState WorldState { get; set; } = new();
    [Parameter] public string? Class { get; set; }
    private string rumorText = string.Empty;
    private string eventText = string.Empty;
    private string activeTab = "inject"; // inject | world

    private readonly string[] presetRumors =
    [
        "Dock crews whisper the Overseer AI throttled power to favor Asterion bays",
        "Phase jitter was spotted around the siphon array at the Terraforming Yard",
        "Security quietly pulled contraband augments from the Cybernetic Atelier",
        "A sealed log in the Derelict Archive mentions a hidden armistice clause",
        "Nebula Cantina regulars say an underlevel courier holds corporate access codes"
    ];

    private readonly string[] presetEvents =
    [
        "Micro-meteor sandblast forces pressure-door inspections across the Docking Ring",
        "Stationwide power ration drops as the Command Spire diverts load to the siphon array",
        "Licensed protest fills the Agora Concourse over priority berths; Security sets de-escalation lines",
        "Xenomed Bay issues a contamination alert; Biofab initiates a quiet reagent recall",
        "Quantum Research Lab reports a jitter spike near the Terraforming Yard; build paused for mitigation"
    ];

    private async Task HandleInjectRumorAsync()
    {
        var text = rumorText?.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            await InjectRumor.InvokeAsync(text);
            rumorText = string.Empty;
        }
    }

    private async Task HandleInjectEventAsync()
    {
        var text = eventText?.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            await InjectEvent.InvokeAsync(text);
            eventText = string.Empty;
        }
    }

    private Task ToggleRunAsync() => IsRunning ? OnStop.InvokeAsync() : OnStart.InvokeAsync();
    private Task ResetAsync() => OnReset.InvokeAsync();
    private void ShowInjectTab() => activeTab = "inject";
    private void ShowWorldTab() => activeTab = "world";
    private Task QuickRumorAsync(string text) => InjectRumor.InvokeAsync(text);
    private Task QuickEventAsync(string text) => InjectEvent.InvokeAsync(text);
}