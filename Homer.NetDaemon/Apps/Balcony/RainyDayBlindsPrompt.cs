using System.ComponentModel;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text.Json;
using Homer.NetDaemon.Channels;
using Homer.NetDaemon.Entities;
using Homer.NetDaemon.Services;
using NetDaemon.AppModel;
using NetDaemon.HassModel;

namespace Homer.NetDaemon.Apps.Balcony;

/// <summary>
/// Asks everyone whether to close the balcony blinds when rain is forecast during the day, then closes them now or
/// later depending on the answer.
/// </summary>
[NetDaemonApp]
public class RainyDayBlindsPrompt(
    ApiObservableFactoryService factory,
    NotifyServices notify,
    IHaContext context,
    IScheduler scheduler
)
    : IAsyncInitializable, IAsyncDisposable
{
    private List<IDisposable> _disposables = [];

    private enum ActionDataTimeSpan
    {
        Now,
        ThirtyMinutes,
        SixtyMinutes,
    }

    private record ActionData(string Action, ActionDataTimeSpan Time);

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var clear = new
        {
            tag = "close_blinds"
        };

        NotifyEveryone("clear_notification", data: clear);

        _disposables.Add(factory.CreateRainForecast()
            .Where(_ => TimeOnly.FromDateTime(DateTime.Now).IsBetween(new TimeOnly(9, 0), new TimeOnly(21, 0)))
            .Subscribe(forecast =>
            {
                var data = new
                {
                    tag = "close_blinds",
                    actions = new object[]
                    {
                        new
                        {
                            title = $"现在",
                            action = JsonSerializer.Serialize(new ActionData("close_blinds", ActionDataTimeSpan.Now))
                        },
                        new
                        {
                            title =
                                $"等30分钟",
                            action = JsonSerializer.Serialize(new ActionData(
                                "close_blinds",
                                ActionDataTimeSpan.ThirtyMinutes
                            ))
                        },
                        new
                        {
                            title =
                                $"等60分钟",
                            action = JsonSerializer.Serialize(new
                                ActionData("close_blinds", ActionDataTimeSpan.SixtyMinutes))
                        },
                        new
                        {
                            title = "不关"
                        }
                    }
                };

                NotifyEveryone($"快要下雨了！ ({forecast})", "主人想关阳台窗帘吗？", data);
            }));

        context.Events.Where(e => e.DataElement?.TryGetProperty("actionName", out _) ?? false)
            .Select(e => e.DataElement?.GetProperty("actionName").GetString())
            .Where(actionName => actionName?.StartsWith('{') ?? false)
            .Select(v =>
            {
                try
                {
                    return JsonSerializer.Deserialize<ActionData>(v!);
                }
                catch
                {
                    return null;
                }
            })
            .Where(o => o is { Action: "close_blinds" })
            .Subscribe(e =>
            {
                ArgumentNullException.ThrowIfNull(e);

                switch (e.Time)
                {
                    case ActionDataTimeSpan.Now:
                        BalconyBlindsChannel.CloseAll();
                        break;
                    case ActionDataTimeSpan.ThirtyMinutes:
                        scheduler.Schedule(TimeSpan.FromMinutes(30), BalconyBlindsChannel.CloseAll);
                        break;
                    case ActionDataTimeSpan.SixtyMinutes:
                        scheduler.Schedule(TimeSpan.FromMinutes(60), BalconyBlindsChannel.CloseAll);
                        break;
                    default: throw new InvalidEnumArgumentException();
                }

                NotifyEveryone("clear_notification", data: clear);

                var time = e.Time switch
                {
                    ActionDataTimeSpan.Now => TimeOnly.FromDateTime(DateTime.Now),
                    ActionDataTimeSpan.ThirtyMinutes => TimeOnly.FromDateTime(DateTime.Now.AddMinutes(30)),
                    ActionDataTimeSpan.SixtyMinutes => TimeOnly.FromDateTime(DateTime.Now.AddMinutes(60)),
                    _ => throw new ArgumentOutOfRangeException()
                };

                NotifyEveryone($"我将在 {time.ToShortTimeString()} 关阳台窗帘");
            });
    }

    private void NotifyEveryone(string message, string? title = null, object? data = null)
    {
        notify.MobileAppQinSS26Ultra(message, title, data: data);
        notify.MobileAppGuanXiujiSIphone(message, title, data: data);
        notify.MobileAppQinBosIphone16ProMax(message, title, data: data);
    }

    public ValueTask DisposeAsync()
    {
        _disposables.ForEach(e => e.Dispose());
        return ValueTask.CompletedTask;
    }
}
