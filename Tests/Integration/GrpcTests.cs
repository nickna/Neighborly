using Google.Protobuf;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Mvc.Testing;
using Neighborly.API.Protos;
using static Neighborly.API.Protos.Vector;
using CoreVector = Neighborly.Vector;

namespace Neighborly.Tests.Integration;

[TestFixture]
public class GrpcTests
{
    [Test]
    public async Task Vector_Can_Be_Added_Retrieved_And_Updated()
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

        CoreVector vector = new([1f, 2f, 3f]);

        // Act 1: Add a vector
        var addVectorResponse = await grpcClient.AddVectorAsync(new AddVectorRequest()
        {
            Vector = new()
            {
                Values = ByteString.CopyFrom(vector.ToBinary())
            }
        }).ConfigureAwait(true);

        // Assert 1: Vector was added
        Assert.That(addVectorResponse.Success, Is.True, "Vector was not added");

        // Act 2: Get the vector
        var getVectorResponse = await grpcClient.GetVectorByIdAsync(new GetVectorByIdRequest
        {
            Id = vector.Id.ToString()
        }).ConfigureAwait(true);
        var vectorFromResponse = new Vector(getVectorResponse.Vector.Values.ToByteArray());

        // Assert 2: Vector was found
        Assert.That(vectorFromResponse, Is.EqualTo(vector), "Vector was not found");

        // Arrange 3: Prepare updated vector
        var updatedVector = new CoreVector(vector.ToBinary());
        updatedVector.Values[0] = 4f;

        // Act 3: Update the vector
        var updateVectorResponse = await grpcClient.UpdateVectorAsync(new UpdateVectorRequest
        {
            Id = updatedVector.Id.ToString(),
            Vector = new()
            {
                Values = ByteString.CopyFrom(updatedVector.ToBinary())
            }
        }).ConfigureAwait(true);

        // Assert 3: Vector was updated
        Assert.That(updateVectorResponse.Success, Is.True, "Vector was not updated");
    }
}
