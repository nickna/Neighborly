# Getting Started with Neighborly

## Overview

Neighborly is an open-source vector database built with C# and .NET 8. It provides two flexible deployment options:

- **gRPC API**: Docker-containerized server for client-server architectures
- **Client Library**: NuGet package for direct integration (similar to SQLite)

The project supports cross-platform deployment (Windows, Xbox, iOS, macOS, Android, Linux) with disk-backed storage, memory caching, and advanced search algorithms.

## Installation Options

### Option 1: Docker Image (Web Server)

To use Neighborly as a web server, pull the Docker image from [DockerHub](https://hub.docker.com/r/nick206/neighborly):

```shell
docker pull nick206/neighborly:latest
```

Run the container:

```shell
docker run -p 8080:8080 -e PROTO_GRPC=true -e PROTO_REST=true nick206/neighborly:latest
```

This starts the Neighborly server with gRPC API access at localhost:8080.

### Option 2: NuGet Package (Client Library)

Install the [NuGet package](https://www.nuget.org/packages/Neighborly) in your .NET projects:

```powershell
PM> NuGet\Install-Package Neighborly
```

Import the necessary namespaces:

```csharp
using Neighborly;
using Neighborly.Databases;
```

## Prerequisites

- .NET 8 SDK or later
- Docker (for containerized deployment)
- Compatible operating system (Windows, macOS, Linux, iOS, Android, Xbox)

## Quick Start Example

```csharp
using Neighborly;

// Create a new vector database
var db = new VectorDatabase();

// Add vectors
var vector1 = new Vector(new float[] { 1.0f, 2.0f, 3.0f });
var vector2 = new Vector(new float[] { 4.0f, 5.0f, 6.0f });

await db.AddAsync(vector1);
await db.AddAsync(vector2);

// Search for similar vectors
var query = new Vector(new float[] { 1.1f, 2.1f, 3.1f });
var results = await db.SearchAsync(query, k: 5);

// Save the database
await db.SaveAsync("my_database.db");
```

## Next Steps

- [Architecture Overview](02-architecture.md)
- [Search Algorithms](03-search-algorithms.md)
- [API Reference](05-api-reference.md)
- [Examples and Samples](06-examples.md)