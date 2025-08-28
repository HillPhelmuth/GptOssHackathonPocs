using GptOssHackathonPocs.Core.Models;

namespace GptOssHackathonPocs.Core.Services;
public interface IIncidentFeed
{
    Task<IReadOnlyList<Incident>> FetchAsync(CancellationToken ct = default);
}
