using Google.Protobuf;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Mvc.Testing;
using Neighborly.API.Protos;
using static Neighborly.API.Protos.Vector;

namespace Neighborly.Tests;

[TestFixture]
public class GrpcTests
{
    [Test]
    public async Task Vector_Can_Be_Added_Retrieved_And_Deleted()
    {
        // Arrange
        Environment.SetEnvironmentVariable("PROTO_GRPC", "true");
        using var factory = new WebApplicationFactory<Program>();

        using var client = factory.CreateClient();
        using var channel = GrpcChannel.ForAddress(factory.Server.BaseAddress, new GrpcChannelOptions
        {
            HttpClient = client
        });

        VectorClient grpcClient = new(channel);

        Vector vector = new([1f, 2f, 3f]);
        AddVectorRequest request = new()
        {
            Vector = new()
            {
                Values = ByteString.CopyFrom(vector.ToBinary())
            }
        };

        // Act 1: Add a vector
        var response = await grpcClient.AddVectorAsync(request).ConfigureAwait(true);
        Assert.That(response.Success, Is.True, "Vector was not added");
    }
}
