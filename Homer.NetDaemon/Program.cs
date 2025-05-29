using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using AsyncKeyedLock;
using Homer.NetDaemon.Apps.Kdk;
using Homer.NetDaemon.Components;
using Homer.NetDaemon.Entities;
using Homer.NetDaemon.Options;
using Homer.NetDaemon.Services;
using Homer.NetDaemon.Services.DataMall;
using Homer.NetDaemon.Services.DgsForecast;
using Homer.NetDaemon.Services.Mrt;
using Homer.NetDaemon.Services.SimplyGo;
using Homer.ServiceDefaults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NetDaemon.AppModel;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.Extensions.Tts;
using NetDaemon.Runtime;
using Refit;

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

builder.Services.AddOptions<SimplyGoOptions>()
    .Bind(builder.Configuration.GetSection("SimplyGo"));

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

builder.Services.AddRefitClient<IDataMallApi>()
    .ConfigureHttpClient((sp, client) =>
    {
        client.BaseAddress = new Uri("https://datamall2.mytransport.sg");
        client.DefaultRequestHeaders.Add("AccountKey",
            sp.GetRequiredService<IOptions<DataMallOptions>>().Value.AccountKey);
    });

builder.Services.AddRefitClient<IMrtApi>()
    .ConfigureHttpClient((sp, client) =>
    {
        client.BaseAddress = new Uri("https://mrt.from.sg");
    });


builder.Services.AddRefitClient<ISimplyGoApi>(new RefitSettings
    {
        ContentSerializer = new SystemTextJsonContentSerializer(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        }),
    })
    .ConfigureHttpClient((client) =>
    {
        client.BaseAddress = new Uri("https://simplygobff.ezlink.com.sg");
        client.DefaultRequestHeaders.Add("X-APP-TYPE", "IOS");
        client.DefaultRequestHeaders.Add("X-APP-VERSION", "9.9.1");
        client.DefaultRequestHeaders.Add("X-OS-VERSION", "8.4");
        client.DefaultRequestHeaders.Add("X-DEVICE-MODEL", "iPhone 14 Pro Max");
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SimplyGo/329");
        client.DefaultRequestHeaders.UserAgent.ParseAdd("CFNetwork/3826.500.111.2.2");
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Darwin/24.4.0");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("amx",
            "e62e7c778eed4b5ba220a8d3c512a555:b29b4d27968a375903ffc9d6dd9ee8987876b156e4cfa15d1b2acdd5b48f2cf3:vZcwFqEz8waDNebPVWdPjpjkln8LUBfx:1748353405");
    })
    .ConfigureAdditionalHttpMessageHandlers((o, s) => { o.Add(new DH()); });

builder.Services.AddRefitClient<IDgsForecast>()
    .ConfigureHttpClient(options => { options.BaseAddress = new Uri("https://api-open.data.gov.sg"); });

builder.Services.AddRefitClient<IKdkAuthApi>()
    .ConfigureHttpClient(options => { options.BaseAddress = new Uri("https://authglb.digital.panasonic.com"); });

builder.Services.AddRefitClient<IKdkApi>()
    .ConfigureHttpClient((sp, client) =>
    {
        client.BaseAddress = new Uri("https://prod.mycfan.pgtls.net");
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
builder.Services.AddSingleton<ApiObservableFactoryService>();
builder.Services.AddSingleton<WaterHeaterTimerService>();
builder.Services.AddSingleton<AsyncKeyedLocker<string>>();

builder.Services.AddServerSideBlazor();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();
builder.Services.AddRazorPages();
builder.Services.AddBootstrapBlazor();

builder.Services.AddAntiforgery(options => { options.SuppressXFrameOptionsHeader = true; });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseWebAssemblyDebugging();
}

// app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseRouting();
app.UseStaticFiles();

app.UseAntiforgery();

app.MapDefaultEndpoints();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode(o => o.ContentSecurityFrameAncestorsPolicy = null)
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Homer.NetDaemon.Client._Imports).Assembly);

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

public class DH : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Activity.Current = null;
        var res = await base.SendAsync(request, cancellationToken);
        var c = await res.Content.ReadAsStringAsync(cancellationToken);
        return res;
    }
}