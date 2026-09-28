var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Homer_NetDaemon>("netdaemon")
    .WithExternalHttpEndpoints();

builder.Build().Run();
