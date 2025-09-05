using GptOssHackathonPocs.Narrative.Core.Models;
using GptOssHackathonPocs.Narrative.Core.Services;
using Microsoft.AspNetCore.Components;

namespace AINarrativeSimulator.Components.BeatTicker;

public partial class BeatTicker
{
    //private readonly List<BeatSummary> _beats = new();
    [Parameter] public List<BeatSummary> Beats { get; set; } = [];
    [Inject]
    private IBeatEngine BeatEngine { get; set; } = default!;

    // Updated to use the Mood enum and map directly to CSS class names based on the enum value
    private static string MoodClass(Mood mood) => $"m-{mood.ToString().ToLowerInvariant()}";
}