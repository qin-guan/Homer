namespace Homer.NetDaemon.Services;

public class WaterHeaterTimerService
{
    private DateTime? _scheduledTurnOffDateTime;
    private DateTime? _lastTurnedOnDateTime;
    private IWaterHeaterController? _controller;

    public DateTime? ScheduledTurnOffDateTime
    {
        get => _scheduledTurnOffDateTime;
        set
        {
            if (_scheduledTurnOffDateTime == value)
            {
                return;
            }

            _scheduledTurnOffDateTime = value;
            StateChanged?.Invoke();
        }
    }

    public DateTime? LastTurnedOnDateTime
    {
        get => _lastTurnedOnDateTime;
        set
        {
            if (_lastTurnedOnDateTime == value)
            {
                return;
            }

            _lastTurnedOnDateTime = value;
            StateChanged?.Invoke();
        }
    }

    /// <summary>The running WaterHeaterController app, or null while it is disabled.</summary>
    public IWaterHeaterController? Controller => Volatile.Read(ref _controller);

    public event Action? StateChanged;

    /// <summary>Raised after the controller has handled the heater turning off, outside of the controller's lock.</summary>
    public event Action? HeaterTurnedOff;

    public void AttachController(IWaterHeaterController controller) => Volatile.Write(ref _controller, controller);

    public void DetachController(IWaterHeaterController controller) =>
        Interlocked.CompareExchange(ref _controller, null, controller);

    public void OnHeaterTurnedOff() => HeaterTurnedOff?.Invoke();

    public bool RequestManualOverride(int minutes)
    {
        if (minutes <= 0)
        {
            return false;
        }

        if (Controller is not { } controller)
        {
            return false;
        }

        controller.RequestHeating(
            $"manual override requested for {minutes} minutes",
            TimeSpan.FromMinutes(minutes));
        return true;
    }
}
