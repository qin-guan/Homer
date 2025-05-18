using System.Reactive.Concurrency;
using System.Text.Json;
using AsyncKeyedLock;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;
using NetDaemon.HassModel;

namespace Homer.NetDaemon.Apps.Balcony;

[NetDaemonApp]
public class Blinds(InputTextEntities textEntities, RemoteEntities remote, IScheduler scheduler) : IAsyncInitializable
{
    private readonly List<string> _blindNames = ["One", "Two", "Three"];
    private static readonly AsyncKeyedLocker<string> AsyncKeyedLocker = new();

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        textEntities.BalconyBlindsState.StateChanges().SubscribeAsync(async (e) =>
        {
            var old = JsonSerializer.Deserialize<List<int>>(e.Old?.State ?? throw new Exception()) ??
                      throw new Exception();
            var desired = JsonSerializer.Deserialize<List<int>>(e.New?.State ?? throw new Exception()) ??
                          throw new Exception();

            if (desired.All(v => v == 3))
            {
                remote.LivingRoomRemote.SendCommand("All Down", "Balcony Blinds");
                return;
            }

            if (desired.All(v => v == 0))
            {
                remote.LivingRoomRemote.SendCommand("All Up", "Balcony Blinds");
                return;
            }

            for (var idx = 0; idx < desired.Count; idx++)
            {
                var diff = old[idx] - desired[idx];
                if (diff is 0) continue;
                var op = diff > 0 ? "Up" : "Down";
                var blind = _blindNames[idx];

                var lockAsync = await AsyncKeyedLocker.LockAsync(blind, cancellationToken);
                remote.LivingRoomRemote.SendCommand($"{_blindNames[idx]} {op}", "Balcony Blinds");

                var idx1 = idx;
                scheduler.Schedule(TimeSpan.FromSeconds(7 * Math.Abs(diff)), () =>
                {
                    remote.LivingRoomRemote.SendCommand($"{_blindNames[idx1]} Stop", "Balcony Blinds");
                    lockAsync.Dispose();
                });
            }
        });
    }
}