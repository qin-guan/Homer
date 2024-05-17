using System.Diagnostics.Metrics;
using NetDaemon.AppModel;

namespace Homer.NetDaemon.Apps;

[NetDaemonApp]
public class Test
{
    public Test(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(nameof(Test));
        var presenceMeter = meter.CreateCounter<int>("test");

        presenceMeter.Add(1);
    }
}