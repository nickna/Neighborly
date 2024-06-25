# Neighborly
## An Open-Source Vector Database

![neighborly-header](https://github.com/nickna/Neighborly/assets/4017153/2dd8a22d-511d-4457-bde5-ac4ceaecf166)

Neighborly is a versatile open-source vector database built with C#, designed to efficiently store and retrieve high-dimensional vector data. It offers two flexible deployment options: a gRPC API in a Docker container and a lightweight bundled library. With its cross-platform compatibility, Neighborly can be seamlessly integrated into a wide range of applications, including Windows, Xbox, iOS, macOS, Android, and Linux.

# Features
* Disk-backed Storage: Efficiently handle large volumes of vector data with memory caching and disk storage for persistence. 
* High Performance: Optimized for fast read and write operations.
* API (gRPC): Access Neighborly's functionality through a gRPC API hosted in a Docker container.
* Client Library (NuGet): Integrate Neighborly as a minimal library into your projects, similar to SQLite.
* Cross-Platform Compatibility: Leverage Neighborly on various platforms, including Windows, Xbox, iOS, macOS, Android, and Linux.
* Advanced Search Algorithms: Utilize k-NN, ANN, range search, and cosine similarity search for efficient vector queries.
* Unit Testing: Ensure reliability and stability with a comprehensive test suite.

# Getting Started

## Web Server (Docker Image)
To use Neighborly as a web server, you can pull the Docker image from [DockerHub](https://hub.docker.com/r/nick206/neighborly):

```shell
docker pull nick206/neighborly:latest
```

Once the image is pulled, you can run the container using the following command:

```shell
docker run -p 8080:8080 -e PROTO_GRPC=true -e PROTO_REST=true nick206/neighborly:latest
```

This will start the Neighborly server, and you can access the gRPC API at localhost:8080.

## Client Library (NuGet Package)
To use Neighborly as a client library in your .NET projects, you can install the [NuGet](https://www.nuget.org/packages/Neighborly) package using the following command:

```powershell
PM> NuGet\Install-Package Neighborly
```

After installing the package, you can use the Neighborly client library in your code by importing the necessary namespaces:

```csharp
using Neighborly;

using Neighborly.Databases;
```

# Deployment Options
## API Server for Web-based Applications
Neighborly provides a gRPC API hosted in a Docker container, facilitating client-server architecture. 

## Client Library for Desktop and Mobile Applications
Neighborly can be used as a lightweight bundled library, similar to SQLite. Add a reference to the compiled DLL (or NuGet package) and utilize the provided classes and methods for managing vector data directly in your projects. The library can be seamlessly integrated into applications targeting Windows, Xbox, iOS, macOS, Android, and Linux platforms.

# Search Algorithms
Neighborly offers a range of advanced search algorithms to efficiently query vector data:

* k-Nearest Neighbors (k-NN): Find the k closest vectors to a given query vector based on a specified distance metric.
* Approximate Nearest Neighbor (ANN): Quickly find approximate nearest neighbors using techniques like locality-sensitive hashing (LSH).
* Range Search: Retrieve all vectors within a specified distance from a query vector.
* Cosine Similarity Search: Identify vectors with the highest cosine similarity to the query vector, ideal for text and high-dimensional data.

# Contributing
We welcome contributions! If you have ideas for new features or have found bugs, please open an issue or submit a pull request. For major changes, please discuss them in an issue first.

# License
This project is licensed under the MIT License. See the [LICENSE](LICENSE.txt) file for details.

# Contact
For any questions or further assistance, feel free to contact [![GitHub](https://img.shields.io/badge/GitHub-nickna-blue)](https://github.com/nickna).