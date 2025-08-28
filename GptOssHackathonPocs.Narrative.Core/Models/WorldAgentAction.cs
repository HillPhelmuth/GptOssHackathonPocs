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

    public DateTime Timestamp { get; set; }

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

/// <summary>
/// Enumerates the possible world agent actions.
/// </summary>
[Description("World agent action types.")]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ActionType
{
    /// <summary>
    /// Speak to a target character.
    /// </summary>
    [Description("Speak to a target character.")]
    SpeakTo,

    /// <summary>
    /// Move to a target location.
    /// </summary>
    [Description("Move to a target location.")]
    MoveTo,

    /// <summary>
    /// Make a decision without direct interaction with a specific target.
    /// </summary>
    [Description("Make a decision (no direct target interaction required).")]
    Decide,
    [Description("Express an emotion or reaction.")]
    Emote,
    [Description("Discover something new in the environment.")]
    Discover,
}
/// <summary>
/// Represents the primary, top-level locations within the world of Pineharbor.
/// </summary>
[Description("Primary, top-level locations within Pineharbor.")]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PineharborLocation
{
    /// <summary>
    /// Town Hall of Pineharbor.
    /// </summary>
    [Description("Town Hall of Pineharbor.")]
    TownHall,

    /// <summary>
    /// Public pier and docks.
    /// </summary>
    [Description("Public pier and docks.")]
    Pier,

    /// <summary>
    /// The local medical clinic.
    /// </summary>
    [Description("Local medical clinic.")]
    Clinic,

    /// <summary>
    /// The town's high school.
    /// </summary>
    [Description("The town's high school.")]
    HighSchool,

    /// <summary>
    /// Auto repair garage.
    /// </summary>
    [Description("Auto repair garage.")]
    Garage,

    /// <summary>
    /// Local cafe.
    /// </summary>
    [Description("Local cafe.")]
    Cafe,

    /// <summary>
    /// Sheriff's Office and holding cells.
    /// </summary>
    [Description("Sheriff's Office.")]
    SheriffsOffice,

    /// <summary>
    /// Shipwreck Museum.
    /// </summary>
    [Description("Shipwreck Museum.")]
    ShipwreckMuseum,

    /// <summary>
    /// Marine research station.
    /// </summary>
    [Description("Marine research station.")]
    ResearchStation,

    /// <summary>
    /// Gazette newspaper office.
    /// </summary>
    [Description("Gazette newspaper office.")]
    GazetteOffice,

    /// <summary>
    /// Local pharmacy.
    /// </summary>
    [Description("Local pharmacy.")]
    Pharmacy,

    /// <summary>
    /// Active construction site.
    /// </summary>
    [Description("Active construction site.")]
    ConstructionSite,

    /// <summary>
    /// Artisan craft shop.
    /// </summary>
    [Description("Artisan craft shop.")]
    CraftShop,

    /// <summary>
    /// Local pub.
    /// </summary>
    [Description("Local pub.")]
    Pub,

    /// <summary>
    /// Fish processing facility.
    /// </summary>
    [Description("Fish processing facility.")]
    FishProcessing,

    /// <summary>
    /// Community center for events and gatherings.
    /// </summary>
    [Description("Community center for events and gatherings.")]
    CommunityCenter,

    /// <summary>
    /// Alley located in the downtown area.
    /// </summary>
    [Description("Alley located in the downtown area.")]
    DowntownAlley
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