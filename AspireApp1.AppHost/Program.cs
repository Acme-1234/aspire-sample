using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache").WithRedisInsight();
var panda = builder.AddRedpanda("redpanda")
                    .WithRedpandaConsole();
var material = builder.AddMaterialize("materialize")
            .WaitFor(panda);



var apiService = builder.AddProject<Projects.AspireApp1_ApiService>("apiservice")
    .WaitFor(material);

builder.AddProject<Projects.AspireApp1_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(cache)
    .WaitFor(cache)
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
