namespace Homer.NetDaemon.Services;

/// <summary>
/// Estimated tank heat inventory in heater-minutes. There is no tank temperature sensor, so this is a first-order
/// model: heating fills it, showers drain it, and standing loss leaks it. Policy in
/// <c>ShowerHeating</c> heats a deficit, not "someone is in the bathroom."
/// </summary>
public sealed class WaterHeaterInventory
{
    /// <summary>Element time that takes the tank from incoming mains to a usable setpoint.</summary>
    public const double CapacityMinutes = 20;

    /// <summary>Inventory to hold after a shower cluster or before a typical demand window.</summary>
    public const double TargetMinutes = 12;

    /// <summary>Below this, a confirmed shower is allowed to turn the element on so the tank does not run out.</summary>
    public const double RunoutFloorMinutes = 4;

    /// <summary>
    /// Heat-minutes lost per real minute while the element is off. ~15–20% of capacity over 8 hours in a warm flat.
    /// </summary>
    public const double StandingLossPerMinute = 0.008;

    /// <summary>Heat-minutes drained per minute of a confirmed shower.</summary>
    public const double DrawPerShowerMinute = 1.0;

    /// <summary>
    /// Shower detection only confirms after several minutes of presence without motion. That water already left.
    /// </summary>
    public const double UndetectedDrawMinutes = 4;

    public static readonly TimeSpan ClusterGap = TimeSpan.FromMinutes(25);
    public static readonly TimeSpan MidShowerHeatDelay = TimeSpan.FromSeconds(90);

    public static readonly TimeOnly NightLockoutStart = new(0, 30);
    public static readonly TimeOnly NightLockoutEnd = new(5, 30);

    private readonly object _gate = new();
    private DateTime _lastTickUtc = DateTime.UtcNow;
    private double _stateOfChargeMinutes = TargetMinutes;

    /// <summary>Estimated usable heat left in the tank, in minutes of element time.</summary>
    public double StateOfChargeMinutes
    {
        get
        {
            lock (_gate)
            {
                return _stateOfChargeMinutes;
            }
        }
    }

    public event Action? StateChanged;

    public void Tick(DateTime utcNow, bool heaterOn, bool showering)
    {
        var changed = false;

        lock (_gate)
        {
            var elapsedMinutes = (utcNow - _lastTickUtc).TotalMinutes;
            if (elapsedMinutes < 0)
            {
                elapsedMinutes = 0;
            }

            // Skip absurd jumps after a clock step or a long pause so one tick cannot empty or fill the tank.
            elapsedMinutes = Math.Min(elapsedMinutes, 30);

            if (elapsedMinutes > 0)
            {
                var next = _stateOfChargeMinutes;
                if (heaterOn)
                {
                    next += elapsedMinutes;
                }
                else
                {
                    next -= StandingLossPerMinute * elapsedMinutes;
                }

                if (showering)
                {
                    next -= DrawPerShowerMinute * elapsedMinutes;
                }

                next = Math.Clamp(next, 0, CapacityMinutes);
                if (Math.Abs(next - _stateOfChargeMinutes) > 0.001)
                {
                    _stateOfChargeMinutes = next;
                    changed = true;
                }
            }

            _lastTickUtc = utcNow;
        }

        if (changed)
        {
            StateChanged?.Invoke();
        }
    }

    /// <summary>Apply the draw that happened before shower detection confirmed the visit.</summary>
    public void ApplyUndetectedShowerDraw()
    {
        ApplyDelta(-UndetectedDrawMinutes * DrawPerShowerMinute);
    }

    public void ApplyDelta(double heatMinutes)
    {
        var changed = false;

        lock (_gate)
        {
            var next = Math.Clamp(_stateOfChargeMinutes + heatMinutes, 0, CapacityMinutes);
            if (Math.Abs(next - _stateOfChargeMinutes) > 0.001)
            {
                _stateOfChargeMinutes = next;
                changed = true;
            }
        }

        if (changed)
        {
            StateChanged?.Invoke();
        }
    }

    public bool IsNightLockout(TimeOnly localTime)
    {
        return localTime >= NightLockoutStart && localTime < NightLockoutEnd;
    }

    public static TimeSpan RecoveryDuration(double stateOfChargeMinutes, double targetMinutes = TargetMinutes)
    {
        var need = targetMinutes - stateOfChargeMinutes;
        return need > 0 ? TimeSpan.FromMinutes(need) : TimeSpan.Zero;
    }
}
