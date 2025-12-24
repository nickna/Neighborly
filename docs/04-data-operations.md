# Data Operations in Neighborly

## Overview

Neighborly provides comprehensive data import/export capabilities and efficient storage mechanisms. This document covers ETL processes, supported formats, vector storage, compression, and data management best practices.

## Supported File Formats

### CSV (Comma-Separated Values)
- **Use Case**: Simple tabular vector data
- **Format**: Each row represents a vector, columns are dimensions
- **Best For**: Small to medium datasets, human-readable format
- **Performance**: Good for structured data with consistent dimensions

### JSON (JavaScript Object Notation)
- **Use Case**: Complex structured data with metadata
- **Format**: Array of vector objects with additional properties
- **Best For**: Data with rich metadata, tags, or variable structures
- **Features**: Supports vector metadata, tags, and original text

### JSONZ (Compressed JSON)
- **Use Case**: Large JSON datasets requiring compression
- **Format**: GZip-compressed JSON
- **Best For**: Network transfer, storage optimization
- **Benefit**: Reduced file size while maintaining JSON flexibility

### Parquet
- **Use Case**: Analytics and big data workflows
- **Format**: Columnar storage with efficient compression
- **Best For**: Large datasets, integration with data processing frameworks
- **Advantages**: Excellent compression, fast column access

### HDF5 (Hierarchical Data Format)
- **Use Case**: Scientific computing and large numerical datasets
- **Format**: Binary format with hierarchical organization
- **Best For**: Multi-dimensional arrays, scientific applications
- **Features**: Self-describing, cross-platform compatibility

### Binary
- **Use Case**: Maximum performance and minimal storage
- **Format**: Custom binary serialization
- **Best For**: Production deployments, performance-critical applications
- **Advantages**: Fastest loading/saving, minimal overhead

## ETL (Extract, Transform, Load) Process

### Import Process

1. **Extract**: Read data from source files or directories
2. **Transform**: Convert data into Vector objects compatible with Neighborly
3. **Load**: Add transformed vectors to the database

```csharp
// Import data asynchronously
await db.ImportDataAsync(path, isDirectory: true, ContentType.JSON);
```

### Export Process

1. **Extract**: Retrieve vectors from the database
2. **Transform**: Convert vectors to the desired output format
3. **Load**: Write formatted data to the specified location

```csharp
// Export data asynchronously
await db.ExportDataAsync(outputPath, ContentType.Parquet);
```

### Batch Processing

- **Directory Import**: Process multiple files in a directory
- **Incremental Updates**: Support for partial data updates
- **Progress Tracking**: Monitor import/export progress
- **Error Handling**: Robust error recovery and reporting

## Vector Storage and Representation

### Vector Class Structure

```csharp
public class Vector
{
    public Guid Id { get; }              // Unique identifier
    public float[] Values { get; }       // Vector coordinates
    public short[] Tags { get; }         // Category tags
    public string OriginalText { get; }  // Source text (optional)
    public int Dimension { get; }        // Number of dimensions
}
```

### MemoryMappedList Storage

Neighborly uses a sophisticated storage system based on memory-mapped files:

#### Architecture
- **Index File**: Stores metadata (ID, offset, length) for each vector
- **Data File**: Contains binary vector representations
- **Memory Mapping**: Enables efficient access to large datasets without loading everything into RAM

#### Key Features
- **Disk-backed Storage**: Handle datasets larger than available memory
- **Fast Random Access**: Quick retrieval by index or ID
- **Lazy Loading**: Load vectors on-demand
- **Tombstone Deletion**: Mark deleted vectors without immediate file reorganization

#### Operations
```csharp
// Add vector
await vectorList.AddAsync(vector);

// Get vector by ID
var vector = await vectorList.GetVectorAsync(id);

// Update existing vector
await vectorList.UpdateAsync(index, newVector);

// Remove vector (tombstone)
await vectorList.RemoveAsync(index);
```

### Storage Optimizations

#### Defragmentation
```csharp
// Remove tombstoned entries and reorganize data
await vectorList.DefragAsync();

// Incremental defragmentation for large datasets
await vectorList.DefragBatchAsync(batchSize: 1000);
```

#### Compression
- **GZip**: Applied during database save operations
- **FpZip**: Floating-point specific compression (planned)
- **Binary Quantization**: Reduce precision for memory savings
- **Product Quantization**: Subspace compression for large vectors

## Tagging System

### Tag Implementation
- **Data Type**: `short[]` for compact storage
- **Mapping**: Bidirectional dictionaries for efficient lookups
- **Operations**: Intersection, union, and exclusion queries

### Tag Operations
```csharp
// Find vectors with specific tag
var vectorIds = db.Tags.GetVectorIdsByTag(tagId);

// Find vectors matching all tags (intersection)
var matchingIds = db.Tags.GetVectorIdsByTags(tagArray);

// Find vectors matching any tag (union)
var anyMatchIds = db.Tags.GetVectorIdsByAnyTag(tagArray);
```

### Performance Characteristics
- **Time Complexity**: O(1) average for tag-based lookups
- **Memory Efficiency**: Compact representation using short values
- **Scalability**: Efficient handling of large numbers of vectors and tags

## Data Management Best Practices

### Import/Export Guidelines

1. **Choose Appropriate Format**
   - CSV for simple, structured data
   - JSON for rich metadata
   - Parquet for analytics workflows
   - Binary for production performance

2. **Use Cancellation Tokens**
   ```csharp
   var cts = new CancellationTokenSource();
   await db.ImportDataAsync(path, isDirectory, contentType, cts.Token);
   ```

3. **Handle Large Datasets**
   - Process in batches for memory efficiency
   - Monitor progress and provide user feedback
   - Implement retry logic for failed operations

4. **Validate Data Integrity**
   - Check vector dimensions consistency
   - Validate required fields
   - Handle missing or malformed data gracefully

### Performance Optimization

1. **Batch Operations**
   ```csharp
   // Batch multiple vectors for efficiency
   await db.AddRangeAsync(vectorCollection);
   ```

2. **Index Management**
   - Allow background indexing to complete
   - Manually rebuild indexes after bulk operations
   - Monitor index rebuild frequency

3. **Memory Management**
   - Use memory-mapped storage for large datasets
   - Configure appropriate cache sizes
   - Monitor memory usage and adjust accordingly

4. **Concurrency Considerations**
   - Import/export operations use appropriate locks
   - Avoid concurrent writes during bulk operations
   - Use read operations during import when possible

### Error Handling

1. **Robust Exception Handling**
   ```csharp
   try
   {
       await db.ImportDataAsync(path, isDirectory, contentType);
   }
   catch (FileNotFoundException ex)
   {
       // Handle missing files
   }
   catch (InvalidDataException ex)
   {
       // Handle corrupt data
   }
   catch (UnauthorizedAccessException ex)
   {
       // Handle permission issues
   }
   ```

2. **Data Validation**
   - Verify file formats before processing
   - Check for required fields and data types
   - Provide meaningful error messages

3. **Recovery Strategies**
   - Implement checkpointing for long operations
   - Support resumable imports for large datasets
   - Maintain data consistency during failures

### Monitoring and Observability

1. **Performance Metrics**
   - Track import/export throughput
   - Monitor memory usage during operations
   - Measure index rebuild times

2. **Logging and Telemetry**
   - Log all data operations with structured logging
   - Use activity tracing for operation correlation
   - Monitor error rates and patterns

3. **Health Checks**
   - Validate data integrity regularly
   - Check storage space availability
   - Monitor background process health