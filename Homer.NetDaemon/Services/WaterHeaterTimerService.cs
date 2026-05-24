namespace Homer.NetDaemon.Services;

public class WaterHeaterTimerService
{
    public DateTime? ScheduledTurnOffDateTime { get; set; }
    public DateTime? LastTurnedOnDateTime { get; set; }

    public event Action<TimeSpan>? ManualOverrideRequested;

    public void RequestManualOverride(int minutes)
    {
        if (minutes <= 0)
        {
            return;
        }

        ManualOverrideRequested?.Invoke(TimeSpan.FromMinutes(minutes));
    }
}
