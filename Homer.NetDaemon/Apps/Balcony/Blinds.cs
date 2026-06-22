using System.Text.Json;
using System.Collections.Concurrent;
using AsyncKeyedLock;
using Homer.NetDaemon.Channels;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;

namespace Homer.NetDaemon.Apps.Balcony;

// [Focus]
public class Blinds : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly List<string> _blindNames = ["One", "Two", "Three"];

    // Track state (0 is fully up, 3 is fully down)
    private readonly double[] _positions = [0, 0, 0];

    // Total time to go from 0 to 3 in seconds
    private const double TotalTimeSeconds = 30.0;

    // Updates every 100ms
    private const int StepDelayMs = 100;

    // Save to HA every 1s
    private const int HaSaveDelayMs = 1000;

    private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeMovements = new();

    public Blinds(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var remote = scope.ServiceProvider.GetRequiredService<RemoteEntities>();
        var textEntities = scope.ServiceProvider.GetRequiredService<InputTextEntities>();

        try
        {
            var currentState = JsonSerializer.Deserialize<List<double>>(textEntities.BalconyBlindsState.State ?? "[0, 0, 0]") ?? [0, 0, 0];
            for (int i = 0; i < 3; i++)
            {
                if (i < currentState.Count)
                    _positions[i] = currentState[i];
            }
        }
        catch
        {
            // Ignore parse errors on startup
        }

        await foreach (var command in BalconyBlindsChannel.Channel.Reader.ReadAllAsync(stoppingToken))
        {
            _ = Task.Run(() =>
            {
                foreach (var blindIdx in command.Blinds)
                {
                    var blindName = _blindNames[blindIdx];

                    // Cancel any ongoing movement for this blind
                    if (_activeMovements.TryRemove(blindName, out var existingCts))
                    {
                        existingCts.Cancel();
                        existingCts.Dispose();
                    }

                    var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    _activeMovements[blindName] = cts;

                    switch (command.Action)
                    {
                        case BalconyBlindAction.Up:
                            _ = MoveBlindAsync(blindIdx, blindName, remote, textEntities, -1, cts.Token);
                            break;
                        case BalconyBlindAction.Down:
                            _ = MoveBlindAsync(blindIdx, blindName, remote, textEntities, 1, cts.Token);
                            break;
                        case BalconyBlindAction.Stop:
                            remote.BalconyRemote.SendCommand($"{blindName} Stop", "Balcony Blinds");
                            // Already cancelled ongoing movements above
                            break;
                        case BalconyBlindAction.Calibrate:
                            var binarySensors = scope.ServiceProvider.GetRequiredService<BinarySensorEntities>();
                            _ = CalibrateBlindAsync(blindIdx, blindName, remote, textEntities, binarySensors, cts.Token);
                            break;
                        case BalconyBlindAction.GoToPosition:
                            if (command.Position.HasValue)
                            {
                                _ = GoToPositionAsync(blindIdx, blindName, command.Position.Value, remote, textEntities, cts.Token);
                            }
                            break;
                    }
                }
            }, stoppingToken);
        }
    }

    private async Task CalibrateBlindAsync(int blindIdx, string blindName, RemoteEntities remote, InputTextEntities textEntities, BinarySensorEntities binarySensors, CancellationToken ct)
    {
        try
        {
            // 1. Move up for slightly more than total time to ensure hitting top
            remote.BalconyRemote.SendCommand($"{blindName} Up", "Balcony Blinds");
            await Task.Delay(TimeSpan.FromSeconds(TotalTimeSeconds + 5), ct);

            _positions[blindIdx] = 0;
            SaveStateToHa(textEntities);

            // 2. Start moving down
            remote.BalconyRemote.SendCommand($"{blindName} Down", "Balcony Blinds");

            BinarySensorEntity? midContactSensor = blindIdx switch
            {
                0 => binarySensors.BalconyBlind1MidContactContact,
                1 => binarySensors.BalconyBlind2MidContactContact,
                2 => binarySensors.BalconyBlind3MidContactContact,
                _ => null
            };

            if (midContactSensor == null) return;

            // 3. Wait for mid contact to trigger
            var tcs = new TaskCompletionSource();
            using var registration = ct.Register(() => tcs.TrySetCanceled());

            using var sub = midContactSensor.StateChanges().Subscribe(_ =>
            {
                tcs.TrySetResult();
            });

            // Fail-safe timeout
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(TotalTimeSeconds), ct);

            await Task.WhenAny(tcs.Task, timeoutTask);

            // Stop it wherever it is
            remote.BalconyRemote.SendCommand($"{blindName} Stop", "Balcony Blinds");

            if (tcs.Task.IsCompletedSuccessfully)
            {
                // Set exactly to middle
                _positions[blindIdx] = 1.5;
                SaveStateToHa(textEntities);
            }
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation
        }
    }

    private async Task GoToPositionAsync(int blindIdx, string blindName, double targetPosition, RemoteEntities remote, InputTextEntities textEntities, CancellationToken ct)
    {
        try
        {
            targetPosition = Math.Clamp(targetPosition, 0, 3);

            var currentPosition = _positions[blindIdx];
            var diff = currentPosition - targetPosition;

            if (Math.Abs(diff) < 0.1) return; // already there

            var direction = diff > 0 ? -1 : 1; // if diff > 0 (e.g. 3 -> 0), move Up (-1)
            var op = direction < 0 ? "Up" : "Down";

            remote.BalconyRemote.SendCommand($"{blindName} {op}", "Balcony Blinds");

            var stepChange = (3.0 / TotalTimeSeconds) * (StepDelayMs / 1000.0) * direction;
            var lastHaSave = DateTime.UtcNow;

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(StepDelayMs, ct);

                    _positions[blindIdx] += stepChange;

                    var newDiff = _positions[blindIdx] - targetPosition;

                    if ((direction < 0 && newDiff <= 0) || (direction > 0 && newDiff >= 0))
                    {
                        _positions[blindIdx] = targetPosition;
                        remote.BalconyRemote.SendCommand($"{blindName} Stop", "Balcony Blinds");
                        break;
                    }

                    if ((DateTime.UtcNow - lastHaSave).TotalMilliseconds >= HaSaveDelayMs)
                    {
                        SaveStateToHa(textEntities);
                        lastHaSave = DateTime.UtcNow;
                    }
                }
            }
            finally
            {
                SaveStateToHa(textEntities);
            }
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation
        }
    }

    private async Task MoveBlindAsync(int blindIdx, string blindName, RemoteEntities remote, InputTextEntities textEntities, int direction, CancellationToken ct)
    {
        try
        {
            var op = direction < 0 ? "Up" : "Down";
            remote.BalconyRemote.SendCommand($"{blindName} {op}", "Balcony Blinds");

            var stepChange = (3.0 / TotalTimeSeconds) * (StepDelayMs / 1000.0) * direction;
            var lastHaSave = DateTime.UtcNow;

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(StepDelayMs, ct);

                    _positions[blindIdx] += stepChange;

                    // Clamp
                    if (_positions[blindIdx] <= 0 && direction < 0)
                    {
                        _positions[blindIdx] = 0;
                        break;
                    }

                    if (_positions[blindIdx] >= 3 && direction > 0)
                    {
                        _positions[blindIdx] = 3;
                        break;
                    }

                    if ((DateTime.UtcNow - lastHaSave).TotalMilliseconds >= HaSaveDelayMs)
                    {
                        SaveStateToHa(textEntities);
                        lastHaSave = DateTime.UtcNow;
                    }
                }
            }
            finally
            {
                SaveStateToHa(textEntities);
            }
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation
        }
    }

    private void SaveStateToHa(InputTextEntities textEntities)
    {
        var serialized = JsonSerializer.Serialize(_positions.Select(p => Math.Round(p, 1)).ToList());
        textEntities.BalconyBlindsState.SetValue(serialized);
    }
}
