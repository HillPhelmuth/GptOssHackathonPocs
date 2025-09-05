using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.ComponentModel; 

namespace GptOssHackathonPocs.Narrative.Core.Models;

public sealed class BeatSummary
{
    [JsonIgnore]
    public string BeatId { get; set; } = Guid.NewGuid().ToString("N");
    [JsonIgnore]
    public DateTime WindowStartUtc { get; set; }
    [JsonIgnore]
    public DateTime WindowEndUtc { get; set; }
    [JsonIgnore]
    public int SourceActionCount { get; set; }

    [Description("Short headline ≤ 8 words capturing the main event; plain language; no emojis.")]
    public required string Title { get; set; } = "";                 // <= 8 words

    [Description("Dramatization of the given events; no new facts, just a dramatic take on the events.")]
    public required string Dramatization { get; set; }

    [Description("One vivid, natural quote from a participant; omit or leave empty if none fits.")]
    public string KeyQuote { get; set; } = "";              // optional

    [Description("Pick one enum value that best matches the beat’s tone. Use exact enum casing.")]
    public Mood Mood { get; set; }           // one of fixed set

    [Description("Integer 0–100 indicating intensity (0=calm, 100=crisis); choose a sensible value based on events.")]
    public int Tension { get; set; }     // 0–100

    [JsonIgnore]
    public string ContinuityKey { get; set; } = ""; // stable storyline key

    [Description("List of agent names/ids involved in this beat; unique; 1–6 items; use canonical agent names.")]
    public List<string> Participants { get; set; } = []; // agent names

    [Description("Places or areas referenced (e.g., station locations); optional; 0–4 concise entries.")]
    public List<string> Locations { get; set; } = [];    // station areas

    [Description("Short lowercase tags (1–2 words) summarizing themes/actions; 3–6 items; no punctuation.")]
    public List<string> Tags { get; set; } = [];         // short hashtags-ish
}