using System.Runtime.Serialization;
using System.Text;
using System.Text.Json.Serialization;

namespace GptOssHackathonPocs.Narrative.Core.Models;

public class WorldAgents
{
    [JsonPropertyName("agents")]
    public List<WorldAgent> Agents { get; set; } = [];

    public static WorldAgents DefaultFromJson()
    {
        var defaultWorldAgents = FileHelper.ExtractFromAssembly<WorldAgents>("WorldAgents.json");
        return defaultWorldAgents;
    }
}

public class WorldAgent
{
    [JsonPropertyName("agent_id")]
    public string AgentId { get; set; }

    [JsonPropertyName("static_traits")]
    public StaticTraits StaticTraits { get; set; }

    [JsonPropertyName("dynamic_state")]
    public DynamicState DynamicState { get; set; }

    [JsonPropertyName("knowledge_memory")]
    public KnowledgeMemory KnowledgeMemory { get; set; }

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; }
    public string? Notes { get; private set; }
    public DateTime LastUpdate { get; set; }

    public void AddNotes(string notes)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(Notes))
        {
            sb.AppendLine(Notes);
            sb.AppendLine();
        }

        sb.AppendLine($"New note added on {DateTime.Now.ToString("g")}");
        sb.AppendLine(notes);
        Notes = sb.ToString();
    }
    public string GetSystemPrompt()
    {
        return $"""
                {Prompt}

                ## Instructions
                
                * Before responding, always update your dynamic state and knowledge memory below by invoking `WorldAgentsPlugin-UpdateAgentState` and `WorldAgentsPlugin-UpdateAgentMemory` tool functions.
                
                * When responding, always begin your response with "From {AgentId}" as the first line.
                
                ## Current State

                {DynamicState.ToMarkdown()}

                ## Current Knowledge and Relationships

                {KnowledgeMemory.ToMarkdown()}
                """;
    }
}

public class DynamicState
{
    [JsonPropertyName("current_mood")]
    public Mood CurrentMood { get; set; }

    [JsonPropertyName("short_term_goals")]
    public List<string> ShortTermGoals { get; set; }

    [JsonPropertyName("long_term_goals")]
    public List<string> LongTermGoals { get; set; }

    [JsonPropertyName("physical_location")]
    public string PhysicalLocation { get; set; }

    public string ToMarkdown()
    {
        var markdownBuilder = new StringBuilder();
        markdownBuilder.AppendLine($"- Current Mood: {CurrentMood}");
        markdownBuilder.AppendLine($"- Physical Location: {PhysicalLocation}");
        if (ShortTermGoals is { Count: > 0 })
        {
            markdownBuilder.AppendLine($"- Short Term Goals:");
            foreach (var goal in ShortTermGoals)
            {
                markdownBuilder.AppendLine($"  - {goal}");
            }
        }
        if (LongTermGoals is { Count: > 0 })
        {
            markdownBuilder.AppendLine($"- Long Term Goals:");
            foreach (var goal in LongTermGoals)
            {
                markdownBuilder.AppendLine($"  - {goal}");
            }
        }
        return markdownBuilder.ToString();
    }
}

public class KnowledgeMemory
{
    [JsonPropertyName("relationships")]
    public List<Relationship> Relationships { get; set; }

    [JsonPropertyName("key_memories")]
    public List<string> KeyMemories { get; set; }
    public string ToMarkdown()
    {
        var markdownBuilder = new StringBuilder();
        if (Relationships is { Count: > 0 })
        {
            markdownBuilder.AppendLine($"### Relationships:");
            foreach (var rel in Relationships)
            {
                markdownBuilder.AppendLine($"- Name: {rel.Name}");
                markdownBuilder.AppendLine($"  - Type: {rel.Details.Type}");
                markdownBuilder.AppendLine($"  - Trust Level: {rel.Details.Trust}/100");
                if (!string.IsNullOrWhiteSpace(rel.Details.Notes))
                {
                    markdownBuilder.AppendLine($"  - Notes: {rel.Details.Notes}");
                }
            }
        }
        if (KeyMemories is { Count: > 0 })
        {
            markdownBuilder.AppendLine($"### Key Memories:");
            foreach (var mem in KeyMemories)
            {
                markdownBuilder.AppendLine($"- {mem}");
            }
        }
        return markdownBuilder.ToString();
    }
}

public class Relationship
{
    [JsonPropertyName("Name")]
    public string Name { get; set; }

    [JsonPropertyName("details")]
    public Details Details { get; set; }
}

public class Details
{
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("trust")]
    public long Trust { get; set; }

    [JsonPropertyName("notes")]
    public string Notes { get; set; }
}

public class StaticTraits
{
    [JsonPropertyName("personality")]
    public List<string> Personality { get; set; }

    [JsonPropertyName("profession")]
    public string Profession { get; set; }

    [JsonPropertyName("core_values")]
    public List<string> CoreValues { get; set; }
}
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Mood
{
    Anxious,
    Suspicious,
    Tired,
    Optimistic,
    Irritated,
    Hopeful,
    Resolute,
    Determined,
    Nostalgic,
    Frustrated,
    Hungry,
    Concerned,
    Confident,
    Busy,
    Vigilant,
    BurnedOut, // "burned out" becomes "BurnedOut"
    Amused,
    Measured,
    Energetic,
    Restless
}
