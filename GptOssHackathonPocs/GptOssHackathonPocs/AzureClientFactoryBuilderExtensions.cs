using Azure.Core.Extensions;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using GptOssHackathonPocs.Core.Hubs;
using GptOssHackathonPocs.Core.Services.PublishActions;
using Microsoft.Extensions.Azure;

namespace GptOssHackathonPocs;

internal static class AzureClientFactoryBuilderExtensions
{
    public static IAzureClientBuilder<BlobServiceClient, BlobClientOptions> AddBlobServiceClient(this AzureClientFactoryBuilder builder, string serviceUriOrConnectionString, bool preferMsi = true)
    {
        if (preferMsi && Uri.TryCreate(serviceUriOrConnectionString, UriKind.Absolute, out Uri? serviceUri))
        {
            return builder.AddBlobServiceClient(serviceUri);
        }
        else
        {
            return BlobClientBuilderExtensions.AddBlobServiceClient(builder, serviceUriOrConnectionString);
        }
    }

    public static IAzureClientBuilder<QueueServiceClient, QueueClientOptions> AddQueueServiceClient(this AzureClientFactoryBuilder builder, string serviceUriOrConnectionString, bool preferMsi = true)
    {
        if (preferMsi && Uri.TryCreate(serviceUriOrConnectionString, UriKind.Absolute, out Uri? serviceUri))
        {
            return builder.AddQueueServiceClient(serviceUri);
        }
        else
        {
            return QueueClientBuilderExtensions.AddQueueServiceClient(builder, serviceUriOrConnectionString);
        }
    }

    public static IAzureClientBuilder<TableServiceClient, TableClientOptions> AddTableServiceClient(this AzureClientFactoryBuilder builder, string serviceUriOrConnectionString, bool preferMsi = true)
    {
        if (preferMsi && Uri.TryCreate(serviceUriOrConnectionString, UriKind.Absolute, out Uri? serviceUri))
        {
            return builder.AddTableServiceClient(serviceUri);
        }
        else
        {
            return TableClientBuilderExtensions.AddTableServiceClient(builder, serviceUriOrConnectionString);
        }
    }
}

internal static class AddWebHookExtensions
{
    public static IEndpointRouteBuilder MapCommsHubWebhooks(this IEndpointRouteBuilder app)
    {
        // Twilio message status webhook (form-encoded)
        app.MapPost("/webhooks/twilio/status", async (HttpContext ctx, IDispatchService dispatch) =>
        {
            // Typically contains MessageSid, MessageStatus, To, From, etc.
            var form = await ctx.Request.ReadFormAsync();
            var sid = form["MessageSid"].ToString();
            var status = form["MessageStatus"].ToString(); // queued|sent|delivered|undelivered|failed

            // For demo, we cannot map sid->job/channel without storage. In a real impl, store JobId+Channel+Sid.
            // Here we simply acknowledge.
            return Results.Ok(new { ok = true, sid, status });
        });

        // SendGrid event webhook (JSON array)
        app.MapPost("/webhooks/sendgrid/events", async (HttpContext ctx) =>
        {
            using var reader = new StreamReader(ctx.Request.Body);
            var body = await reader.ReadToEndAsync();
            // Parse & correlate to DispatchJob here.
            return Results.Ok(new { ok = true });
        });

        app.MapHub<ActionPlansHub>("/actionPlans");
        return app;
    }
}
