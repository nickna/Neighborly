# GEMINI.md

This document provides context and instructions for Gemini, the AI coding assistant, to understand and interact with the Neighborly project.

## Project Overview

Neighborly is an open-source vector database built with **C# and .NET 10**. It provides two deployment options:
- **gRPC API**: Docker-containerized server for client-server architectures.
- **Client Library**: NuGet package for direct integration (similar to SQLite).

The project supports cross-platform deployment (Windows, Linux, macOS, Android, iOS) with disk-backed storage, memory caching, and advanced search algorithms.

## Build and Test

This project uses the .NET CLI for building and testing.

### Build
To build the project:
```bash
dotnet restore
dotnet build --configuration Release --no-restore
```

### Test
To run all tests:
```bash
dotnet test --configuration Release --no-build --verbosity normal
```

To run tests for a specific project:
```bash
dotnet test Tests/Tests.csproj --configuration Release
```

### Development Setup
To configure the development environment (PowerShell):
```powershell
.\setup-dev.ps1
```

## Architecture

### Core Components
- **VectorDatabase** (`Neighborly/VectorDatabase.cs`): Main database class managing vector storage, indexing, and search. Uses `ReaderWriterLockSlim` for thread safety.
- **SearchService** (`Neighborly/Search/SearchService.cs`): Manages search algorithms (KD-Tree, Ball Tree, HNSW, LSH).
- **VectorService** (`API.gRPC/Services/VectorService.cs`): gRPC service exposing operations over the network.

### Key Subsystems
- **Distance Calculators**: Euclidean, Cosine, Manhattan, Chebyshev, Minkowski in `Neighborly/Distance/`.
- **ETL System**: Import/export for CSV, JSON, Parquet, HDF5 in `Neighborly/ETL/`.
- **Search Algorithms**: KD-Tree, Ball Tree, LSH, Linear Search in `Neighborly/Search/`.
- **Compression**: FpZip compression with native libraries.
- **Memory Management**: Memory-mapped files for large datasets.

### Thread Safety
- Uses `ReaderWriterLockSlim` for concurrent access.
- Asynchronous operations throughout.
- Background index rebuilding runs on a separate thread (disabled on mobile platforms to conserve battery).

## Project Structure

- `Neighborly/`: Core vector database library.
- `API.gRPC/`: gRPC API server with Docker support.
- `Tests/`: NUnit test suite (Unit & Integration).
- `Adapters.SemanticKernel/`: Microsoft Semantic Kernel integration adapter.
- `samples/`: Example implementations.
- `Neighborly.Python/`: Python bindings.

## Coding Conventions

- **Commit Messages**: Present tense, imperative mood (e.g., "Add feature"). First line < 72 chars.
- **C# Style**: Follow standard C# coding conventions.
- **File Endings**: Ensure all files end with a newline.

## Configuration

- **Target Framework**: .NET 10 (`net10.0`)
- **Version Control**: Git