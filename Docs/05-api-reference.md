# API Reference

## Overview

Neighborly provides APIs for both embedded usage (NuGet package) and client-server scenarios (gRPC/REST). This reference covers core classes, methods, and usage patterns.

## Core Classes

### VectorDatabase

The main database class for managing vector storage and operations.

#### Constructor
```csharp
public VectorDatabase(string? id = null)
```
- `id`: Optional identifier for telemetry and logging

#### Key Properties
- `Count`: Number of vectors in the database
- `IsReadOnly`: Whether the database is in read-only mode
- `Tags`: Access to the tagging system
- `Vectors`: Direct access to the vector collection

#### Core Methods

##### AddAsync
```csharp
public async Task AddAsync(Vector vector, CancellationToken cancellationToken = default)
public async Task AddRangeAsync(IEnumerable<Vector> vectors, CancellationToken cancellationToken = default)
```
Add single or multiple vectors to the database.

##### SearchAsync
```csharp
public async Task<List<Vector>> SearchAsync(Vector query, int k = 5, SearchAlgorithm algorithm = SearchAlgorithm.Auto, CancellationToken cancellationToken = default)
```
Perform k-nearest neighbor search.

Parameters:
- `query`: Query vector
- `k`: Number of nearest neighbors to return
- `algorithm`: Search algorithm to use (Auto, KDTree, BallTree, HNSW, LSH, Linear)
- `cancellationToken`: Cancellation token

##### RangeSearchAsync
```csharp
public async Task<List<Vector>> RangeSearchAsync(Vector query, float radius, SearchAlgorithm algorithm = SearchAlgorithm.Auto, CancellationToken cancellationToken = default)
```
Find all vectors within a specified distance.

##### LoadAsync / SaveAsync
```csharp
public async Task LoadAsync(string path, bool createOnNew = true, CancellationToken cancellationToken = default)
public async Task SaveAsync(string path, CancellationToken cancellationToken = default)
```
Load and save database from/to disk.

##### ImportDataAsync / ExportDataAsync
```csharp
public async Task ImportDataAsync(string path, bool isDirectory, ContentType contentType, CancellationToken cancellationToken = default)
public async Task ExportDataAsync(string path, ContentType contentType, CancellationToken cancellationToken = default)
```
Import and export data in various formats.

##### UpdateAsync / RemoveAsync
```csharp
public async Task UpdateAsync(Guid id, Vector newVector, CancellationToken cancellationToken = default)
public async Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken = default)
```
Update or remove vectors by ID.

##### Index Management
```csharp
public async Task RebuildSearchIndexesAsync(CancellationToken cancellationToken = default)
public async Task RebuildTagsAsync(CancellationToken cancellationToken = default)
```
Manually rebuild search indexes and tag mappings.

### Vector

Represents a single vector with metadata.

#### Constructors
```csharp
public Vector(float[] values, Guid? id = null, short[]? tags = null, string? originalText = null)
public Vector(ReadOnlySpan<byte> bytes)  // From binary data
```

#### Properties
- `Id`: Unique identifier (Guid)
- `Values`: Vector coordinates (float[])
- `Tags`: Category tags (short[])
- `OriginalText`: Original text content (string?)
- `Dimension`: Number of dimensions (int)

#### Methods
```csharp
public float Distance(Vector other, IDistanceCalculator? calculator = null)
public byte[] ToBinary()
public Vector Clone()
```

### SearchService

Manages search algorithm implementations.

#### Methods
```csharp
public async Task<List<Vector>> SearchAsync(Vector query, int k, SearchAlgorithm algorithm = SearchAlgorithm.Auto)
public async Task<List<Vector>> RangeSearchAsync(Vector query, float radius, SearchAlgorithm algorithm = SearchAlgorithm.Auto)
```

### VectorTags

Manages the tagging system.

#### Methods
```csharp
public HashSet<int> GetVectorIdsByTag(short tag)
public HashSet<int> GetVectorIdsByTags(short[] tags)        // Intersection
public HashSet<int> GetVectorIdsByAnyTag(short[] tags)      // Union
public short[] GetTagsByVectorId(int vectorId)
public void BuildMap()  // Rebuild tag mappings
```

## Distance Calculators

### IDistanceCalculator Interface
```csharp
public interface IDistanceCalculator
{
    float CalculateDistance(ReadOnlySpan<float> vector1, ReadOnlySpan<float> vector2);
    string Name { get; }
}
```

### Available Implementations
- `EuclideanDistanceCalculator`: L2 distance
- `CosineSimilarityCalculator`: Cosine similarity
- `ManhattanDistanceCalculator`: L1 distance
- `ChebyshevDistanceCalculator`: L∞ distance
- `MinkowskiDistanceCalculator`: Generalized Lp distance

## Enumerations

### SearchAlgorithm
```csharp
public enum SearchAlgorithm
{
    Auto,           // Automatic selection
    Linear,         // Brute force search
    KDTree,         // K-dimensional tree
    BallTree,       // Ball tree
    HNSW,           // Hierarchical NSW
    LSH,            // Locality-sensitive hashing
    BinaryQuantization,  // Binary quantized search
    ProductQuantization  // Product quantized search
}
```

