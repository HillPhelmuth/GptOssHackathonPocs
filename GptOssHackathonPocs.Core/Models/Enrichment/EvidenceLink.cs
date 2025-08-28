using System.Text.Json.Serialization;

namespace GptOssHackathonPocs.Core.Models.Enrichment;

public sealed record EvidenceLink(
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("url")] string Url);
