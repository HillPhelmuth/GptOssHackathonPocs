using GptOssHackathonPocs.Narrative.Core;
using GptOssHackathonPocs.Narrative.Core.Models;
using Microsoft.AspNetCore.Components;

namespace AINarrativeSimulator.Components;

public partial class AgentInspector
{
    [Parameter] public IEnumerable<WorldAgent> Agents { get; set; } = [];
    [Parameter] public string? SelectedAgentId { get; set; }
    [Parameter] public EventCallback<string> SelectedAgentIdChanged { get; set; }
    [Parameter] public string? Class { get; set; }
    private string _activeTab = "overview"; // overview | relationships | memories | goals
    [Inject]
    private WorldState WorldStateService { get; set; } = default!;

    private string _lastUpdatedAgentId = "";

    protected override Task OnInitializedAsync()
    {
        WorldStateService.LastAgentUpdated += HandleLastAgentUpdated;
        return base.OnInitializedAsync();
    }

    private void HandleLastAgentUpdated(string obj)
    {
        _lastUpdatedAgentId = obj;
    }

    private WorldAgent? Current => string.IsNullOrWhiteSpace(SelectedAgentId)
        ? null
        : Agents.FirstOrDefault(a => a.AgentId == SelectedAgentId);

    private Task SelectAgentAsync(string id) => SelectedAgentIdChanged.InvokeAsync(id);

    // Updated to use Mood enum instead of string
    private string GetMoodBadge(Mood mood) => mood switch
    {
        Mood.Concerned => "text-orange-400 bg-orange-500-20",
        Mood.Suspicious => "text-red-500 bg-red-500-20",
        Mood.Anxious => "text-yellow-500 bg-yellow-500-20",
        Mood.Tired => "text-gray-400 bg-gray-500-20",
        Mood.Optimistic => "text-green-400 bg-green-500-20",
        Mood.Irritated => "text-red-400 bg-red-500-20",
        Mood.Hopeful => "text-blue-400 bg-blue-500-20",
        Mood.Resolute => "text-blue-500 bg-blue-500-20",
        Mood.Determined => "text-blue-400 bg-blue-500-20",
        Mood.Nostalgic => "text-purple-400 bg-purple-500-20",
        Mood.Frustrated => "text-red-500 bg-red-500-20",
        Mood.Hungry => "text-orange-500 bg-orange-500-20",
        Mood.Confident => "text-green-500 bg-green-500-20",
        Mood.Busy => "text-gray-500 bg-gray-500-20",
        Mood.Vigilant => "text-red-400 bg-red-500-20",
        Mood.BurnedOut => "text-gray-500 bg-gray-500-20",
        Mood.Amused => "text-yellow-400 bg-yellow-500-20",
        Mood.Measured => "text-slate-400 bg-slate-500-20",
        Mood.Energetic => "text-green-500 bg-green-500-20",
        Mood.Restless => "text-purple-400 bg-purple-500-20",
        Mood.Violent => "text-red-600 bg-red-500-20",
        _ => "text-gray-400 bg-gray-500-20"
    };

    private string EnergyBar(int v) => v > 70 ? "bg-green-500" : v > 40 ? "bg-yellow-500" : "bg-red-500";
    private string StressBar(int v) => v > 60 ? "bg-red-500" : v > 30 ? "bg-yellow-500" : "bg-green-500";
    private string TrustText(double t) => t > 7 ? "text-green-400" : t > 4 ? "text-yellow-400" : "text-red-400";
}