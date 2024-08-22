using Neighborly;
using Neighborly.API;
using Neighborly.API.Mappers;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddLogging();
builder.Services.AddGrpc();
builder.Services.AddSingleton<VectorDatabase>();
builder.Services.AddSingleton<VectorMapper>();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName: "Neighborly"))
    .WithTracing(builder =>
    {
        builder.AddAspNetCoreInstrumentation()
            .AddInstrumentation<Instrumentation>()
            .AddSource(Instrumentation.Instance.ActivitySource.Name);
    })
    .WithMetrics(builder =>
    {
        builder.AddRuntimeInstrumentation()
            .AddAspNetCoreInstrumentation()
            .AddInstrumentation<Instrumentation>()
            .AddMeter(Instrumentation.Instance.Meter.Name);
    })
    .UseOtlpExporter();

var app = builder.Build();

// Get the application lifetime object
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();

// Get the VectorDatabase instance
var vectorDatabase = app.Services.GetRequiredService<VectorDatabase>();

var databasePath = Environment.GetEnvironmentVariable("DATABASE_PATH");
if (string.IsNullOrEmpty(databasePath))
{
    databasePath = "";
}

bool gRPCEnable = Environment.GetEnvironmentVariable("PROTO_GRPC") == "true";
bool RESTEnable = Environment.GetEnvironmentVariable("PROTO_REST") == "true";


// Shutdown the application if both gRPC and REST are disabled
if (!gRPCEnable && !RESTEnable)
{
    app.Logger.AppShuttingDownAsNoProtocolsEnabled();
    await app.StopAsync();
    return;
}

// Load Database on application start
await vectorDatabase.LoadAsync(databasePath);

// ToBinaryStream Database on application shutdown
lifetime.ApplicationStopping.Register(async () =>
{
    if (!string.IsNullOrEmpty(databasePath))
    {
        await vectorDatabase.SaveAsync(databasePath).ConfigureAwait(false);
    }
});

// Configure the gRPC API
if (gRPCEnable)
{
    app.MapGrpcService<VectorService>();
}

// Configure the REST API
if (RESTEnable)
{
    var mapper = app.Services.GetRequiredService<VectorMapper>();
    API.Services.RestServices.MapVectorRoutes(app, mapper);
}

await app.RunAsync().ConfigureAwait(true);

// Helper to make this visible to tests
internal partial class Program { }