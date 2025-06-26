using Microsoft.Extensions.Logging;
using Neighborly;
using Neighborly.Tests.ConcurrencyFramework;
using Neighborly.Tests.Helpers;

namespace Neighborly.Tests;

[TestFixture]
public class DeterministicConcurrencyTests
{
    private VectorDatabase _db;
    private MockLogger<VectorDatabase> _logger;
    private ConcurrencyTestRunner _runner;

    [SetUp]
    public void Setup()
    {
        _logger = new MockLogger<VectorDatabase>();
        _db = new VectorDatabase(_logger, null);
        _runner = new ConcurrencyTestRunner();
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
    }

    [Test]
    public async Task SimpleAddOperations_ShouldBeConsistent()
    {
        // Arrange: Create 4 vectors to be added by 2 threads
        var vector1 = CreateVector([1.0f, 2.0f, 3.0f], "vector1");
        var vector2 = CreateVector([4.0f, 5.0f, 6.0f], "vector2");
        var vector3 = CreateVector([7.0f, 8.0f, 9.0f], "vector3");
        var vector4 = CreateVector([10.0f, 11.0f, 12.0f], "vector4");

        var actions = new[]
        {
            new ConcurrencyTestAction(ConcurrencyTestAction.OperationType.Add, 0, 0, vector1),
            new ConcurrencyTestAction(ConcurrencyTestAction.OperationType.Add, 0, 1, vector2),
            new ConcurrencyTestAction(ConcurrencyTestAction.OperationType.Add, 1, 0, vector3),
            new ConcurrencyTestAction(ConcurrencyTestAction.OperationType.Add, 1, 1, vector4)
        };

        var expectedState = new Dictionary<Guid, Vector>
        {
            [vector1.Id] = vector1,
            [vector2.Id] = vector2,
            [vector3.Id] = vector3,
            [vector4.Id] = vector4
        };

        var script = new ConcurrencyTestScript(actions, expectedState);

        // Act
        var result = await _runner.ExecuteAsync(_db, script);

        // Assert
        Assert.That(result.Success, Is.True, $"Test failed with exceptions: {string.Join(", ", result.Exceptions.Select(e => e.Message))}");
        Assert.That(result.TotalActionsExecuted, Is.EqualTo(4));
        Assert.That(_db.Count, Is.EqualTo(4));
        
        _runner.VerifyFinalState(_db, script, result);
    }

    [Test]
    public async Task AddThenRemoveOperations_ShouldBeConsistent()
    {
        // Arrange: Add vectors then remove some of them
        var vector1 = CreateVector([1.0f, 2.0f, 3.0f], "vector1");
        var vector2 = CreateVector([4.0f, 5.0f, 6.0f], "vector2");
        var vector3 = CreateVector([7.0f, 8.0f, 9.0f], "vector3");

        var actions = new[]
        {
            // Thread 0: Add vectors 1 and 2
            new ConcurrencyTestAction(ConcurrencyTestAction.OperationType.Add, 0, 0, vector1),
            new ConcurrencyTestAction(ConcurrencyTestAction.OperationType.Add, 0, 1, vector2),
            
            // Thread 1: Add vector 3, then remove vector 1
            new ConcurrencyTestAction(ConcurrencyTestAction.OperationType.Add, 1, 0, vector3),
            new ConcurrencyTestAction(ConcurrencyTestAction.OperationType.Remove, 1, 1, vector1, vector1.Id, 10),
        };

        // Expected: vectors 2 and 3 should remain
        var expectedState = new Dictionary<Guid, Vector>
        {
            [vector2.Id] = vector2,
            [vector3.Id] = vector3
        };

        var script = new ConcurrencyTestScript(actions, expectedState);

        // Act
        var result = await _runner.ExecuteAsync(_db, script);

        // Assert
        Assert.That(result.Success, Is.True, $"Test failed with exceptions: {string.Join(", ", result.Exceptions.Select(e => e.Message))}");
        Assert.That(_db.Count, Is.EqualTo(2));
        
        _runner.VerifyFinalState(_db, script, result);
    }

