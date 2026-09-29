namespace Homer.NetDaemon.Services;

public enum BathroomState
{
    Unoccupied,
    Occupied,
    Showering
}

public enum BathroomId
{
    Bathroom,
    MasterBathroom
}

/// <param name="Reason">Why the status changed, for logging.</param>
/// <param name="EndedShowerDuration">How long the shower lasted, when this change ends one.</param>
public record BathroomStatusChange(
    BathroomId Bathroom,
    BathroomState Status,
    string Reason,
    TimeSpan? EndedShowerDuration = null);

/// <summary>
/// Bathroom status published by the shower detection apps, read by the dashboard and shower heating.
/// </summary>
public class BathroomStatusService
{
    private readonly object _gate = new();
    private readonly Dictionary<BathroomId, BathroomState> _statuses = [];

    public BathroomState BathroomStatus => GetStatus(BathroomId.Bathroom);
    public BathroomState MasterBathroomStatus => GetStatus(BathroomId.MasterBathroom);

    public bool AnyShowering
    {
        get
        {
            lock (_gate)
            {
                return _statuses.ContainsValue(BathroomState.Showering);
            }
        }
    }

    /// <summary>Raised when a bathroom's status changes. Handlers run outside of this service's lock.</summary>
    public event Action<BathroomStatusChange>? StatusChanged;

    public BathroomState GetStatus(BathroomId bathroom)
    {
        lock (_gate)
        {
            return _statuses.GetValueOrDefault(bathroom, BathroomState.Unoccupied);
        }
    }

    public void SetStatus(BathroomStatusChange change)
    {
        lock (_gate)
        {
            if (_statuses.GetValueOrDefault(change.Bathroom, BathroomState.Unoccupied) == change.Status)
            {
                return;
            }

            _statuses[change.Bathroom] = change.Status;
        }

        StatusChanged?.Invoke(change);
    }
}
