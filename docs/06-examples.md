# Examples and Samples

## Overview

This document provides comprehensive examples for using Neighborly in various scenarios, from basic operations to advanced integrations.

## Basic Usage Examples

### Simple Vector Database Operations

```csharp
using Neighborly;
using System;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        // Create a new vector database
        var db = new VectorDatabase();
        
        // Create some sample vectors
        var vector1 = new Vector(new float[] { 1.0f, 2.0f, 3.0f });
        var vector2 = new Vector(new float[] { 4.0f, 5.0f, 6.0f });
        var vector3 = new Vector(new float[] { 7.0f, 8.0f, 9.0f });
        
        // Add vectors to the database
        await db.AddAsync(vector1);
        await db.AddAsync(vector2);
        await db.AddAsync(vector3);
        
        Console.WriteLine($"Database contains {db.Count} vectors");
        
        // Search for similar vectors
        var query = new Vector(new float[] { 1.1f, 2.1f, 3.1f });
        var results = await db.SearchAsync(query, k: 2);
        
        Console.WriteLine($"Found {results.Count} similar vectors:");
        foreach (var result in results)
        {
            var distance = query.Distance(result);
            Console.WriteLine($"Vector {result.Id}: distance = {distance:F4}");
        }
        
        // Save the database
        await db.SaveAsync("my_vectors.db");
        Console.WriteLine("Database saved successfully");
    }
}
```

### Working with Tags

```csharp
using Neighborly;
using System;
using System.Linq;
using System.Threading.Tasks;

class TaggedVectorExample
{
    static async Task Main(string[] args)
    {
        var db = new VectorDatabase();
        
        // Create vectors with tags
        var documents = new[]
        {
            new Vector(new float[] { 0.1f, 0.2f, 0.3f }, tags: new short[] { 1, 2 }, originalText: "Document about cats"),
            new Vector(new float[] { 0.4f, 0.5f, 0.6f }, tags: new short[] { 1, 3 }, originalText: "Document about dogs"),
            new Vector(new float[] { 0.7f, 0.8f, 0.9f }, tags: new short[] { 2, 3 }, originalText: "Document about animals"),
            new Vector(new float[] { 0.2f, 0.3f, 0.4f }, tags: new short[] { 4 }, originalText: "Document about technology")
        };
        
        // Add all vectors
        await db.AddRangeAsync(documents);
        
        // Find vectors with specific tag (tag 1 = "pets")
        var petVectorIds = db.Tags.GetVectorIdsByTag(1);
        Console.WriteLine($"Found {petVectorIds.Count} pet-related documents:");
        
        foreach (var id in petVectorIds)
        {
            var vector = db.Vectors[id];
            Console.WriteLine($"  - {vector.OriginalText}");
        }
        
        // Find vectors with multiple tags (intersection)
        var animalAndPetVectors = db.Tags.GetVectorIdsByTags(new short[] { 1, 2 });
        Console.WriteLine($"\nDocuments tagged with both pets AND animals: {animalAndPetVectors.Count}");
        
        // Find vectors with any of the specified tags (union)
        var anyAnimalVectors = db.Tags.GetVectorIdsByAnyTag(new short[] { 1, 2, 3 });
        Console.WriteLine($"Documents with any animal-related tag: {anyAnimalVectors.Count}");
    }
}
```

### Data Import/Export

```csharp
using Neighborly;
using Neighborly.ETL;
using System;
using System.IO;
using System.Threading.Tasks;

class DataImportExportExample
{
    static async Task Main(string[] args)
    {
        var db = new VectorDatabase();
        
        // Import data from JSON file
        if (File.Exists("vectors.json"))
        {
            await db.ImportDataAsync("vectors.json", isDirectory: false, ContentType.JSON);
            Console.WriteLine($"Imported {db.Count} vectors from JSON");
        }
        
        // Add some new vectors
        var newVectors = new[]
        {
            new Vector(new float[] { 1.0f, 2.0f }, originalText: "Sample 1"),
            new Vector(new float[] { 3.0f, 4.0f }, originalText: "Sample 2")
        };
        
        await db.AddRangeAsync(newVectors);
        
        // Export to different formats
        await db.ExportDataAsync("output.csv", ContentType.CSV);
        await db.ExportDataAsync("output.json", ContentType.JSON);
        await db.ExportDataAsync("output.parquet", ContentType.Parquet);
        
        Console.WriteLine("Data exported to multiple formats");
        
        // Import from a directory of files
        if (Directory.Exists("data_directory"))
        {
            await db.ImportDataAsync("data_directory", isDirectory: true, ContentType.JSON);
            Console.WriteLine($"Imported from directory, total vectors: {db.Count}");
        }
    }
}
```

