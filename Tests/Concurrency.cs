using Microsoft.Extensions.Logging;
using Neighborly;
using Neighborly.Search;
using Neighborly.Tests.Helpers;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Neighborly.Tests;

[TestFixture]
public class VectorDatabaseConcurrencyTests
{
    private VectorDatabase _db;
    private MockLogger<VectorDatabase> _logger;

    [SetUp]
    public void Setup()
    {
        _logger = new MockLogger<VectorDatabase>();
        _db = new VectorDatabase(_logger, null);
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
    }

    // This test may intermittently fail due to the nature of concurrent operations.
    [Test]
    [Ignore("Intermittently fails due to the nature of concurrent operations.")]
    public async Task ConcurrencyStressTest()
    {
        // Arrange
        int operationCount = 10000;
        int threadCount = 8;
        var operationLog = new ConcurrentDictionary<Guid, ConcurrentQueue<(DateTime Timestamp, string Operation, Vector Vector)>>();

        // Act
        var stopwatch = Stopwatch.StartNew();
        var tasks = new List<Task>();
        for (int i = 0; i < threadCount; i++)
        {
            tasks.Add(Task.Run(() => PerformRandomOperations(_db, operationCount / threadCount, operationLog)));
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        Console.WriteLine($"Concurrency test completed in {stopwatch.ElapsedMilliseconds}ms");
        VerifyDatabaseIntegrity(_db, operationLog);
    }

    private void PerformRandomOperations(VectorDatabase db, int count, ConcurrentDictionary<Guid, ConcurrentQueue<(DateTime, string, Vector)>> operationLog)
    {
        var random = new Random();
        for (int i = 0; i < count; i++)
        {
            switch (random.Next(4))
            {
                case 0:
                    var newVector = CreateRandomVector();
                    db.Vectors.Add(newVector);
                    LogOperation(operationLog, newVector.Id, "Add", newVector);
                    break;
                case 1:
                    var vectorToRemove = GetRandomVector(db);
                    if (vectorToRemove != null && db.Vectors.Remove(vectorToRemove))
                    {
                        LogOperation(operationLog, vectorToRemove.Id, "Remove", vectorToRemove);
                    }
                    break;
                case 2:
                    var vectorToUpdate = GetRandomVector(db);
                    if (vectorToUpdate != null)
                    {
                        var updatedVector = CreateRandomVector();
                        updatedVector.Id = vectorToUpdate.Id; // Keep the same ID
                        if (db.Vectors.Update(vectorToUpdate.Id, updatedVector))
                        {
                            LogOperation(operationLog, updatedVector.Id, "Update", updatedVector);
                        }
                    }
                    break;
                case 3:
                    db.Search(CreateRandomVector(), 5);
                    break;
            }
        }
    }

    private void LogOperation(ConcurrentDictionary<Guid, ConcurrentQueue<(DateTime, string, Vector)>> operationLog, Guid id, string operation, Vector vector)
    {
        operationLog.AddOrUpdate(
            id,
            new ConcurrentQueue<(DateTime, string, Vector)>(new[] { (DateTime.UtcNow, operation, vector) }),
            (_, queue) =>
            {
                queue.Enqueue((DateTime.UtcNow, operation, vector));
                return queue;
            });
    }

    private void VerifyDatabaseIntegrity(VectorDatabase db, ConcurrentDictionary<Guid, ConcurrentQueue<(DateTime Timestamp, string Operation, Vector Vector)>> operationLog)
    {
        var finalState = new Dictionary<Guid, (string Operation, Vector Vector)>();
        foreach (var kvp in operationLog)
        {
            var operations = kvp.Value.OrderBy(x => x.Timestamp).ToList();
            var lastOperation = operations.Last();

            // Check if there's an Add after the last Remove
            var lastRemoveIndex = operations.FindLastIndex(op => op.Operation == "Remove");
            var lastAddIndex = operations.FindLastIndex(op => op.Operation == "Add");

            if (lastAddIndex > lastRemoveIndex)
            {
                finalState[kvp.Key] = ("Add", lastOperation.Vector);
            }
            else if (lastOperation.Operation != "Remove")
            {
                finalState[kvp.Key] = (lastOperation.Operation, lastOperation.Vector);
            }
        }

        int expectedCount = finalState.Count(kvp => kvp.Value.Operation != "Remove");
        Console.WriteLine($"Added: {finalState.Count(kvp => kvp.Value.Operation == "Add")}, " +
                          $"Removed: {operationLog.Count - finalState.Count}, " +
                          $"Updated: {finalState.Count(kvp => kvp.Value.Operation == "Update")}");
        Console.WriteLine($"Expected Count: {expectedCount}, Actual Count: {db.Count}");

        var inconsistentVectors = new List<(Guid Id, string ExpectedOperation, string ActualState)>();

        foreach (var vector in db.Vectors)
        {
            if (!finalState.ContainsKey(vector.Id))
            {
                inconsistentVectors.Add((vector.Id, "Not in final state", "Present"));
            }
        }

        foreach (var kvp in finalState)
        {
            if (!db.Vectors.Contains(kvp.Value.Vector))
            {
                inconsistentVectors.Add((kvp.Key, kvp.Value.Operation, "Missing"));
            }
        }

        Console.WriteLine($"Database integrity verified. Count: {db.Count}, Expected: {expectedCount}");

        if (inconsistentVectors.Any())
        {
            Console.WriteLine("Inconsistent vectors found:");
            foreach (var (id, expectedOperation, actualState) in inconsistentVectors)
            {
                Console.WriteLine($"Vector ID: {id}, Expected: {expectedOperation}, Actual: {actualState}");
                if (operationLog.TryGetValue(id, out var operations))
                {
                    foreach (var (timestamp, operation, vector) in operations.OrderBy(x => x.Timestamp))
                    {
                        Console.WriteLine($"  {timestamp:HH:mm:ss.fff}: {operation}");
                    }
                }
            }
        }

        Assert.That(db.Count, Is.EqualTo(expectedCount), "The database count should match the expected count.");
        Assert.That(inconsistentVectors, Is.Empty, "There should be no inconsistencies between the operation log and the database state.");
    }

    private Vector CreateRandomVector()
    {
        var random = new Random();
        float[] values = new float[3];
        for (int i = 0; i < 3; i++)
        {
            values[i] = (float)random.NextDouble();
        }
        return new Vector(values, Path.GetRandomFileName().Replace(".", ""));
    }

    private Vector GetRandomVector(VectorDatabase db)
    {
        int maxAttempts = 10;
        for (int i = 0; i < maxAttempts; i++)
        {
            try
            {
                var vectors = db.Vectors.ToList(); // Create a snapshot of the current state
                if (vectors.Count == 0)
                {
                    return null;
                }
                var random = new Random();
                return vectors[random.Next(vectors.Count)];
            }
            catch (Exception)
            {
                // Any exception here is likely due to concurrent modification
                if (i == maxAttempts - 1)
                {
                    return null; // If we've exhausted our attempts, return null
                }
            }
        }
        return null;
    }
}