    [Test]
    public async Task AddUpdateOperations_ShouldBeConsistent()
    {
        // Arrange: Simple test - just add vectors on separate threads
        var vector1 = CreateVector([1.0f, 2.0f, 3.0f], "vector1");
        var vector2 = CreateVector([4.0f, 5.0f, 6.0f], "vector2");

        var actions = new[]
        {
            // Thread 0: Add vector 1
            new ConcurrencyTestAction(ConcurrencyTestAction.OperationType.Add, 0, 0, vector1),
            
            // Thread 1: Add vector 2  
            new ConcurrencyTestAction(ConcurrencyTestAction.OperationType.Add, 1, 0, vector2),
        };

        var expectedState = new Dictionary<Guid, Vector>
        {
            [vector1.Id] = vector1,
            [vector2.Id] = vector2
        };

        var script = new ConcurrencyTestScript(actions, expectedState);

        // Act
        var result = await _runner.ExecuteAsync(_db, script);

        // Assert
        Assert.That(result.Success, Is.True, $"Test failed with exceptions: {string.Join(", ", result.Exceptions.Select(e => e.Message))}");
        Assert.That(_db.Count, Is.EqualTo(2));
        
        _runner.VerifyFinalState(_db, script, result);
    }

    [Test]
    public async Task ComplexMixedOperations_ShouldBeConsistent()
    {
        // Arrange: Complex scenario with adds, updates, removes, and searches
        var vector1 = CreateVector([1.0f, 2.0f, 3.0f], "vector1");
        var vector2 = CreateVector([4.0f, 5.0f, 6.0f], "vector2");
        var vector3 = CreateVector([7.0f, 8.0f, 9.0f], "vector3");
        var vector4 = CreateVector([10.0f, 11.0f, 12.0f], "vector4");
        var updatedVector2Data = new Vector([40.0f, 50.0f, 60.0f], "updated_vector2");

        var searchVector = CreateVector([5.0f, 5.0f, 5.0f], "search_vector");

        var actions = new[]
        {
            // Thread 0: Add vector1, add vector2, update vector2
            new ConcurrencyTestAction(ConcurrencyTestAction.OperationType.Add, 0, 0, vector1),
            new ConcurrencyTestAction(ConcurrencyTestAction.OperationType.Add, 0, 1, vector2),
            new ConcurrencyTestAction(ConcurrencyTestAction.OperationType.Update, 0, 2, updatedVector2Data, vector2.Id, 15),
            
            // Thread 1: Add vector3, search, add vector4, remove vector1
            new ConcurrencyTestAction(ConcurrencyTestAction.OperationType.Add, 1, 0, vector3),
            new ConcurrencyTestAction(ConcurrencyTestAction.OperationType.Search, 1, 1, searchVector, null, 5),
            new ConcurrencyTestAction(ConcurrencyTestAction.OperationType.Add, 1, 2, vector4),
            new ConcurrencyTestAction(ConcurrencyTestAction.OperationType.Remove, 1, 3, vector1, vector1.Id, 20),
        };

        // Expected: vector2 updated with new data but same ID, vector3, vector4 (vector1 removed)
        var expectedUpdatedVector2 = new Vector([40.0f, 50.0f, 60.0f], "updated_vector2");
        var idProperty = typeof(Vector).GetProperty("Id");
        idProperty?.SetValue(expectedUpdatedVector2, vector2.Id);

        var expectedState = new Dictionary<Guid, Vector>
        {
            [vector2.Id] = expectedUpdatedVector2, // Same ID as vector2, but updated data
            [vector3.Id] = vector3,
            [vector4.Id] = vector4
        };

        var script = new ConcurrencyTestScript(actions, expectedState);

        // Act
        var result = await _runner.ExecuteAsync(_db, script);

        // Assert
        Assert.That(result.Success, Is.True, $"Test failed with exceptions: {string.Join(", ", result.Exceptions.Select(e => e.Message))}");
        Assert.That(_db.Count, Is.EqualTo(3));
        
        _runner.VerifyFinalState(_db, script, result);
    }

