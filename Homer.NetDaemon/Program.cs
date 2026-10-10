using System.Reflection;
using AsyncKeyedLock;
using Homer.NetDaemon.Components;
using Homer.NetDaemon.Dashboard;
using Homer.NetDaemon.Entities;
using Homer.NetDaemon.Options;
using Homer.NetDaemon.Services;
using Homer.NetDaemon.Services.DataMall;
using Homer.NetDaemon.Services.OpenMeteo;
using Homer.ServiceDefaults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NetDaemon.AppModel;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.Extensions.Tts;
using NetDaemon.Runtime;
using Refit;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseNetDaemonAppSettings();
builder.Host.UseNetDaemonRuntime();
builder.Host.UseNetDaemonTextToSpeech();
builder.Host.UseNetDaemonMqttEntityManagement();

builder.Services.AddOptions<DataMallOptions>()
    .Bind(builder.Configuration.GetSection("DataMall"));

builder.Services.AddOptions<GoogleHomeDashboardOptions>()
    .Bind(builder.Configuration.GetSection("GoogleHomeDashboard"));

builder.Services.AddRefitGeneratedClient<IDataMallApi>()
    .ConfigureHttpClient((sp, client) =>
    {
        client.BaseAddress = new Uri("https://datamall2.mytransport.sg");
        client.DefaultRequestHeaders.Add("AccountKey",
            sp.GetRequiredService<IOptions<DataMallOptions>>().Value.AccountKey);
    });

builder.Services.AddRefitGeneratedClient<IOpenMeteoApi>()
    .ConfigureHttpClient(options => { options.BaseAddress = new Uri("https://api.open-meteo.com"); });

builder.Services.AddAppsFromAssembly(Assembly.GetExecutingAssembly());
builder.Services.AddNetDaemonStateManager();
builder.Services.AddNetDaemonScheduler();
builder.Services.AddHomeAssistantGenerated();

builder.AddServiceDefaults();

builder.Services.AddProblemDetails();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHostedService<WaterHeaterTurnOffChannel>();
builder.Services.AddHostedService<Homer.NetDaemon.Apps.Remotes.LivingRoomRemote>();
builder.Services.AddHostedService<Homer.NetDaemon.Apps.Balcony.Blinds>();
builder.Services.AddSingleton<ApiObservableFactoryService>();
builder.Services.AddSingleton<WaterHeaterTimerService>();
builder.Services.AddSingleton<WaterHeaterInventory>();
builder.Services.AddSingleton<BathroomStatusService>();
builder.Services.AddSingleton<DashboardState>();
builder.Services.AddScoped<DashboardActions>();
builder.Services.AddSingleton<AsyncKeyedLocker<string>>();

builder.Services.AddServerSideBlazor();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();
builder.Services.AddRazorPages();
builder.Services.AddBootstrapBlazor();

builder.Services.AddAntiforgery(options => { options.SuppressXFrameOptionsHeader = true; });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}

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
        notifyServices.MobileAppQinSS26Ultra(content, "Message from the internet");
    });

app.Run();
