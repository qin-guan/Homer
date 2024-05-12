using Projects;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Homer_NetDaemon>("netdaemon");

builder.Build().Run();