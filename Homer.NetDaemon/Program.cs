using System.Reflection;
using Homer.NetDaemon.Apps.Remotes;
using Homer.NetDaemon.Entities;
using Microsoft.AspNetCore.Mvc;
using NetDaemon.AppModel;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.Extensions.Tts;
using NetDaemon.Runtime;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseNetDaemonAppSettings();
builder.Host.UseNetDaemonRuntime();
builder.Host.UseNetDaemonTextToSpeech();

builder.Services.AddSerilog((services, lc) => lc
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console()
);

builder.Services.AddAppsFromAssembly(Assembly.GetExecutingAssembly());
builder.Services.AddNetDaemonStateManager();
builder.Services.AddNetDaemonScheduler();
builder.Services.AddHomeAssistantGenerated();

builder.AddServiceDefaults();

builder.Services.AddProblemDetails();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IrRemoteLock>();

var app = builder.Build();

app.UseSerilogRequestLogging();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapDefaultEndpoints();

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

app.MapPost("/contact/qg", ([FromBody] string content, NotifyServices notifyServices) =>
{
    notifyServices.MobileAppQinsIphone(content, "Message from the internet");
});

app.Run();