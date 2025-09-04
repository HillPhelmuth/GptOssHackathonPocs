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



   
    private static string MoodClass(string mood) => mood.ToLowerInvariant() switch
    {
        "tense" => "m-tense",
        "hopeful" => "m-hopeful",
        "anxious" => "m-anxious",
        "determined" => "m-determined",
        "chaotic" => "m-chaotic",
        "solemn" => "m-solemn",
        "jubilant" => "m-jubilant",
        _ => "m-neutral"
    };
}