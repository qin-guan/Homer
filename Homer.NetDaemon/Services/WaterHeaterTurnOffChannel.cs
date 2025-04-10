using System.Reactive.Concurrency;
using Homer.NetDaemon.Entities;

namespace Homer.NetDaemon.Services;

public class WaterHeaterTurnOffChannel(IServiceProvider serviceProvider, ILogger<WaterHeaterTurnOffChannel> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var scheduler = scope.ServiceProvider.GetRequiredService<IScheduler>();
        var switchEntities = scope.ServiceProvider.GetRequiredService<SwitchEntities>();

        await foreach (var timeSpan in Channels.Channels.TurnOffWaterHeaterSwitch.Reader.ReadAllAsync(stoppingToken))
        {
            logger.LogInformation("Turning off heater in {TimeSpan}", timeSpan);
            scheduler.Schedule(timeSpan, () => { switchEntities.WaterHeaterSwitch.TurnOff(); });
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var switchEntities = scope.ServiceProvider.GetRequiredService<SwitchEntities>();

        switchEntities.WaterHeaterSwitch.TurnOff();
    }
}