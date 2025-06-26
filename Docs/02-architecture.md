# Neighborly Architecture

## Overview

Neighborly is designed as a high-performance, cross-platform vector database with a modular architecture that supports both embedded and client-server deployments.

## Core Components

### VectorDatabase (`Neighborly/VectorDatabase.cs`)
The main database class that manages vector storage, indexing, and search operations. Features:
- Uses `ReaderWriterLockSlim` for thread safety
- Implements automatic background index rebuilding
- Supports asynchronous operations throughout
- Provides instrumentation and telemetry

### SearchService (`Neighborly/Search/SearchService.cs`)
Manages search algorithms and provides:
- k-NN (k-Nearest Neighbors) search
- ANN (Approximate Nearest Neighbors) search
- Range search capabilities
- Cosine similarity search
- Multiple search algorithm implementations (KD-Tree, Ball Tree, LSH, Linear Search)

### VectorService (`API.gRPC/Services/VectorService.cs`)
gRPC service implementation that exposes vector database operations over the network for client-server architectures.

## Key Subsystems

### Distance Calculators (`Neighborly/Distance/`)
Multiple distance calculation algorithms:
- **Euclidean**: Standard L2 distance
- **Cosine**: Cosine similarity for high-dimensional data
- **Manhattan**: L1 distance (city block distance)
- **Chebyshev**: L∞ distance (maximum difference)
- **Minkowski**: Generalized distance metric

### ETL System (`Neighborly/ETL/`)
Data import/export functionality supporting:
- **CSV**: Comma-separated values
- **JSON**: JavaScript Object Notation
- **JSONZ**: Compressed JSON
- **Parquet**: Columnar storage format
- **HDF5**: Hierarchical data format

### Search Algorithms (`Neighborly/Search/`)
- **KD-Tree**: Binary space partitioning for low-dimensional data
- **Ball Tree**: Metric tree for high-dimensional data
- **HNSW**: Hierarchical Navigable Small World graphs
- **LSH**: Locality-Sensitive Hashing for approximate search
- **Linear Search**: Brute-force search for small datasets
- **Binary/Product Quantization**: Compression techniques

### Memory Management
- **Memory-mapped files**: Efficient large dataset handling
- **MemoryMappedList**: Custom implementation for vector storage
- **Compression**: FpZip compression with native libraries
- **Caching**: Intelligent memory caching strategies

## Project Structure

```
Neighborly/                 # Core vector database library
├── Distance/              # Distance calculation algorithms
├── ETL/                   # Data import/export functionality
├── Search/                # Search algorithm implementations
└── fpzip_runtimes/        # Platform-specific compression libraries

API.gRPC/                  # gRPC API server with Docker support
├── Services/              # gRPC service implementations
├── Models/                # Data transfer objects
└── Protos/                # Protocol buffer definitions

Tests/                     # NUnit test suite
├── Integration/           # Integration tests for gRPC/REST
├── Helpers/               # Test utilities and mocks
└── Distance/              # Distance calculator tests

Adapters.SemanticKernel/   # Semantic Kernel integration
samples/                   # Example implementations
├── OTEL/                  # OpenTelemetry observability example
└── SemanticKernel/        # Semantic Kernel integration example

Neighborly.Python/         # Python bindings (experimental)
```

## Thread Safety and Concurrency

### Synchronization Strategy
- **ReaderWriterLockSlim**: Enables multiple concurrent readers with exclusive writers
- **Asynchronous operations**: Non-blocking I/O throughout the system
- **Background indexing**: Separate thread for index maintenance
- **Cancellation support**: Graceful operation cancellation via `CancellationToken`

### Index Service
Automatic background process that:
- Monitors database modifications
- Rebuilds search indexes after a 5-second delay
- Batches updates for efficiency
- Runs on low-priority thread to avoid interference
- Provides telemetry for monitoring

## Platform Considerations

### Desktop/Server Platforms
- Full feature set including background indexing
- Optimized for high-throughput scenarios
- Complete memory management capabilities

### Mobile Platforms (Android/iOS)
- Background indexing disabled to conserve battery
- Reduced background processing due to platform restrictions
- Manual index rebuilding recommended at strategic points:
  - App startup
  - When device is charging
  - After significant database modifications

### Cross-Platform Compatibility
- Native compression libraries for each platform
- Platform-specific optimizations
- Consistent API across all supported platforms

## Data Flow

1. **Vector Input** → Validation → Storage in MemoryMappedList
2. **Indexing** → Background service rebuilds search structures
3. **Search Request** → Algorithm selection → Index traversal → Results
4. **Persistence** → Compression → Disk storage → Recovery on restart

## Performance Characteristics

- **Memory Efficiency**: Memory-mapped files reduce RAM requirements
- **Disk I/O**: Optimized for SSD storage with minimal random access
- **Concurrency**: High read throughput with protected write operations
- **Scalability**: Handles datasets larger than available memory
- **Search Speed**: Sub-linear search complexity for most algorithms