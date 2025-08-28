using GptOssHackathonPocs.Core.Models;

namespace GptOssHackathonPocs.Core.Services;

public interface IActionPlanPublisher
{
    Task PublishAsync(ActionItem plan, CancellationToken ct = default);
}

public interface IPlanPublisher
{
    Task PublishAsync(ActionItem plan, CancellationToken ct = default);
}
