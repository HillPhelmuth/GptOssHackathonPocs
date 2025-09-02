
using GptOssHackathonPocs.Core.Models;
using GptOssHackathonPocs.Core.Models.Publishing;
using GptOssHackathonPocs.Core.Models.StructuredOutput;

namespace GptOssHackathonPocs.Core.Services.PublishActions;

public interface IRecipientDirectory
{
    /// <summary>
    /// Resolve a set of groups (and optional geofence) into concrete recipients.
    /// The implementation may consult GIS and org directories.
    /// </summary>
    Task<IReadOnlyList<Recipient>> ResolveAsync(IReadOnlyList<string> groups, string? geoJson, CancellationToken ct = default);
    Task<IReadOnlyList<Contact>> ResolveAsync(ActionItem item, CancellationToken ct = default);
}