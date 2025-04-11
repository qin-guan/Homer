using System.Reflection;
using Homer.NetDaemon.Apps.Kdk;
using Homer.NetDaemon.Components;
using Homer.NetDaemon.Entities;
using Homer.NetDaemon.Options;
using Homer.NetDaemon.Services;
using Homer.NetDaemon.Services.DataMall;
using Homer.NetDaemon.Services.DgsForecast;
using Homer.ServiceDefaults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NetDaemon.AppModel;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.Extensions.Tts;
using NetDaemon.Runtime;

// Log.Logger = new LoggerConfiguration()
//     .WriteTo.Console()
//     .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseNetDaemonAppSettings();
builder.Host.UseNetDaemonRuntime();
builder.Host.UseNetDaemonTextToSpeech();
builder.Host.UseNetDaemonMqttEntityManagement();

builder.Services.AddOptions<KdkOptions>()
    .Bind(builder.Configuration.GetSection("Kdk"));

builder.Services.AddOptions<DataMallOptions>()
    .Bind(builder.Configuration.GetSection("DataMall"));

builder.Services.AddOptions<GoogleHomeDashboardOptions>()
    .Bind(builder.Configuration.GetSection("GoogleHomeDashboard"));

// builder.Services.AddSerilog((services, lc) => lc
//     .ReadFrom.Configuration(builder.Configuration)
//     .ReadFrom.Services(services)
//     .Enrich.FromLogContext()
//     .WriteTo.Console()
// );

builder.Services.AddHttpApi<IDataMallApi>()
    .ConfigureHttpApi(options => { options.HttpHost = new Uri("https://datamall2.mytransport.sg"); })
    .ConfigureHttpClient((sp, client) =>
    {
        client.DefaultRequestHeaders.Add("AccountKey",
            sp.GetRequiredService<IOptions<DataMallOptions>>().Value.AccountKey);
    });

builder.Services.AddHttpApi<IDgsForecast>()
    .ConfigureHttpApi(options => { options.HttpHost = new Uri("https://api-open.data.gov.sg"); });

builder.Services.AddHttpApi<IKdkAuthApi>()
    .ConfigureHttpApi(options => { options.HttpHost = new Uri("https://authglb.digital.panasonic.com"); });

builder.Services.AddHttpApi<IKdkApi>()
    .ConfigureHttpApi((options, sp) => { options.HttpHost = new Uri("https://prod.mycfan.pgtls.net"); })
    .ConfigureHttpClient((sp, client) =>
    {
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

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<KdkTimestampDelegatingHandler>();
builder.Services.AddTransient<KdkAuthorizationDelegatingHandler>();
builder.Services.AddHostedService<WaterHeaterTurnOffChannel>();
builder.Services.AddSingleton<DataMallObservableFactoryService>();
builder.Services.AddSingleton<WaterHeaterTimerService>();

builder.Services.AddServerSideBlazor();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddRazorPages();
builder.Services.AddBootstrapBlazor();

builder.Services.AddAntiforgery(options => { options.SuppressXFrameOptionsHeader = true; });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAntiforgery();

app.MapDefaultEndpoints();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode(o => o.ContentSecurityFrameAncestorsPolicy = null);

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