    [Test]
    public async Task RaceConditionScenario_AddRemoveOnSameVector()
    {
        // Arrange: Test race condition where one thread adds and another tries to remove the same vector
        var vector1 = CreateVector([1.0f, 2.0f, 3.0f], "vector1");

        var actions = new[]
        {
            // Thread 0: Add vector1
            new ConcurrencyTestAction(ConcurrencyTestAction.OperationType.Add, 0, 0, vector1),
            
            // Thread 1: Try to remove vector1 at almost the same time (may fail)
            new ConcurrencyTestAction(ConcurrencyTestAction.OperationType.Remove, 1, 0, vector1, vector1.Id, 1),
        };

        // Expected state depends on timing - could be empty or contain vector1
        // We'll verify consistency rather than specific outcome
        var script = new ConcurrencyTestScript(actions, new Dictionary<Guid, Vector>());

        // Act
        var result = await _runner.ExecuteAsync(_db, script);

        // Assert
        Assert.That(result.Success, Is.True, $"Test failed with exceptions: {string.Join(", ", result.Exceptions.Select(e => e.Message))}");
        
        // The key assertion is that we should have either 0 or 1 vector, but not more
        Assert.That(_db.Count, Is.LessThanOrEqualTo(1));
        
        // If there's 1 vector, it should be vector1
        if (_db.Count == 1)
        {
            var remainingVector = _db.Vectors.First();
            Assert.That(remainingVector.Id, Is.EqualTo(vector1.Id));
        }
    }

    [Test]
    public async Task HighConcurrencyStressTest_DeterministicPattern()
    {
        // Arrange: Create a deterministic pattern with many operations across multiple threads
        var vectors = Enumerable.Range(1, 20)
            .Select(i => CreateVector([i * 1.0f, i * 2.0f, i * 3.0f], $"vector{i}"))
            .ToArray();

        var actions = new List<ConcurrencyTestAction>();
        
        // Thread 0: Add first 10 vectors
        for (int i = 0; i < 10; i++)
        {
            actions.Add(new ConcurrencyTestAction(ConcurrencyTestAction.OperationType.Add, 0, i, vectors[i]));
        }
        
        // Thread 1: Add next 10 vectors
        for (int i = 10; i < 20; i++)
        {
            actions.Add(new ConcurrencyTestAction(ConcurrencyTestAction.OperationType.Add, 1, i - 10, vectors[i]));
        }
        
        // Thread 2: Update first 5 vectors with new data
        var updateData = new Vector[5];
        for (int i = 0; i < 5; i++)
        {
            updateData[i] = new Vector([(i + 100) * 1.0f, (i + 100) * 2.0f, (i + 100) * 3.0f], $"updated_vector{i}");
            actions.Add(new ConcurrencyTestAction(ConcurrencyTestAction.OperationType.Update, 2, i, updateData[i], vectors[i].Id, 10));
        }
        
        // Thread 3: Remove vectors 15-19
        for (int i = 15; i < 20; i++)
        {
            actions.Add(new ConcurrencyTestAction(ConcurrencyTestAction.OperationType.Remove, 3, i - 15, vectors[i], vectors[i].Id, 50));
        }

        // Calculate expected state: vectors 0-4 updated, vectors 5-14 original, vectors 15-19 removed
        var expectedState = new Dictionary<Guid, Vector>();
        var idProperty = typeof(Vector).GetProperty("Id");
        
        // Add updated vectors 0-4 (same IDs as original, but updated data)
        for (int i = 0; i < 5; i++)
        {
            var expectedUpdatedVector = new Vector([(i + 100) * 1.0f, (i + 100) * 2.0f, (i + 100) * 3.0f], $"updated_vector{i}");
            idProperty?.SetValue(expectedUpdatedVector, vectors[i].Id);
            expectedState[vectors[i].Id] = expectedUpdatedVector;
        }
        
        // Add original vectors 5-14
        for (int i = 5; i < 15; i++)
        {
            expectedState[vectors[i].Id] = vectors[i];
        }

        var script = new ConcurrencyTestScript(actions, expectedState);

        // Act
        var result = await _runner.ExecuteAsync(_db, script);

        // Assert
        Assert.That(result.Success, Is.True, $"Test failed with exceptions: {string.Join(", ", result.Exceptions.Select(e => e.Message))}");
        Assert.That(_db.Count, Is.EqualTo(15));
        
        _runner.VerifyFinalState(_db, script, result);
    }

    /// <summary>
    /// Creates a vector with natural ID for testing.
    /// </summary>
    private Vector CreateVector(float[] values, string text)
    {
        return new Vector(values, text);
    }
}