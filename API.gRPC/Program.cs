using Microsoft.Extensions.Hosting;
using Neighborly;
using Neighborly.API;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddGrpc();
builder.Services.AddSingleton<VectorDatabase>();

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

// Load Database on application start
vectorDatabase.Load(databasePath);

// Save Database on application shutdown
lifetime.ApplicationStopping.Register(() =>
{
    vectorDatabase.Save(databasePath);
});

// Configure the HTTP request pipeline.
app.MapGrpcService<VectorService>();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();
