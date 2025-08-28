using System;
using System.Collections.Generic;

namespace AINarrativeSimulator.Components.Models;

// Core models to mirror the React app's types with only the properties used by the UI components.

public class WorldAction
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Type { get; set; } = ""; // speak | move | think | interact | emote | discover
    public string AgentId { get; set; } = "";
    public string? Target { get; set; }
    public string Location { get; set; } = "";
    public string? Visibility { get; set; } // private | location | public
    public string Content { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

//public class WorldState
//{
//    public string CurrentTime { get; set; } = DateTime.Now.ToString("t");
//    public string Weather { get; set; } = "Clear";
//    public List<string> GlobalEvents { get; set; } = [];
//    public List<string> Rumors { get; set; } = [];
//    public Dictionary<string, LocationState> Locations { get; set; } = new();
//}

public class LocationState
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Atmosphere { get; set; } = "";
    public List<string> Occupants { get; set; } = [];
}

public class AgentState
{
    public string AgentId { get; set; } = "";
    public AgentStaticTraits StaticTraits { get; set; } = new();
    public AgentDynamicState DynamicState { get; set; } = new();
    public AgentKnowledgeMemory KnowledgeMemory { get; set; } = new();
}

public class AgentStaticTraits
{
    public string Profession { get; set; } = "";
    public int Age { get; set; }
    public string Description { get; set; } = "";
    public List<string> PersonalityTraits { get; set; } = [];
    public List<string> CoreValues { get; set; } = [];
}

public class AgentDynamicState
{
    public string CurrentMood { get; set; } = "Neutral";
    public string PhysicalLocation { get; set; } = "";
    public int Energy { get; set; } // 0-100
    public int Stress { get; set; } // 0-100
}

public class AgentKnowledgeMemory
{
    public Dictionary<string, RelationshipInfo> Relationships { get; set; } = new();
    public List<AgentMemory> KeyMemories { get; set; } = [];
    public List<string> KnownSecrets { get; set; } = [];
}

public class RelationshipInfo
{
    public double Trust { get; set; } // 0-10
    public string Summary { get; set; } = "";
    public DateTime? LastContact { get; set; }
}

public class AgentMemory
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime When { get; set; } = DateTime.UtcNow;
    public string Title { get; set; } = "";
    public string Detail { get; set; } = "";
    public string? Tag { get; set; } // e.g., discovery, conversation, etc.
}
