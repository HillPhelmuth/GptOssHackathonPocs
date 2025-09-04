using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace GptOssHackathonPocs.Narrative.Core.Models;

/// <summary>
/// Represents an action performed by a world agent, including the action type, target, and optional details.
/// </summary>
[Description("Represents a world agent action, including type, target, and optional details.")]
public class WorldAgentAction
{
    [JsonIgnore]
    public string? ActingAgent { get; set; }
    [JsonIgnore]
    public string? BriefDescription { get; set; }
    [JsonIgnore]
    public string? AgentImagePath { get; set; }
    /// <summary>
    /// The kind of action to perform.
    /// </summary>
    [Description("Action type (e.g., SpeakTo, MoveTo, Decide).")]
    [JsonPropertyName("type")]
    public required ActionType Type { get; set; }

    /// <summary>
    /// The entity or location the action is directed at (e.g., character name or location).
    /// </summary>
    [Description("The target of the action, such as a character name or a location.")]
    [JsonPropertyName("target")]
    public string? Target { get; set; } // e.g., character name or location

    /// <summary>
    /// Optional extra information for the action, such as dialogue content or decision rationale.
    /// </summary>
    [Description("Details for the action (e.g., what to say or decide).")]
    [JsonPropertyName("details")]
    public required string Details { get; set; } // e.g., what to say or decide

    public DateTime Timestamp { get; set; } = DateTime.Now;

