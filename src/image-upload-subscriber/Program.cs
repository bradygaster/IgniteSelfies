using Dapr;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Azure;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using System.Text.Json.Serialization;
using UploadSubscriber;

// set up all of the dependencies the app will need
var builder = WebApplication.CreateBuilder(args);

string _frontEndCorsPolicy = "corspolicy";
string _azureStorage = builder.Configuration.GetValue<string>("AzureStorageConnectionString");
string _azureSignalR = builder.Configuration.GetValue<string>("AzureSignalRConnectionString");
string _frontEndUrl = builder.Configuration.GetValue<string>("FrontEnd");
int _siloPort = builder.Configuration.GetValue<int>("SiloPort");
int _gatewayPort = builder.Configuration.GetValue<int>("GatewayPort");

// store data protection keys in storage so we're ready for horizontal scaling
builder.Services.AddAzureClientsCore();
builder.Services.AddDataProtection()
                .PersistKeysToAzureBlobStorage(
                    connectionString: _azureStorage,
                    blobName: "keys.xml",
                    containerName: "keys"
                );

// allow the front end access to connect to the server
builder.Services.AddCors(corsOptions =>
{
    corsOptions.AddPolicy(name: _frontEndCorsPolicy, policy =>
    {
        policy
            .WithOrigins(_frontEndUrl)
            .AllowCredentials()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// add orleans for distributed state persistence
builder.Host.UseOrleans((hostBuilderContext, siloBuilder) =>
{
    siloBuilder
        .Configure<SiloOptions>(options =>
        {
            options.SiloName = $"silo_{Environment.MachineName}_{Random.Shared.Next(100)}";
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
        .UseAzureStorageClustering(options => options.ConfigureTableServiceClient(_azureStorage))
        .AddAzureTableGrainStorageAsDefault(options => options.ConfigureTableServiceClient(_azureStorage))
        .UseDashboard(dashboardOptions => dashboardOptions.HostSelf = false)
        .ConfigureServices((context, services) =>
        {
            services.AddSingleton<ISelfieServer, SelfieServer>();
            services.AddHostedService<SelfieServerWorker>();
        })
        ;
});
builder.Services.AddServicesForSelfHostedDashboard();

// add real-time
builder.Services.AddSignalR().AddAzureSignalR(options =>
{
    options.ConnectionString = _azureSignalR;
});
builder.Services.AddApplicationInsightsTelemetry();

// build the app
var app = builder.Build();

// ready the app for receiving Dapr events
app.UseCloudEvents();
app.MapSubscribeHandler();

// add the CORS policy created earlier
app.UseCors(_frontEndCorsPolicy);

// map the SignalR Hub endpoint the HTML/JavaScript UI will use
app.MapHub<SelfieHub>("/hubs/selfies");

// map the minimal API route Dapr calls when it receives messages
app.MapPost("/selfies",
    [Topic("selfieapppubsub", "incoming")] async (
        Selfie selfie,
        IGrainFactory grainFactory
    ) =>
    {
        Console.WriteLine("Subscriber received : " + selfie.Url);
        var selfieServerDirectory = grainFactory.GetGrain<ISelfieServerDirectory>(0);
        await selfieServerDirectory.ReceiveSelfie(selfie);
        return Results.Ok(selfie);
    });

// start the back end server
app.UseOrleansDashboard();
app.Run();

// the payload of a Selfie message
public record Selfie([property: JsonPropertyName("url")] string Url);