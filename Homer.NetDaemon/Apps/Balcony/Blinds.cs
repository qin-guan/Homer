using System.Reactive.Concurrency;
using System.Text.Json;
using AsyncKeyedLock;
using Homer.NetDaemon.Entities;
using NetDaemon.AppModel;
using NetDaemon.HassModel;

namespace Homer.NetDaemon.Apps.Balcony;

[Focus]
[NetDaemonApp]
public class Blinds(
    InputTextEntities textEntities,
    RemoteEntities remote,
    IScheduler scheduler,
    AsyncKeyedLocker<string> locker
) : IAsyncInitializable
{
    private readonly List<string> _blindNames = ["One", "Two", "Three"];

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
                var tasks = _blindNames.Select(async name =>
                    await locker.LockAsync(name, cancellationToken));

                var locks = await Task.WhenAll(tasks);

                remote.LivingRoomRemote.SendCommand("All Down", "Balcony Blinds");

                await Task.Delay(TimeSpan.FromSeconds(30));

                foreach (var disposable in locks)
                {
                    disposable.Dispose();
                }

                return;
            }

            if (desired.All(v => v == 0))
            {
                var tasks = _blindNames.Select(async name =>
                    await locker.LockAsync(name, cancellationToken));

                var locks = await Task.WhenAll(tasks);

                remote.LivingRoomRemote.SendCommand("All Up", "Balcony Blinds");

                await Task.Delay(TimeSpan.FromSeconds(30));

                foreach (var disposable in locks)
                {
                    disposable.Dispose();
                }

                return;
            }

            await Parallel.ForEachAsync(
                desired.Select((v, idx) => new { Value = v, Idx = idx }),
                cancellationToken,
                async (val, ct2) =>
                {
                    var diff = old[val.Idx] - val.Value;
                    if (diff == 0) return;

                    var op = diff > 0 ? "Up" : "Down";
                    var blind = _blindNames[val.Idx];

                    using var lockAsync = await locker.LockAsync(blind, ct2);
                    remote.LivingRoomRemote.SendCommand($"{blind} {op}", "Balcony Blinds");
                    await Task.Delay(TimeSpan.FromSeconds(10 * Math.Abs(diff)), ct2);
                    remote.LivingRoomRemote.SendCommand($"{blind} Stop", "Balcony Blinds");
                });
        });
    }
}