## Advanced Examples

### Custom Distance Calculator

```csharp
using Neighborly;
using Neighborly.Distance;
using System;

public class WeightedEuclideanCalculator : IDistanceCalculator
{
    private readonly float[] _weights;
    
    public string Name => "WeightedEuclidean";
    
    public WeightedEuclideanCalculator(float[] weights)
    {
        _weights = weights ?? throw new ArgumentNullException(nameof(weights));
    }
    
    public float CalculateDistance(ReadOnlySpan<float> vector1, ReadOnlySpan<float> vector2)
    {
        if (vector1.Length != vector2.Length || vector1.Length != _weights.Length)
            throw new ArgumentException("Vector dimensions must match weights");
            
        float sum = 0;
        for (int i = 0; i < vector1.Length; i++)
        {
            float diff = vector1[i] - vector2[i];
            sum += _weights[i] * diff * diff;
        }
        
        return (float)Math.Sqrt(sum);
    }
}

class CustomDistanceExample
{
    static async Task Main(string[] args)
    {
        var db = new VectorDatabase();
        
        // Add some vectors
        var vector1 = new Vector(new float[] { 1.0f, 2.0f, 3.0f });
        var vector2 = new Vector(new float[] { 2.0f, 3.0f, 4.0f });
        
        await db.AddAsync(vector1);
        await db.AddAsync(vector2);
        
        // Use custom weighted distance
        var weights = new float[] { 2.0f, 1.0f, 0.5f }; // First dimension is more important
        var calculator = new WeightedEuclideanCalculator(weights);
        
        var distance = vector1.Distance(vector2, calculator);
        Console.WriteLine($"Weighted Euclidean distance: {distance:F4}");
        
        // Compare with regular Euclidean
        var regularDistance = vector1.Distance(vector2);
        Console.WriteLine($"Regular Euclidean distance: {regularDistance:F4}");
    }
}
```

### Range Search Example

```csharp
using Neighborly;
using Neighborly.Search;
using System;
using System.Threading.Tasks;

class RangeSearchExample
{
    static async Task Main(string[] args)
    {
        var db = new VectorDatabase();
        
        // Create a cluster of vectors around (5, 5)
        var random = new Random(42);
        for (int i = 0; i < 100; i++)
        {
            var x = 5.0f + (float)(random.NextDouble() - 0.5) * 2; // 4-6 range
            var y = 5.0f + (float)(random.NextDouble() - 0.5) * 2; // 4-6 range
            var vector = new Vector(new float[] { x, y }, originalText: $"Point {i}");
            await db.AddAsync(vector);
        }
        
        // Add some outliers
        await db.AddAsync(new Vector(new float[] { 10.0f, 10.0f }, originalText: "Outlier 1"));
        await db.AddAsync(new Vector(new float[] { 0.0f, 0.0f }, originalText: "Outlier 2"));
        
        Console.WriteLine($"Created database with {db.Count} vectors");
        
        // Search for vectors within radius of center point
        var center = new Vector(new float[] { 5.0f, 5.0f });
        var radius = 1.5f;
        
        var nearbyVectors = await db.RangeSearchAsync(center, radius);
        
        Console.WriteLine($"Found {nearbyVectors.Count} vectors within radius {radius} of center (5,5):");
        foreach (var vector in nearbyVectors)
        {
            var distance = center.Distance(vector);
            Console.WriteLine($"  {vector.OriginalText}: ({vector.Values[0]:F2}, {vector.Values[1]:F2}) - distance: {distance:F4}");
        }
    }
}
```

### Algorithm Comparison