    public (ActionType, string) ToTypeMarkdown()
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(Target))
        {
            sb.AppendLine($"- **Target:** {Target}");
        }
        sb.AppendLine($"- **Details:** {Details}");
        sb.AppendLine($"- **Timestamp:** {Timestamp:u}");
        return (Type, sb.ToString());
    }
}
public class UpdateAgentStateRequest
{
    [Description("Description of the update")]
    public required string Description { get; set; }
    [Description("The updated agent dynamic state")]
    public required DynamicState UpdatedDynamicState { get; set; }
}
public class UpdateAgentMemoryRequest
{
    [Description("Description of the update")]
    public required string Description { get; set; }
    [Description("The updated agent knowledge memory and relationships")]
    public required KnowledgeMemory UpdatedKnowledgeMemory { get; set; }
}
/// <summary>
/// Enumerates the possible world agent actions.
/// </summary>
[Description("World agent action types.")]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ActionType
{
    None,
    Error,
    [Description("Speak to a target character.")]
    SpeakTo,
    [Description("Move to a target location.")]
    MoveTo,
    [Description("Make a decision (no direct target interaction required).")]
    Decide,
    [Description("Discover something new in the environment.")]
    Discover,
    [Description("Trade or purchase items from a target.")]
    Purchase,
    [Description("Engage in a combat or hostile action.")]
    Attack,
    [Description("Defend against an attack or threat.")]
    Defend,
    [Description("Flee from a dangerous situation or threat.")]
    Flee,
    [Description("Undermine or sabotage a target.")]
    Undermine,
}
/// <summary>
/// Represents the primary, top-level locations within the world of Vespera.
/// </summary>
[Description("Primary, top-level locations within Vespera.")]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum VesperaLocation
{
    /// <summary>
    /// Central command spire of Vespera Station, home to the Overseer AI and war room.
    /// </summary>
    [Description("Central command spire of Vespera Station, home to the Overseer AI and war room.")]
    CommandSpire,

    /// <summary>
    /// Primary orbital docking ring for starships; customs and cargo cranes hum day and night-cycle.
    /// </summary>
    [Description("Primary orbital docking ring for starships; customs and cargo cranes hum day and night-cycle.")]
    DockingRing,

    /// <summary>
    /// Advanced xenomedical bay specializing in multi-species trauma and nano-surgery.
    /// </summary>
    [Description("Advanced xenomedical bay specializing in multi-species trauma and nano-surgery.")]
    XenomedBay,

    /// <summary>
    /// Cadet academy where pilots, navigators, and tacticians are forged.
    /// </summary>
    [Description("Cadet academy where pilots, navigators, and tacticians are forged.")]
    CadetAcademy,

    /// <summary>
    /// Heavy mech hangar and drone maintenance bay for exo-frames and hull crawlers.
    /// </summary>
    [Description("Heavy mech hangar and drone maintenance bay for exo-frames and hull crawlers.")]
    MechHangar,

    /// <summary>
    /// Neon-lit cantina favored by smugglers, couriers, and off-shift riggers.
    /// </summary>
    [Description("Neon-lit cantina favored by smugglers, couriers, and off-shift riggers.")]
    NebulaCantina,

    /// <summary>
    /// Station security precinct with holding cells and a pervasive surveillance hub.
    /// </summary>
    [Description("Station security precinct with holding cells and a pervasive surveillance hub.")]
    SecurityPrecinct,

    /// <summary>
    /// Archive of derelicts and relics from lost expeditions, curated by void-salvagers.
    /// </summary>
    [Description("Archive of derelicts and relics from lost expeditions, curated by void-salvagers.")]
    DerelictArchive,

    /// <summary>
    /// Quantum anomalies research lab pushing at the edges of time and causality.
    /// </summary>
    [Description("Quantum anomalies research lab pushing at the edges of time and causality.")]
    QuantumResearchLab,

    /// <summary>
    /// HoloPress bureau broadcasting systemwide feeds and encrypted whistlecasts.
    /// </summary>
    [Description("HoloPress bureau broadcasting systemwide feeds and encrypted whistlecasts.")]
    HoloPressBureau,

    /// <summary>
    /// Biofabrication dispensary for gene-meds, nano-tonics, and custom antigens.
    /// </summary>
    [Description("Biofabrication dispensary for gene-meds, nano-tonics, and custom antigens.")]
    BiofabDispensary,

    /// <summary>
    /// Active assembly yard for terraforming modules and prefabs bound for frontier worlds.
    /// </summary>
    [Description("Active assembly yard for terraforming modules and prefabs bound for frontier worlds.")]
    TerraformingYard,

    /// <summary>
    /// Cybernetic atelier crafting bespoke augments and sensate synth-sculptures.
    /// </summary>
    [Description("Cybernetic atelier crafting bespoke augments and sensate synth-sculptures.")]
    CyberneticAtelier,

    /// <summary>
    /// Starlight taproom pouring comet-ice brews and vacuum-aged spirits.
    /// </summary>
    [Description("Starlight taproom pouring comet-ice brews and vacuum-aged spirits.")]
    StarlightTaproom,

    /// <summary>
    /// Protein vatworks where algae and myco-cultures become ration bricks and gourmet gels.
    /// </summary>
    [Description("Protein vatworks where algae and myco-cultures become ration bricks and gourmet gels.")]
    ProteinVatworks,

    /// <summary>
    /// Grand agora concourse for markets, rallies, and stationwide assemblies.
    /// </summary>
    [Description("Grand agora concourse for markets, rallies, and stationwide assemblies.")]
    AgoraConcourse,

    /// <summary>
    /// Shadowed underlevel conduit threading maintenance shafts and forgotten bulkheads.
    /// </summary>
    [Description("Shadowed underlevel conduit threading maintenance shafts and forgotten bulkheads.")]
    UnderlevelConduit
}
public static class EnumHelpers
{
    public static string ToDescriptionString<T>(this T val) where T : Enum
    {
        var fi = val.GetType().GetField(val.ToString());
        var attributes = (DescriptionAttribute[])fi.GetCustomAttributes(typeof(DescriptionAttribute), false);
        return attributes.Length > 0 ? attributes[0].Description : val.ToString();
    }
    public static Dictionary<T, string> AllEnumDescriptions<T>() where T : Enum
    {
        // Get all values of the enum
        var enumType = typeof(T);
        if (!enumType.IsEnum)
            throw new ArgumentException("T must be an enumerated type");
        var values = Enum.GetValues(enumType).Cast<T>();
        // Convert each value to its description
        return values.ToDictionary(v => v, v => v.ToDescriptionString());

    }
}