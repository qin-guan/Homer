namespace Homer.NetDaemon.Services;

public class WaterHeaterTimerService
{
    private DateTime? _scheduledTurnOffDateTime;
    private DateTime? _lastTurnedOnDateTime;

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

    public event Action<TimeSpan>? ManualOverrideRequested;
    public event Action? StateChanged;

    public bool RequestManualOverride(int minutes)
    {
        if (minutes <= 0)
        {
            return false;
        }

        var manualOverrideRequested = ManualOverrideRequested;
        if (manualOverrideRequested is null)
        {
            return false;
        }

        manualOverrideRequested.Invoke(TimeSpan.FromMinutes(minutes));
        return true;
    }
}
