using GptOssHackathonPocs.Core.Models.Publishing;
using Microsoft.Extensions.Logging;

namespace GptOssHackathonPocs.Core.Services.PublishActions;

public interface IDispatchChannel
{
    Channel Channel { get; }
    Task<ChannelDelivery> SendAsync(DispatchRequest request, IReadOnlyList<Recipient> recipients, CancellationToken ct = default);
}

public interface IDispatchService
{
    Task<DispatchJob> QueueAndSendAsync(DispatchRequest request, CancellationToken ct = default);
    Task<DispatchJob?> GetAsync(Guid jobId);
    Task UpdateDeliveryAsync(Guid jobId, Channel channel, Action<ChannelDelivery> mutate);
}

/// <summary>
/// In-memory job tracker + fan-out to per-channel publishers.
/// </summary>
public sealed class DispatchService : IDispatchService
{
    private readonly IEnumerable<IDispatchChannel> _channels;
    private readonly IRecipientDirectory _directory;
    private readonly ILogger<DispatchService> _log;

    private readonly Dictionary<Guid, DispatchJob> _jobs = new();

    public DispatchService(IEnumerable<IDispatchChannel> channels, IRecipientDirectory directory, ILogger<DispatchService> log)
        => (_channels, _directory, _log) = (channels, directory, log);

    public async Task<DispatchJob> QueueAndSendAsync(DispatchRequest request, CancellationToken ct = default)
    {
        var job = new DispatchJob { JobId = request.JobId, Item = request.Item, CreatedUtc = DateTimeOffset.UtcNow };
        _jobs[job.JobId] = job;

        var targets = await _directory.ResolveAsync(request.Target.Groups, request.Target.GeoJson, ct);
        foreach (var ch in _channels.Where(c => request.Channels.Contains(c.Channel)))
        {
            var d = new ChannelDelivery { Channel = ch.Channel };
            job.Deliveries.Add(d);

            try
            {
                var result = await ch.SendAsync(request, targets, ct);
                d.Provider = result.Provider;
                d.ProviderMessageId = result.ProviderMessageId;
                d.Status = result.Status;
                d.Delivered = result.Delivered;
                d.Failed = result.Failed;
                d.Acknowledged = result.Acknowledged;
                foreach (var e in result.Errors) d.Errors.Add(e);
            }
            catch (Exception ex)
            {
                d.Status = "failed";
                d.Errors.Add(ex.Message);
                _log.LogError(ex, "Channel {Channel} failed for job {JobId}", ch.Channel, job.JobId);
            }
        }
        return job;
    }

    public Task<DispatchJob?> GetAsync(Guid jobId) => Task.FromResult(_jobs.GetValueOrDefault(jobId));

    public Task UpdateDeliveryAsync(Guid jobId, Channel channel, Action<ChannelDelivery> mutate)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            var d = job.Deliveries.FirstOrDefault(x => x.Channel == channel);
            if (d is not null) mutate(d);
        }
        return Task.CompletedTask;
    }
}

/// <summary>
/// Demo channel implementations that just log and succeed. Replace with Twilio/SendGrid/etc.
/// </summary>
public sealed class SmsDemoChannel : IDispatchChannel
{
    private readonly ILogger<SmsDemoChannel> _log;
    public SmsDemoChannel(ILogger<SmsDemoChannel> log) => _log = log;
    public Channel Channel => Channel.SMS;

    public Task<ChannelDelivery> SendAsync(DispatchRequest request, IReadOnlyList<Recipient> recipients, CancellationToken ct = default)
    {
        _log.LogInformation("SMS demo: {Count} recipients for {Title}", recipients.Count, request.Item.Title);
        return Task.FromResult(new ChannelDelivery
        {
            Channel = Channel,
            Provider = "demo",
            ProviderMessageId = Guid.NewGuid().ToString("N"),
            Status = "sent",
            Delivered = recipients.Count,
        });
    }
}

public sealed class EmailDemoChannel : IDispatchChannel
{
    private readonly ILogger<EmailDemoChannel> _log;
    public EmailDemoChannel(ILogger<EmailDemoChannel> log) => _log = log;
    public Channel Channel => Channel.EMAIL;

    public Task<ChannelDelivery> SendAsync(DispatchRequest request, IReadOnlyList<Recipient> recipients, CancellationToken ct = default)
    {
        _log.LogInformation("Email demo: {Count} recipients for {Title}", recipients.Count, request.Item.Title);
        return Task.FromResult(new ChannelDelivery
        {
            Channel = Channel,
            Provider = "demo",
            ProviderMessageId = Guid.NewGuid().ToString("N"),
            Status = "sent",
            Delivered = recipients.Count,
        });
    }
}

public sealed class CapFileChannel : IDispatchChannel
{
    private readonly ILogger<CapFileChannel> _log;
    public CapFileChannel(ILogger<CapFileChannel> log) => _log = log;
    public Channel Channel => Channel.CAP;

    public Task<ChannelDelivery> SendAsync(DispatchRequest request, IReadOnlyList<Recipient> recipients, CancellationToken ct = default)
    {
        // In practice: call your existing CapFilePlanPublisher here.
        _log.LogInformation("CAP demo: wrote CAP XML for {Title}", request.Item.Title);
        return Task.FromResult(new ChannelDelivery
        {
            Channel = Channel,
            Provider = "cap-file",
            ProviderMessageId = Guid.NewGuid().ToString("N"),
            Status = "sent",
        });
    }
}
