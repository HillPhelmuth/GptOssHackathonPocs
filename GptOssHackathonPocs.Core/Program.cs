using GptOssHackathonPocs.Core.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Default client
builder.Services.AddHttpClient("default");

// NWS requires a User-Agent identifying your app/email per their docs.
builder.Services.AddHttpClient("nws", c =>
{
    c.DefaultRequestHeaders.UserAgent.ParseAdd("TriageCopilot/0.1 (+your-email@example.com)");
    c.DefaultRequestHeaders.Accept.ParseAdd("application/geo+json, application/json");
});

builder.Services.AddSingleton<IIncidentFeed, NwsAlertsFeed>();
builder.Services.AddSingleton<IIncidentFeed, UsgsQuakesFeed>();
builder.Services.AddSingleton<IIncidentFeed, NhcStormsFeed>();
builder.Services.AddSingleton<IncidentAggregator>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
