using Homer.NetDaemon.Dashboard;
using Microsoft.AspNetCore.Components;

namespace Homer.NetDaemon.Components.Dashboard.Cards;

public abstract class CardBase : ComponentBase
{
    [CascadingParameter] public DashboardUi Ui { get; set; } = null!;

    [Parameter, EditorRequired] public SmartCard Card { get; set; } = null!;
    [Parameter, EditorRequired] public HomeSnapshot Home { get; set; } = null!;

    protected bool IsSmall => Card.Size == CardSize.Small;
    protected bool IsLarge => Card.Size == CardSize.Large;

    /// <summary>The devices a card is about, in the order the card lists them.</summary>
    protected List<DeviceInfo> SubjectDevices() =>
        (Card.Subjects ?? [])
        .Select(id => Home.Devices.FirstOrDefault(d => d.EntityId == id))
        .OfType<DeviceInfo>()
        .ToList();

    protected string RoomName(string roomId) => Home.Room(roomId)?.Name ?? roomId switch
    {
        "bedroom2" => "卧室二",
        "bedroom3" => "卧室三",
        _ => roomId
    };
}