### ContentType
```csharp
public enum ContentType
{
    CSV,
    JSON,
    JSONZ,      // Compressed JSON
    Binary,
    Parquet,
    HDF5
}
```

## gRPC API

### Service Definition (Vector.proto)
```protobuf
service VectorService {
    rpc AddVector(AddVectorRequest) returns (AddVectorResponse);
    rpc SearchVectors(SearchRequest) returns (SearchResponse);
    rpc GetVector(GetVectorRequest) returns (GetVectorResponse);
    rpc UpdateVector(UpdateVectorRequest) returns (UpdateVectorResponse);
    rpc DeleteVector(DeleteVectorRequest) returns (DeleteVectorResponse);
    rpc GetDatabaseInfo(GetDatabaseInfoRequest) returns (GetDatabaseInfoResponse);
}
```

### Message Types
```protobuf
message VectorDto {
    string id = 1;
    repeated float values = 2;
    repeated int32 tags = 3;
    string original_text = 4;
}

message SearchRequest {
    VectorDto query = 1;
    int32 k = 2;
    string algorithm = 3;
}

message SearchResponse {
    repeated VectorDto vectors = 1;
    repeated float distances = 2;
}
```

## REST API

### Endpoints

#### POST /vectors
Add a new vector to the database.

**Request Body:**
```json
{
    "id": "123e4567-e89b-12d3-a456-426614174000",
    "values": [1.0, 2.0, 3.0],
    "tags": [1, 2, 3],
    "originalText": "sample text"
}
```

#### POST /vectors/search
Search for similar vectors.

**Request Body:**
```json
{
    "query": {
        "values": [1.1, 2.1, 3.1]
    },
    "k": 5,
    "algorithm": "Auto"
}
```

**Response:**
```json
{
    "vectors": [
        {
            "id": "123e4567-e89b-12d3-a456-426614174000",
            "values": [1.0, 2.0, 3.0],
            "tags": [1, 2, 3],
            "originalText": "sample text"
        }
    ],
    "distances": [0.1732]
}
```

#### GET /vectors/{id}
Retrieve a specific vector by ID.

#### PUT /vectors/{id}
Update an existing vector.

#### DELETE /vectors/{id}
Remove a vector from the database.

#### GET /database/info
Get database information and statistics.

## Error Handling

### Common Exceptions
- `FileNotFoundException`: Database file not found
- `InvalidDataException`: Corrupt or invalid data
- `ArgumentException`: Invalid parameters
- `UnauthorizedAccessException`: Permission denied
- `IOException`: General I/O errors

### Error Response Format (REST)
```json
{
    "error": {
        "code": "VectorNotFound",
        "message": "Vector with ID 123 not found",
        "details": {}
    }
}
```

## Configuration

### HNSWConfig
```csharp
public class HNSWConfig
{
    public int M { get; set; } = 16;                    // Max connections per node
    public int EfConstruction { get; set; } = 200;      // Construction parameter
    public int Ef { get; set; } = 50;                   // Search parameter
    public int MaxM { get; set; } = 16;                 // Max connections
    public int MaxM0 { get; set; } = 32;                // Max connections layer 0
}
```

### Environment Variables (Docker)
- `PROTO_GRPC`: Enable gRPC endpoint (true/false)
- `PROTO_REST`: Enable REST endpoint (true/false)
- `ASPNETCORE_URLS`: Binding URLs
- `ASPNETCORE_ENVIRONMENT`: Environment (Development/Production)

## Usage Examples

### Basic Operations
```csharp
// Initialize database
var db = new VectorDatabase();

// Add vectors
var vector1 = new Vector(new float[] { 1.0f, 2.0f, 3.0f });
await db.AddAsync(vector1);

// Search
var query = new Vector(new float[] { 1.1f, 2.1f, 3.1f });
var results = await db.SearchAsync(query, k: 5);

// Save/Load
await db.SaveAsync("database.db");
await db.LoadAsync("database.db");
```

### Tag-based Operations
```csharp
// Add vector with tags
var taggedVector = new Vector(
    values: new float[] { 1.0f, 2.0f, 3.0f },
    tags: new short[] { 1, 2, 3 }
);
await db.AddAsync(taggedVector);

// Find vectors by tag
var vectorIds = db.Tags.GetVectorIdsByTag(1);
var vectors = vectorIds.Select(id => db.Vectors[id]).ToList();
```

### Advanced Search
```csharp
// Range search
var nearbyVectors = await db.RangeSearchAsync(query, radius: 0.5f);

// Algorithm-specific search
var hnswResults = await db.SearchAsync(query, k: 10, SearchAlgorithm.HNSW);

// Custom distance calculator
var cosineCalculator = new CosineSimilarityCalculator();
var distance = vector1.Distance(vector2, cosineCalculator);
```