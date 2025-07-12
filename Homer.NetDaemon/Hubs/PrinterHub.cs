using Microsoft.AspNetCore.SignalR;

namespace Homer.NetDaemon.Hubs;

public class PrinterHub : Hub
{
    private readonly Dictionary<string, string> _printers = new ();
    
    public override async Task OnConnectedAsync()
    {
        
    }

    public async Task Init(string macAddr)
    {
        if (Context.UserIdentifier is null) return;
        _printers[macAddr] = Context.UserIdentifier;
    }

    public async Task AddJob(string macAddr, byte[] data)
    {
        await Clients.User(_printers[macAddr]).SendAsync("ReceiveJob", data);
    }
}