```csharp
using Neighborly;
using Neighborly.Search;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

class AlgorithmComparisonExample
{
    static async Task Main(string[] args)
    {
        var db = new VectorDatabase();
        
        // Generate random high-dimensional vectors
        var random = new Random(42);
        var dimensions = 128;
        var vectorCount = 10000;
        
        Console.WriteLine($"Generating {vectorCount} random {dimensions}D vectors...");
        
        for (int i = 0; i < vectorCount; i++)
        {
            var values = new float[dimensions];
            for (int j = 0; j < dimensions; j++)
            {
                values[j] = (float)random.NextGaussian();
            }
            await db.AddAsync(new Vector(values));
        }
        
        // Wait for indexes to build
        await Task.Delay(6000); // Allow background indexing to complete
        
        // Create a query vector
        var queryValues = new float[dimensions];
        for (int i = 0; i < dimensions; i++)
        {
            queryValues[i] = (float)random.NextGaussian();
        }
        var query = new Vector(queryValues);
        
        // Test different algorithms
        var algorithms = new[] 
        { 
            SearchAlgorithm.Linear, 
            SearchAlgorithm.BallTree, 
            SearchAlgorithm.HNSW 
        };
        
        foreach (var algorithm in algorithms)
        {
            var sw = Stopwatch.StartNew();
            var results = await db.SearchAsync(query, k: 10, algorithm: algorithm);
            sw.Stop();
            
            Console.WriteLine($"{algorithm}: {sw.ElapsedMilliseconds}ms, found {results.Count} results");
            
            if (results.Count > 0)
            {
                var avgDistance = 0f;
                foreach (var result in results)
                {
                    avgDistance += query.Distance(result);
                }
                avgDistance /= results.Count;
                Console.WriteLine($"  Average distance: {avgDistance:F4}");
            }
        }
    }
}

// Extension method for Gaussian random numbers
public static class RandomExtensions
{
    public static double NextGaussian(this Random random, double mean = 0, double stdDev = 1)
    {
        var u1 = 1.0 - random.NextDouble();
        var u2 = 1.0 - random.NextDouble();
        var randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        return mean + stdDev * randStdNormal;
    }
}
```

## Integration Examples

### Semantic Kernel Integration

Located in `samples/SemanticKernel/`, this example demonstrates:

- Integration with Microsoft Semantic Kernel
- Using Ollama for embeddings and chat completion
- Text document processing and storage
- Question-answering over stored documents

Key features:
- Automatic Docker container setup for Ollama
- Custom embedding generation
- Memory plugin integration
- End-to-end text processing pipeline

### OpenTelemetry Integration

Located in `samples/OTEL/`, this example shows:

- Integration with .NET Aspire for observability
- Custom metrics and tracing
- Performance monitoring
- Distributed tracing across API calls

Key features:
- Automatic telemetry collection
- Custom metrics dashboard
- Request tracing and correlation
- Performance monitoring

### Python Bindings

Located in `Neighborly.Python/`, provides:

- Python wrapper for the .NET library
- Cross-platform Python package
- Native performance with Python convenience

Example usage:
```python
from neighborly import VectorDatabase, Vector

# Create database
db = VectorDatabase()

# Add vectors
vector1 = Vector([1.0, 2.0, 3.0])
db.add_vector(vector1)

# Search
query = Vector([1.1, 2.1, 3.1])
results = db.search(query, k=5)

# Save/load
db.save("database.db")
db.load("database.db")
```

## Performance Examples

### Benchmarking

```csharp
using Neighborly;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

class BenchmarkExample
{
    static async Task Main(string[] args)
    {
        await BenchmarkInsertionSpeed();
        await BenchmarkSearchSpeed();
        await BenchmarkMemoryUsage();
    }
    
    static async Task BenchmarkInsertionSpeed()
    {
        var db = new VectorDatabase();
        var vectorCount = 10000;
        var dimensions = 100;
        var random = new Random(42);
        
        Console.WriteLine($"Benchmarking insertion of {vectorCount} vectors...");
        
        var sw = Stopwatch.StartNew();
        
        for (int i = 0; i < vectorCount; i++)
        {
            var values = new float[dimensions];
            for (int j = 0; j < dimensions; j++)
            {
                values[j] = (float)random.NextDouble();
            }
            
            await db.AddAsync(new Vector(values));
            
            if (i % 1000 == 0)
            {
                Console.WriteLine($"Inserted {i} vectors ({sw.ElapsedMilliseconds}ms)");
            }
        }
        
        sw.Stop();
        Console.WriteLine($"Total insertion time: {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"Average time per vector: {(double)sw.ElapsedMilliseconds / vectorCount:F3}ms");
        Console.WriteLine($"Insertion rate: {vectorCount * 1000.0 / sw.ElapsedMilliseconds:F0} vectors/second");
    }
    
    static async Task BenchmarkSearchSpeed()
    {
        var db = new VectorDatabase();
        
        // Pre-populate database
        await PopulateDatabase(db, 5000, 50);
        
        // Wait for indexes to build
        await Task.Delay(6000);
        
        Console.WriteLine("\nBenchmarking search speed...");
        
        var random = new Random(42);
        var queryCount = 100;
        var k = 10;
        
        var sw = Stopwatch.StartNew();
        
        for (int i = 0; i < queryCount; i++)
        {
            var queryValues = new float[50];
            for (int j = 0; j < 50; j++)
            {
                queryValues[j] = (float)random.NextDouble();
            }
            
            var query = new Vector(queryValues);
            var results = await db.SearchAsync(query, k);
        }
        
        sw.Stop();
        
        Console.WriteLine($"Total search time for {queryCount} queries: {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"Average time per query: {(double)sw.ElapsedMilliseconds / queryCount:F3}ms");
        Console.WriteLine($"Search rate: {queryCount * 1000.0 / sw.ElapsedMilliseconds:F0} queries/second");
    }
    
    static async Task BenchmarkMemoryUsage()
    {
        var db = new VectorDatabase();
        
        Console.WriteLine("\nBenchmarking memory usage...");
        
        var initialMemory = GC.GetTotalMemory(true);
        Console.WriteLine($"Initial memory: {initialMemory / 1024 / 1024:F1} MB");
        
        await PopulateDatabase(db, 10000, 100);
        
        var afterInsertMemory = GC.GetTotalMemory(true);
        Console.WriteLine($"After inserting 10k vectors: {afterInsertMemory / 1024 / 1024:F1} MB");
        Console.WriteLine($"Memory per vector: {(afterInsertMemory - initialMemory) / 10000.0:F0} bytes");
        
        // Save to disk and reload
        await db.SaveAsync("benchmark.db");
        var savedFileSize = new System.IO.FileInfo("benchmark.db").Length;
        Console.WriteLine($"Saved file size: {savedFileSize / 1024 / 1024:F1} MB");
        
        db = new VectorDatabase();
        await db.LoadAsync("benchmark.db");
        
        var afterLoadMemory = GC.GetTotalMemory(true);
        Console.WriteLine($"After loading from disk: {afterLoadMemory / 1024 / 1024:F1} MB");
    }
    
    static async Task PopulateDatabase(VectorDatabase db, int count, int dimensions)
    {
        var random = new Random(42);
        
        for (int i = 0; i < count; i++)
        {
            var values = new float[dimensions];
            for (int j = 0; j < dimensions; j++)
            {
                values[j] = (float)random.NextDouble();
            }
            
            await db.AddAsync(new Vector(values));
        }
    }
}
```

