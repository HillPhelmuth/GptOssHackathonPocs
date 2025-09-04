using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GptOssHackathonPocs.Narrative.Core.Models;

public sealed class BeatSummary
{
    public string BeatId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime WindowStartUtc { get; set; }
    public DateTime WindowEndUtc { get; set; }
    public int SourceActionCount { get; set; }

    public string Title { get; set; } = "";                 // <= 8 words
    public string Summary { get; set; } = "";               // 2–3 sentences
    public string KeyQuote { get; set; } = "";              // optional
    public string Mood { get; set; } = "neutral";           // one of fixed set
    public int Tension { get; set; }                        // 0–100
    public string ContinuityKey { get; set; } = "";         // stable storyline key
    public List<string> Participants { get; set; } = new(); // agent names
    public List<string> Locations { get; set; } = new();    // station areas
    public List<string> Tags { get; set; } = new();         // short hashtags-ish
}