using GptOssHackathonPocs.Core.Models.Publishing;
using GptOssHackathonPocs.Core.Services.PublishActions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Twilio.Clients;

namespace GptOssHackathonPocs.Core.Models;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCommsHub(this IServiceCollection services)
    {
        services.AddScoped<ActionQueueState>();
        services.AddSingleton<IRecipientDirectory, InMemoryRecipientDirectory>();
        services.AddSingleton<IDispatchService, DispatchService>();
        services.AddSingleton<IDispatchChannel, SmsDemoChannel>();
        services.AddSingleton<IDispatchChannel, EmailDemoChannel>();
        services.AddSingleton<IDispatchChannel, CapFileChannel>();

        return services;
    }
    public static IServiceCollection AddCommsPublishers(
        this IServiceCollection services, IConfiguration config)
    {
        services.Configure<TwilioPublisherOptions>(config.GetSection("Twilio"));
        services.Configure<SendGridPublisherOptions>(config.GetSection("SendGrid"));

        // Twilio client
        services.AddSingleton<ITwilioRestClient>(sp =>
        {
            var cfg = sp.GetRequiredService<IOptions<TwilioPublisherOptions>>().Value;
            var sid = config["Twilio:AccountSid"]!;
            var token = config["Twilio:AuthToken"]!;
            return new TwilioRestClient(sid, token);
        });

        // Publishers
        services.AddSingleton<IPlanPublisher, TwilioSmsAndVoicePlanPublisher>();
        services.AddSingleton<IPlanPublisher, SendGridEmailPlanPublisher>();

        return services;
    }
}
