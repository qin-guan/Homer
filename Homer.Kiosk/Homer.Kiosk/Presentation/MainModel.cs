namespace Homer.Kiosk.Presentation;

public partial record MainModel
{
    public MainModel()
    {
        Title = "Homer Kiosk";
    }

    public string? Title { get; }

    // Switch states - these would typically come from NetDaemon entities
    public IState<bool> BalconyLightsOn => State<bool>.Value(this, () => false);
    public IState<bool> AirCon1On => State<bool>.Value(this, () => false);
    public IState<bool> AirCon2On => State<bool>.Value(this, () => false);
    public IState<bool> WaterHeaterOn => State<bool>.Value(this, () => false);

    // Commands for toggling switches
    public async Task ToggleBalconyLights()
    {
        // Placeholder for actual NetDaemon integration
        await Task.CompletedTask;
    }

    public async Task ToggleAirCon1()
    {
        await Task.CompletedTask;
    }

    public async Task ToggleAirCon2()
    {
        await Task.CompletedTask;
    }

    public async Task ToggleWaterHeater()
    {
        await Task.CompletedTask;
    }
}
