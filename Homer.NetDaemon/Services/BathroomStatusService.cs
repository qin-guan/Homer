namespace Homer.NetDaemon.Services;

public enum BathroomState
{
    Unoccupied,
    Occupied,
    Showering
}

public class BathroomStatusService
{
    public BathroomState BathroomStatus { get; set; } = BathroomState.Unoccupied;
    public BathroomState MasterBathroomStatus { get; set; } = BathroomState.Unoccupied;
}