## Best Practices Examples

### Error Handling and Resilience

```csharp
using Neighborly;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

class ResilientDatabaseExample
{
    static async Task Main(string[] args)
    {
        await SafeDatabaseOperations();
        await CancellationExample();
        await RetryExample();
    }
    
    static async Task SafeDatabaseOperations()
    {
        var db = new VectorDatabase();
        
        try
        {
            // Attempt to load existing database
            await db.LoadAsync("vectors.db", createOnNew: true);
            Console.WriteLine($"Loaded database with {db.Count} vectors");
        }
        catch (FileNotFoundException)
        {
            Console.WriteLine("Database file not found, starting with empty database");
        }
        catch (InvalidDataException ex)
        {
            Console.WriteLine($"Database file is corrupted: {ex.Message}");
            Console.WriteLine("Starting with fresh database");
            db = new VectorDatabase();
        }
        catch (UnauthorizedAccessException)
        {
            Console.WriteLine("Permission denied accessing database file");
            return;
        }
        
        try
        {
            // Add some vectors
            var vector = new Vector(new float[] { 1.0f, 2.0f, 3.0f });
            await db.AddAsync(vector);
            
            // Save database
            await db.SaveAsync("vectors.db");
            Console.WriteLine("Database saved successfully");
        }
        catch (IOException ex)
        {
            Console.WriteLine($"Error saving database: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error: {ex.Message}");
        }
    }
    
    static async Task CancellationExample()
    {
        var db = new VectorDatabase();
        
        using var cts = new CancellationTokenSource();
        
        // Cancel after 5 seconds
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        
        try
        {
            // Long-running import operation
            await db.ImportDataAsync("large_dataset/", isDirectory: true, 
                ContentType.JSON, cts.Token);
            Console.WriteLine("Import completed successfully");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Import operation was cancelled");
        }
    }
    
    static async Task RetryExample()
    {
        var db = new VectorDatabase();
        const int maxRetries = 3;
        
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await db.LoadAsync("unreliable_network_path/vectors.db");
                Console.WriteLine("Database loaded successfully");
                break;
            }
            catch (IOException) when (attempt < maxRetries)
            {
                Console.WriteLine($"Attempt {attempt} failed, retrying...");
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt))); // Exponential backoff
            }
            catch (IOException) when (attempt == maxRetries)
            {
                Console.WriteLine("All retry attempts failed");
                throw;
            }
        }
    }
}
```

These examples demonstrate various aspects of using Neighborly effectively, from basic operations to advanced scenarios and best practices. Each example includes comprehensive error handling and performance considerations.