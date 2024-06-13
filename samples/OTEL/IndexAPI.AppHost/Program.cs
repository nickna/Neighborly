var builder = DistributedApplication.CreateBuilder(args);

var db = builder.AddProject<Projects.API>("db")
    .WithEnvironment("PROTO_GRPC", "true");
builder.AddProject<Projects.IndexAPI>("api")
    .WithExternalHttpEndpoints()
    .WithReference(db);

await builder.Build().RunAsync().ConfigureAwait(false);