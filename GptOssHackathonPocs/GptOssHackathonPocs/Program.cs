using Blazored.LocalStorage;
using GptOssHackathonPocs;
using GptOssHackathonPocs.Components;
using GptOssHackathonPocs.Core;
using GptOssHackathonPocs.Core.Hubs;
using GptOssHackathonPocs.Core.Models;
using GptOssHackathonPocs.Core.Models.Enrichment;
using GptOssHackathonPocs.Core.Services;
using GptOssHackathonPocs.Narrative.Core;
using GptOssHackathonPocs.Narrative.Core.Services;
using Microsoft.Extensions.Azure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();
builder.Services.AddSignalR(o =>
{
    o.MaximumReceiveMessageSize = null;
});
builder.Services.AddHttpClient("nws", c =>
{
    c.DefaultRequestHeaders.UserAgent.ParseAdd("TriageCopilot/0.1 (+your-email@example.com)");
    c.DefaultRequestHeaders.Accept.ParseAdd("application/geo+json, application/json");
});

builder.Services.AddSingleton<IIncidentFeed, NwsAlertsFeed>();
builder.Services.AddSingleton<IIncidentFeed, UsgsQuakesFeed>();
builder.Services.AddSingleton<IIncidentFeed, NhcStormsFeed>();
builder.Services.AddSingleton<IIncidentFeed, FirmsActiveFiresFeed>();
builder.Services.AddSingleton<IncidentAggregator>();
builder.Services.AddTriageEnrichment();
//builder.Services.AddHttpClient<WorldPopPopulationIndex>();
//builder.Services.AddSingleton(await EarthEngineRestClient.CreateAsync("openaiosshackathonEarth", @"C:\Users\adamh\source\repos\GptOssHackathonPocs\GptOssHackathonPocs.Core\searchwithsemantickernel-a3b6f34079f4.json"));
//builder.Services.AddSingleton<IPopulationIndex, WorldPopPopulationIndex>();
// Narrative services
builder.Services.AddSingleton<WorldState>();
builder.Services.AddScoped<AiAgentOrchestration>();
builder.Services.AddScoped<INarrativeOrchestration, NarrativeOrchestration>();
builder.Services.AddScoped<IBeatEngine, BeatEngine>();
// Program.cs (additions)
builder.Services.AddHttpClient<NwsGeometryResolver>("nws", c =>
{
    c.DefaultRequestHeaders.UserAgent.ParseAdd("TriageCopilot/0.1 (+your-email@example.com)");
    c.DefaultRequestHeaders.Accept.ParseAdd("application/geo+json, application/json");
});


// Program.cs
builder.Services.AddMemoryCache(o =>
{
    // We'll use "1 unit per cached geometry", so this is a rough max entry count.
    o.SizeLimit = 1000; // tweak as you like
});
builder.Services.AddAzureClients(clientBuilder =>
{
    AzureClientFactoryBuilderExtensions.AddBlobServiceClient(clientBuilder, builder.Configuration["StorageConnection:blobServiceUri"]!).WithName("StorageConnection");
    
});
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddCommsHub();
builder.Services.AddCommsPublishers(builder.Configuration);
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(GptOssHackathonPocs.Client._Imports).Assembly);

app.MapCommsHubWebhooks();
app.Run();
