using System.Text.Json.Serialization;
using GptOssHackathonPocs.Core.Models.StructuredOutput;

namespace GptOssHackathonPocs.Core.Models.Publishing;

/// <summary>
/// Delivery channels supported by the Communications Hub.
/// Keep names aligned with ActionItem.RequiredTools values when possible.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Channel
{
    CAP,        // Common Alerting Protocol (public/IPAWS handoff)
    SMS,
    EMAIL,
    SLACK,
    TEAMS,
    VOICE,      // phone tree escalation
    PUSH        // app/web push
}

public sealed record Recipient(string Name, string? Phone, string? Email, string? ChatId, Channel? PreferredChannel = null);

/// <summary>
/// Targeting for a dispatch. Public broadcasts use geofence; targeted use groups.
/// </summary>
public sealed record DispatchTarget(
    bool IsPublic,
    string? GeoJson,
    IReadOnlyList<string> Groups
);

/// <summary>
/// Request to dispatch a single ActionItem to one or more channels.
/// </summary>
public sealed record DispatchRequest(
    Guid JobId,
    string RequestedBy,
    ActionItem Item,
    IReadOnlyList<Channel> Channels,
    DispatchTarget Target,
    bool RequireAck,
    DateTimeOffset? ScheduledFor,
    TimeSpan? FollowUpIn,
    string? TemplateId
);

public sealed record ChannelDelivery
{
    public required Channel Channel { get; init; }
    public string? Provider { get; set; }
    public string? ProviderMessageId { get; set; }
    public string Status { get; set; } = "queued"; // queued|sent|delivered|failed|acknowledged
    public int Delivered { get; set; }
    public int Failed { get; set; }
    public int Acknowledged { get; set; }
    public List<string> Errors { get; } = [];
}

public sealed class DispatchJob
{
    public required Guid JobId { get; init; }
    public required ActionItem Item { get; init; }
    public required DateTimeOffset CreatedUtc { get; init; }
    public List<ChannelDelivery> Deliveries { get; } = [];
}
