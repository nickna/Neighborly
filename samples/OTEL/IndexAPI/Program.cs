using Google.Protobuf;
using Grpc.Core;
using IndexAPI;
using Microsoft.Extensions.Options;
using Neighborly.API.Protos;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using static Neighborly.API.Protos.Vector;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<FakeEmbeddingService>();

builder.Services.Configure<VectorSettings>(builder.Configuration.GetSection(VectorSettings.Name));

builder.Services.AddGrpcClient<VectorClient>(static (serviceProvider, options) =>
{
    var settings = serviceProvider.GetRequiredService<IOptions<VectorSettings>>().Value;
    options.Address = settings.Uri;
    options.ChannelOptionsActions.Add(static channelOptions =>
    {
        channelOptions.Credentials = ChannelCredentials.Insecure;
    });
});

builder.Services.AddSingleton<Instrumentation>();
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName: "SomeClient"))
    .WithTracing(tracing =>
    {
        if (builder.Environment.IsDevelopment())
        {
            tracing.SetSampler(new AlwaysOnSampler());
        }

        tracing.AddSource(Instrumentation.ActivitySourceName)
            .AddAspNetCoreInstrumentation()
            .AddGrpcClientInstrumentation()
            .AddGrpcClientInstrumentation();
    })
    .WithMetrics(metrics =>
    {
        metrics.AddRuntimeInstrumentation()
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();
    })
    .UseOtlpExporter();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapPost("/", static async (string text, FakeEmbeddingService embeddingService, VectorClient vectorClient, CancellationToken cancellationToken) =>
{
    float[] embeddings = await embeddingService.GetEmbeddingsAsync(text, cancellationToken).ConfigureAwait(false);
    Neighborly.Vector vector = new(embeddings, text);
    ByteString bytes = ByteString.CopyFrom(vector.ToBinary());
    VectorMessage vectorMessage = new() { Values = bytes };
    var response = await vectorClient.AddVectorAsync(new AddVectorRequest { Vector = vectorMessage }, cancellationToken: cancellationToken).ConfigureAwait(false);
    if (response.Success)
    {
        return Results.Created($"/{vector.Id}", vector);
    }
    else
    {
        return Results.BadRequest(response.Message);
    }
})
.WithName("AddTextToIndex")
.WithOpenApi();

await app.RunAsync().ConfigureAwait(false);
