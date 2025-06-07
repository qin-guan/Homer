using System.ComponentModel;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text.Json;
using Homer.NetDaemon.Entities;
using Homer.NetDaemon.Services;
using NetDaemon.AppModel;
using NetDaemon.HassModel;

namespace Homer.NetDaemon.Apps.Balcony;

[Focus]
[NetDaemonApp]
public class RainyWeatherCloseBlinds(
    ApiObservableFactoryService factory,
    InputTextEntities textEntities,
    NotifyServices notify,
    IHaContext context,
    IHaRegistry registry,
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

        notify.MobileAppQinsIphone(
            "clear_notification",
            data: clear
        );

        notify.MobileAppGuanXiujiSIphone(
            "clear_notification",
            data: clear
        );

        notify.MobileAppQinBosIphone16ProMax(
            "clear_notification",
            data: clear
        );

        var observable = factory.CreateForecast()
            .Select(f => f.Data.Items.First().Forecasts.First(f => f.Area == "Bishan").Value)
            .Where(v => v is (
                "Moderate Rain" or
                "Heavy Rain" or
                "Passing Showers" or
                "Light Showers" or
                "Showers" or
                "Heavy Showers" or
                "Thundery Showers" or
                "Heavy Thundery Showers" or
                "Heavy Thundery Showers with Gusty Winds")
            )
            .DistinctUntilChanged();

        _disposables.Add(observable
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

                notify.MobileAppQinsIphone(
                    $"快要下雨了！ ({forecast})",
                    "主人想关阳台窗帘吗？",
                    data: data
                );

                notify.MobileAppGuanXiujiSIphone(
                    $"快要下雨了！ ({forecast})",
                    "主人想关阳台窗帘吗？",
                    data: data
                );

                notify.MobileAppQinBosIphone16ProMax(
                    $"快要下雨了！ ({forecast})",
                    "主人想关阳台窗帘吗？",
                    data: data
                );
            }));

        _disposables.Add(observable
            .Where(_ => TimeOnly.FromDateTime(DateTime.Now).IsBetween(new TimeOnly(21, 0), new TimeOnly(9, 0)))
            .Subscribe(forecast => { textEntities.BalconyBlindsState.SetValue("[3, 3, 3]"); }));

        context.Events.Where(e => e.DataElement?.TryGetProperty("actionName", out _) ?? false)
            .Select(e => e.DataElement?.GetProperty("actionName").GetString())
            .Where(actionName => actionName?.StartsWith('{') ?? false)
            .Select(v =>
            {
                try
                {
                    return JsonSerializer.Deserialize<ActionData>(v);
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
                        textEntities.BalconyBlindsState.SetValue("[3, 3, 3]");
                        break;
                    case ActionDataTimeSpan.ThirtyMinutes:
                        scheduler.Schedule(TimeSpan.FromMinutes(30),
                            () => { textEntities.BalconyBlindsState.SetValue("[3, 3, 3]"); });
                        break;
                    case ActionDataTimeSpan.SixtyMinutes:
                        scheduler.Schedule(TimeSpan.FromMinutes(60),
                            () => { textEntities.BalconyBlindsState.SetValue("[3, 3, 3]"); });
                        break;
                    default: throw new InvalidEnumArgumentException();
                }

                notify.MobileAppQinsIphone(
                    "clear_notification",
                    data: clear
                );

                notify.MobileAppGuanXiujiSIphone(
                    "clear_notification",
                    data: clear
                );

                notify.MobileAppQinBosIphone16ProMax(
                    "clear_notification",
                    data: clear
                );

                var time = e.Time switch
                {
                    ActionDataTimeSpan.Now => TimeOnly.FromDateTime(DateTime.Now),
                    ActionDataTimeSpan.ThirtyMinutes => TimeOnly.FromDateTime(DateTime.Now.AddMinutes(30)),
                    ActionDataTimeSpan.SixtyMinutes => TimeOnly.FromDateTime(DateTime.Now.AddMinutes(60)),
                    _ => throw new ArgumentOutOfRangeException()
                };

                notify.MobileAppQinsIphone(
                    $"我将在 {time.ToShortTimeString()} 关阳台窗帘"
                );

                notify.MobileAppGuanXiujiSIphone(
                    $"我将在 {time.ToShortTimeString()} 关阳台窗帘"
                );

                notify.MobileAppQinBosIphone16ProMax(
                    $"我将在 {time.ToShortTimeString()} 关阳台窗帘"
                );
            });
    }

    public ValueTask DisposeAsync()
    {
        _disposables.ForEach(e => e.Dispose());
        return ValueTask.CompletedTask;
    }
}