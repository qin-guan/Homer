namespace Homer.NetDaemon.Services.DataMall.BusArrival;

public class BusArrivalResponse
{
    public string BusStopCode { get; set; }
    public List<Service> Services { get; set; }
}

public class Service
{
    public string ServiceNo { get; set; }
    public string Operator { get; set; }
    public NextBus NextBus { get; set; }
    public NextBus NextBus2 { get; set; }
    public NextBus NextBus3 { get; set; }
}

public class NextBus
{
    public string OriginCode { get; set; }
    public string DestinationCode { get; set; }
    public DateTime? EstimatedArrival { get; set; }
    public int Monitored { get; set; }
    public string Latitude { get; set; }
    public string Longitude { get; set; }
    public string VisitNumber { get; set; }
    public string Load { get; set; }
    public string Feature { get; set; }
    public string Type { get; set; }
}