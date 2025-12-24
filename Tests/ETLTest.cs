using Neighborly.ETL;

namespace Neighborly.Tests;

[TestFixture]
public class ETLTest
{
    public static IReadOnlyList<IETL> EtlImplementations =
    [
        new Csv(), new JSON(), new JSONZ(), new Neighborly.ETL.Parquet()  // HDF5 is not implemented yet
    ];

    [TestCaseSource(nameof(EtlImplementations))]
    public async Task Can_SaveAndLoad_Vectors(IETL etl)
    {
        // Arrange,
        var vectors = new List<Vector>
        {
            new([ 1f, 2, 3 ], "Original Text 1"),
            new([ 4f, 5, 6 ], "Original Text 2"),
            new([ 7f, 8, 9 ], "Original Text 3")
        };

        var path = Path.GetTempFileName();
        try
        {
            // Act
            await etl.ExportDataAsync(vectors, path).ConfigureAwait(false);
            var loadedVectors = new List<Vector>();
            await etl.ImportDataAsync(path, loadedVectors, isDirectory: false).ConfigureAwait(false);

            // Assert
            Assert.That(vectors, Has.Count.EqualTo(loadedVectors.Count));
            for (int i = 0; i < vectors.Count; i++)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(vectors[i].Id, Is.EqualTo(loadedVectors[i].Id));
                    Assert.That(vectors[i].Values, Is.EqualTo(loadedVectors[i].Values));
                    Assert.That(vectors[i].Tags, Is.EqualTo(loadedVectors[i].Tags));
                    Assert.That(vectors[i].OriginalText, Is.EqualTo(loadedVectors[i].OriginalText));
                });
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestCaseSource(nameof(EtlImplementations))]
    public async Task Can_Import_FromDirectory_InParallel(IETL etl)
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create test data across multiple files
            var vectorSets = new List<List<Vector>>
            {
                new() { new([ 1f, 2, 3 ], "File1 Vector1"), new([ 4f, 5, 6 ], "File1 Vector2") },
                new() { new([ 7f, 8, 9 ], "File2 Vector1"), new([ 10f, 11, 12 ], "File2 Vector2") },
                new() { new([ 13f, 14, 15 ], "File3 Vector1"), new([ 16f, 17, 18 ], "File3 Vector2") }
            };

            // Export each set to a separate file
            for (int i = 0; i < vectorSets.Count; i++)
            {
                var filePath = Path.Combine(tempDir, $"vectors_{i}{etl.FileExtension}");
                await etl.ExportDataAsync(vectorSets[i], filePath).ConfigureAwait(false);
            }

            // Act - Import entire directory
            var loadedVectors = new List<Vector>();
            await etl.ImportDataAsync(tempDir, loadedVectors, isDirectory: true).ConfigureAwait(false);

            // Assert
            var expectedCount = vectorSets.Sum(s => s.Count);
            Assert.That(loadedVectors, Has.Count.EqualTo(expectedCount));

            // Verify all original vectors are present (order may vary due to parallel processing)
            var allExpectedVectors = vectorSets.SelectMany(s => s).ToList();
            foreach (var expected in allExpectedVectors)
            {
                var loaded = loadedVectors.FirstOrDefault(v => v.Id == expected.Id);
                Assert.That(loaded, Is.Not.Null, $"Vector {expected.Id} not found in loaded vectors");
                Assert.That(loaded!.Values, Is.EqualTo(expected.Values));
                Assert.That(loaded.OriginalText, Is.EqualTo(expected.OriginalText));
            }
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [TestCaseSource(nameof(EtlImplementations))]
    public async Task Export_WithIAsyncEnumerable_Streams_Data(IETL etl)
    {
        // Arrange
        var path = Path.GetTempFileName();
        var vectors = GenerateVectorsAsync(count: 100);

        try
        {
            // Act
            await etl.ExportDataAsync(vectors, path).ConfigureAwait(false);

            // Assert - Verify file was created and can be read back
            var loadedVectors = new List<Vector>();
            await etl.ImportDataAsync(path, loadedVectors, isDirectory: false).ConfigureAwait(false);
            Assert.That(loadedVectors, Has.Count.EqualTo(100));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestCaseSource(nameof(EtlImplementations))]
    public async Task Import_RespectsPreCancelledToken(IETL etl)
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create a single test file
            var vectors = new List<Vector> { new([ 1f, 2, 3 ], "Test Vector") };
            var filePath = Path.Combine(tempDir, $"vectors{etl.FileExtension}");
            await etl.ExportDataAsync(vectors, filePath).ConfigureAwait(false);

            // Act - Use a pre-cancelled token
            using var cts = new CancellationTokenSource();
            cts.Cancel(); // Cancel before starting
            var loadedVectors = new List<Vector>();

            // Assert - Should throw OperationCanceledException or AggregateException
            try
            {
                await etl.ImportDataAsync(tempDir, loadedVectors, isDirectory: true, cts.Token).ConfigureAwait(false);
                Assert.Fail("Expected OperationCanceledException but operation completed successfully");
            }
            catch (OperationCanceledException)
            {
                // Expected - cancellation token was respected
                Assert.Pass();
            }
            catch (AggregateException aggEx) when (aggEx.InnerExceptions.Any(e => e is OperationCanceledException))
            {
                // Also acceptable - parallel operations may wrap in AggregateException
                Assert.Pass();
            }
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [TestCaseSource(nameof(EtlImplementations))]
    public async Task Import_EmptyDirectory_ReturnsEmpty(IETL etl)
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Act - Import from empty directory
            var loadedVectors = new List<Vector>();
            await etl.ImportDataAsync(tempDir, loadedVectors, isDirectory: true).ConfigureAwait(false);

            // Assert - No vectors should be loaded
            Assert.That(loadedVectors, Is.Empty);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    // Helper to generate async enumerable of vectors
    private static async IAsyncEnumerable<Vector> GenerateVectorsAsync(int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return new Vector([ (float)i, i + 1, i + 2 ], $"Generated Vector {i}");
            await Task.Yield(); // Simulate async operation
        }
    }
}
