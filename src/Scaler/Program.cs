using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using Scaler.Services;

var builder = WebApplication.CreateBuilder(args);

string _azureStorage = builder.Configuration.GetValue<string>("AzureStorageConnectionString");
int _siloPort = builder.Configuration.GetValue<int>("SiloPort");
int _gatewayPort = builder.Configuration.GetValue<int>("GatewayPort");

builder.Services.AddGrpc();
builder.Host.UseOrleans((hostBuilderContext, siloBuilder) =>
{
    siloBuilder
        .Configure<SiloOptions>(options =>
        {
            options.SiloName = $"scaler_{Environment.MachineName}_{Random.Shared.Next(100)}";
        })
        .Configure<ClusterOptions>(options =>
        {
            options.ServiceId = "Selfies";
            options.ClusterId = "Development";
        })
        .ConfigureEndpoints(
            siloPort: _siloPort,
            gatewayPort: _gatewayPort
        )
        .UseAzureStorageClustering(options => options.ConfigureTableServiceClient(_azureStorage));
});

var app = builder.Build();
app.MapGrpcService<ExternalScalerService>();
app.Run();
