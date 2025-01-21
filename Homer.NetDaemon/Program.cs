using System.Reflection;
using System.Text.Json;
using Homer.NetDaemon.Apps.Daikin;
using Homer.NetDaemon.Apps.Kdk;
using Homer.NetDaemon.Apps.Remotes;
using Homer.NetDaemon.Components;
using Homer.NetDaemon.Entities;
using Homer.NetDaemon.Options;
using Homer.ServiceDefaults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NetDaemon.AppModel;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.Extensions.Tts;
using NetDaemon.Runtime;
using Refit;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseNetDaemonAppSettings();
builder.Host.UseNetDaemonRuntime();
builder.Host.UseNetDaemonTextToSpeech();
builder.Host.UseNetDaemonMqttEntityManagement();

builder.Services.AddOptions<DaikinOptions>()
    .Bind(builder.Configuration.GetSection("Daikin"))
    .Validate(options => !string.IsNullOrWhiteSpace(options.Username) && !string.IsNullOrWhiteSpace(options.Password))
    .ValidateOnStart();

builder.Services.AddOptions<KdkOptions>()
    .Bind(builder.Configuration.GetSection("Kdk"))
    .Validate(options => !string.IsNullOrWhiteSpace(options.RefreshToken))
    .Validate(options => !string.IsNullOrWhiteSpace(options.ApiKey))
    .ValidateOnStart();

builder.Services.AddSerilog((services, lc) => lc
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console()
);

builder.Services.AddRefitClient<IDaikinApi>()
    .ConfigureHttpClient((sp, client) => { client.BaseAddress = new Uri("https://appdaikin.ez1.cloud:8443"); })
    .AddHttpMessageHandler<DaikinAuthorizationDelegatingHandler>();

builder.Services.AddRefitClient<IKdkAuthApi>()
    .ConfigureHttpClient((sp, client) => { client.BaseAddress = new Uri("https://authglb.digital.panasonic.com"); });

builder.Services.AddRefitClient<IKdkApi>()
    .ConfigureHttpClient((sp, client) =>
    {
        client.BaseAddress = new Uri("https://prod.mycfan.pgtls.net");
        client.DefaultRequestHeaders.Add("User-Agent", "Ceiling Fan/1.1.0 (iPhone; iOS 18.0.1; Scale/3.00)");
        client.DefaultRequestHeaders.Add("X-Api-Key", sp.GetRequiredService<IOptions<KdkOptions>>().Value.ApiKey);
    })
    .AddHttpMessageHandler<KdkAuthorizationDelegatingHandler>()
    .AddHttpMessageHandler<KdkTimestampDelegatingHandler>();

builder.Services.AddAppsFromAssembly(Assembly.GetExecutingAssembly());
builder.Services.AddNetDaemonStateManager();
builder.Services.AddNetDaemonScheduler();
builder.Services.AddHomeAssistantGenerated();

builder.AddServiceDefaults();

builder.Services.AddProblemDetails();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IrRemoteChannel>();
builder.Services.AddSingleton<LivingRoomPresetService>();
builder.Services.AddTransient<DaikinAuthorizationDelegatingHandler>();
builder.Services.AddTransient<KdkTimestampDelegatingHandler>();
builder.Services.AddTransient<KdkAuthorizationDelegatingHandler>();

builder.Services.AddServerSideBlazor();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddRazorPages();

builder.Services.AddAntiforgery(options =>
{
    options.SuppressXFrameOptionsHeader = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAntiforgery();

app.MapDefaultEndpoints();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapGet("/loaderio-ce0e0bd11b62d5ea48a4f5998c69599f", () => "loaderio-ce0e0bd11b62d5ea48a4f5998c69599f");

app.MapGet("/gc", () =>
{
    var info = GC.GetGCMemoryInfo();
    var total = GC.GetTotalMemory(false);
    var totalAlloc = GC.GetTotalAllocatedBytes();

    return new
    {
        info.Generation,
        info.Compacted,
        info.Concurrent,
        total,
        totalAlloc,
        info.PromotedBytes,
        info.HeapSizeBytes,
        info.MemoryLoadBytes,
        info.TotalCommittedBytes,
        info.TotalAvailableMemoryBytes,
        info.HighMemoryLoadThresholdBytes
    };
});

app.MapPost("/contact/qg",
    ([FromQuery] string content, NotifyServices notifyServices) =>
    {
        notifyServices.MobileAppQinsIphone(content, "Message from the internet");
    });

app.Run();