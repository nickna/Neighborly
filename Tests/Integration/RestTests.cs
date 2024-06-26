﻿using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Neighborly.Tests.Integration;

[TestFixture]
public class RestTests
{
    [Test]
    public async Task Vector_Can_Be_Added_Retrieved_And_Deleted()
    {
        // Arrange
        Environment.SetEnvironmentVariable("PROTO_REST", "true");
        using var factory = new WebApplicationFactory<Program>();

        using var client = factory.CreateClient();
        Vector vector = new([1f, 2f, 3f]);

        // Act 1: Add a vector
        var response = await client.PostAsJsonAsync("/vector", vector).ConfigureAwait(true);

        // Assert 1: Vector was added
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), "Vector was not added");
        var vectorUri = response.Headers.Location;

        // Act 2: Get the vector
        response = await client.GetAsync(vectorUri).ConfigureAwait(true);

        // Assert 2: Vector was found
        Assert.That(response.IsSuccessStatusCode, Is.True, "Vector was not found");

        // Act 3: Delete the vector
        response = await client.DeleteAsync(vectorUri).ConfigureAwait(true);

        // Assert 3: Vector was deleted
        Assert.That(response.IsSuccessStatusCode, Is.True, "Vector was not deleted ");

        // Act 4: Get the vector again
        response = await client.GetAsync(vectorUri).ConfigureAwait(true);

        // Assert 4: Vector was not found (because it was deleted)
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound), "Vector was not deleted");
    }

    [Test]
    public async Task Vectors_Can_Be_Cleared()
    {
        // Arrange
        Environment.SetEnvironmentVariable("PROTO_REST", "true");
        using var factory = new WebApplicationFactory<Program>();

        using var client = factory.CreateClient();
        Vector vector = new([1f, 2f, 3f]);
        
        var response = await client.PostAsJsonAsync("/vector", vector).ConfigureAwait(true);
        var vectorUri = response.Headers.Location;

        // Act
        response = await client.DeleteAsync("/db/clear").ConfigureAwait(true);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent), "Vectors were not deleted");
        response = await client.GetAsync(vectorUri).ConfigureAwait(true);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound), "Vectors is still found");
    }
}
