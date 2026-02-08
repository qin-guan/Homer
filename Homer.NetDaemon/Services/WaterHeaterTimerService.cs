namespace Homer.NetDaemon.Services;

public class WaterHeaterTimerService
{
    public DateTime? ScheduledTurnOffDateTime { get; set; }
    public DateTime? LastTurnedOnDateTime { get; set; }
}