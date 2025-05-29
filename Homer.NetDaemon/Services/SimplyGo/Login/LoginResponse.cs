namespace Homer.NetDaemon.Services.SimplyGo.Login;

public class LoginResponse
{
    public Data Data { get; set; }
}

public class Data
{
    public string ClientId { get; set; }
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
}

