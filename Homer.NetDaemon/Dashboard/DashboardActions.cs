using Homer.NetDaemon.Channels;
using Homer.NetDaemon.Entities;
using Homer.NetDaemon.Services;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace Homer.NetDaemon.Dashboard;

/// <summary>What the dashboard can do to the home. Scoped to a Blazor circuit, like the entities it calls through.</summary>
public sealed class DashboardActions(
    IHaContext haContext,
    SwitchEntities switches,
    InputBooleanEntities inputBooleans,
    WaterHeaterTimerService waterHeaterTimer,
    DashboardState state,
    ILogger<DashboardActions> logger)
{
    public const int MaxHeatMinutes = 25;

    public void Toggle(string entityId) => Call(entityId, "toggle");

    public void TurnOn(string entityId) => Call(entityId, "turn_on");

    public void TurnOff(IEnumerable<string> entityIds)
    {
        foreach (var entityId in entityIds)
        {
            Call(entityId, "turn_off");
        }
    }

    public void SetLaundryMode(bool on)
    {
        if (on)
        {
            inputBooleans.LiangYiMoShi.TurnOn();
        }
        else
        {
            inputBooleans.LiangYiMoShi.TurnOff();
        }
    }

    public void Dismiss(SmartCard card)
    {
        if (card.DismissFor is { } duration)
        {
            state.Dismiss(card.Key, duration);
        }
    }

    public void MediaPlayPause(string entityId) => Call(entityId, "media_play_pause");

    public void MediaNext(string entityId) => Call(entityId, "media_next_track");

    /// <param name="position">0 fully up (open) to 3 fully down (closed).</param>
    public void MoveBlinds(IReadOnlyList<int> blinds, double position) =>
        Send(new BalconyBlindCommand(BalconyBlindAction.GoToPosition, blinds.ToList(),
            Math.Clamp(position, 0, BlindsInfo.Closed)));

    public void StopBlinds() => Send(new BalconyBlindCommand(BalconyBlindAction.Stop, [0, 1, 2]));

    public void CalibrateBlinds() => Send(new BalconyBlindCommand(BalconyBlindAction.Calibrate, [0, 1, 2]));

    public void CloseBlinds() => BalconyBlindsChannel.CloseAll();

    /// <summary>Heats the tank through the water heater controller, or directly with a timed turn-off while it's disabled.</summary>
    public void HeatWater(int minutes)
    {
        minutes = Math.Clamp(minutes, 1, MaxHeatMinutes);
        if (waterHeaterTimer.RequestManualOverride(minutes))
        {
            return;
        }

        var duration = TimeSpan.FromMinutes(minutes);
        var now = DateTime.UtcNow;

        waterHeaterTimer.ScheduledTurnOffDateTime = now.Add(duration);
        waterHeaterTimer.LastTurnedOnDateTime = now;
        switches.WaterHeaterSwitch.TurnOn();
        Channels.Channels.TurnOffWaterHeaterSwitch.Writer.TryWrite(duration);
    }

    /// <summary>Asks the controller to stop heating; it keeps the element on until the minimum run has passed.</summary>
    public void StopWaterHeater()
    {
        if (waterHeaterTimer.Controller is { } controller)
        {
            controller.ReleaseHeating("stopped from the dashboard");
        }
        else
        {
            switches.WaterHeaterSwitch.TurnOff();
        }
    }

    private void Call(string entityId, string service)
    {
        var domain = entityId[..entityId.IndexOf('.')];
        logger.LogInformation("Dashboard calling {Domain}.{Service} for {EntityId}", domain, service, entityId);
        haContext.CallService(domain, service, ServiceTarget.FromEntity(entityId));
    }

    private static void Send(BalconyBlindCommand command) => BalconyBlindsChannel.Channel.Writer.TryWrite(command);
}
