namespace Homer.NetDaemon.Services;

/// <summary>
/// How apps turn the water heater on: every run is paid for from the daily budget and paired with a scheduled
/// turn-off. Implemented by the WaterHeaterController app and reached through <see cref="WaterHeaterTimerService.Controller"/>.
/// </summary>
public interface IWaterHeaterController
{
    bool IsHeaterOn { get; }

    /// <summary>
    /// Turns the heater on for up to <paramref name="duration"/>, or moves the current run's turn-off to match it,
    /// within the remaining budget and the maximum run duration.
    /// </summary>
    void RequestHeating(string reason, TimeSpan duration);

    /// <summary>Turns the heater off, once the current run has lasted the minimum run duration.</summary>
    void ReleaseHeating(string reason);
}
