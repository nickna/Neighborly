# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Neighborly is an open-source vector database built with C# and .NET 8. It provides two deployment options:
- **gRPC API**: Docker-containerized server for client-server architectures
- **Client Library**: NuGet package for direct integration (similar to SQLite)

The project supports cross-platform deployment (Windows, Xbox, iOS, macOS, Android, Linux) with disk-backed storage, memory caching, and advanced search algorithms.

## Build and Test Commands

### Building
```bash
dotnet restore
dotnet build --configuration Release --no-restore
```

**Note:** If you encounter Aspire workload errors, run:
```bash
dotnet workload install aspire
```

### Testing
```bash
dotnet test --configuration Release --no-build --verbosity normal
```

### Running Tests for Specific Project
```bash
dotnet test Tests/Tests.csproj --configuration Release
```

### Development Setup
Run the PowerShell setup script to configure development environment:
```powershell
.\setup-dev.ps1
```

## Architecture

### Core Components

**VectorDatabase** (`Neighborly/VectorDatabase.cs`): Main database class that manages vector storage, indexing, and search operations. Uses `ReaderWriterLockSlim` for thread safety and implements automatic background index rebuilding.

**SearchService** (`Neighborly/Search/SearchService.cs`): Manages search algorithms including KD-Tree and Ball Tree implementations. Provides k-NN, ANN, range search, and cosine similarity search capabilities.

**VectorService** (`API.gRPC/Services/VectorService.cs`): gRPC service implementation that exposes vector database operations over the network.

### Key Subsystems

- **Distance Calculators**: Multiple algorithms (Euclidean, Cosine, Manhattan, Chebyshev, Minkowski) in `Neighborly/Distance/`
- **ETL System**: Data import/export for CSV, JSON, Parquet, HDF5 formats in `Neighborly/ETL/`
- **Search Algorithms**: KD-Tree, Ball Tree, LSH, and Linear Search in `Neighborly/Search/`
- **Compression**: FpZip compression with native libraries for multiple platforms
- **Memory Management**: Memory-mapped files for efficient large dataset handling

### Project Structure

- `Neighborly/`: Core vector database library
- `API.gRPC/`: gRPC API server with Docker support
- `Tests/`: NUnit test suite with integration and unit tests
- `Adapters.SemanticKernel/`: Semantic Kernel integration adapter
- `samples/`: Example implementations including OTEL observability
- `Neighborly.Python/`: Python bindings (experimental)

### Thread Safety and Concurrency

The system uses `ReaderWriterLockSlim` for concurrent access control and implements asynchronous operations throughout. Background index rebuilding runs on a separate thread with a 5-second delay after modifications to batch updates efficiently.

### Platform-Specific Considerations

Mobile platforms (Android/iOS) have limited background processing capabilities, so automatic index rebuilding is disabled on these platforms to conserve battery and comply with platform restrictions.

## Testing Framework

Uses NUnit with test categories for different types of tests. Integration tests are available for gRPC and REST endpoints. Test utilities and mocks are provided in `Tests/Helpers/`.