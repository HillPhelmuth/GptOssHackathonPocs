using AINarrativeSimulator.Components.Models;
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
        "Strange lights were seen near the old mine last night",
        "Someone found an old journal hidden in the library basement",
        "The mining company executives are planning to return to town",
        "A mysterious figure was spotted walking through the town square at midnight",
        "Old Mr. Henderson's diary mentions a secret passage in the mine"
    ];

    private readonly string[] presetEvents =
    [
        "A sudden thunderstorm rolls in over Pineharbor",
        "The town's power goes out for several hours",
        "A stranger arrives on the evening train",
        "The old mine entrance gate is found open",
        "A time capsule is discovered during construction work"
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