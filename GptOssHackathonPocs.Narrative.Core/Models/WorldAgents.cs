using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
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
    public required string AgentId { get; set; }

    [JsonPropertyName("static_traits")]
    public required StaticTraits StaticTraits { get; set; }

    [JsonPropertyName("dynamic_state")]
    public required DynamicState DynamicState { get; set; }

    [JsonPropertyName("knowledge_memory")]
    public required KnowledgeMemory KnowledgeMemory { get; set; }

    [JsonPropertyName("prompt")]
    public required string Prompt { get; set; }
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
    public string GetSystemPrompt(string worldStateDescription = "")
    {
        return $"""
                {Prompt}

                ## Instructions
                
                * Before responding, always start by taking an action. Use the information below, with particular focus on **World State**, to inform your decisions.
                
                * When responding, always begin your response with "From {AgentId}" as the first line.
                
                ## Your Current State

                {DynamicState.ToMarkdown()}

                ## Your Current Knowledge and Relationships

                {KnowledgeMemory.ToMarkdown()}
                
                ## World State
                
                {worldStateDescription}
                """;
    }

    public string UpdateDynamicStatePrompt(string worldStateDescription, string actionDescription)
    {
        return 
            $"""
            {Prompt}
            
            ## Instructions
            
            Update your current mood, short-term goals, long term goals, and location as required by the action you just took and the current world state.
            
            Do not remove any existing goals, just modify existing goals add/or new ones as needed.
            
            ## Your Current State
            
            {DynamicState.ToMarkdown()}
            
            ## Your Current Knowledge and Relationships
            
            {KnowledgeMemory.ToMarkdown()}
            
            ## World State
            
            {worldStateDescription}
            
            ## Action Taken
            
            {actionDescription}
            """;
    }
    public string UpdateKnowledgeMemoryPrompt(string worldStateDescription, string actionDescription)
    {
        return 
            $"""
            {Prompt}
            
            ## Instructions
            
            Update your relationships and key memories as required by the action you just took and the current world state.
            
            Do not remove any existing relationships or memories, just modify existing ones and/or add new ones as needed.
            
            ## Your Current State
            
            {DynamicState.ToMarkdown()}
            
            ## Your Current Knowledge and Relationships
            
            {KnowledgeMemory.ToMarkdown()}
            
            ## World State
            
            {worldStateDescription}
            
            ## Action Taken
            
            {actionDescription}
            """;
    }
}

public class DynamicState
{
    [JsonPropertyName("current_mood")]
    public Mood CurrentMood { get; set; }

    [JsonPropertyName("short_term_goals")]
    public required List<string> ShortTermGoals { get; set; }

    [JsonPropertyName("long_term_goals")]
    public required List<string> LongTermGoals { get; set; }

    [JsonPropertyName("physical_location")]
    public required string PhysicalLocation { get; set; }

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
    public required List<Relationship> Relationships { get; set; }

    [JsonPropertyName("recent_memories")]
    public required List<string> RecentMemories { get; set; }
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
        if (RecentMemories is { Count: > 0 })
        {
            markdownBuilder.AppendLine($"### Key Memories:");
            foreach (var mem in RecentMemories)
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
    public required string Name { get; set; }

    [JsonPropertyName("details")]
    public required Details Details { get; set; }
}

public class Details
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("trust")]
    public long Trust { get; set; }

    [JsonPropertyName("notes")]
    public required string Notes { get; set; }
}

public class StaticTraits
{
    [JsonPropertyName("personality")]
    public required string PersonalityTraits { get; set; }

    [JsonPropertyName("profession")]
    public required string Profession { get; set; }

    [JsonPropertyName("core_values")]
    public required string CoreValues { get; set; }
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
    Restless,
    Violent
}
