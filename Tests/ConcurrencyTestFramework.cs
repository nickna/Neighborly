using Neighborly;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Neighborly.Tests.ConcurrencyFramework;

/// <summary>
/// Represents a single operation to be performed in a concurrency test.
/// </summary>
public readonly struct ConcurrencyTestAction
{
    public enum OperationType
    {
        Add,
        Update,
        Remove,
        Search
    }

    /// <summary>
    /// The type of operation to perform.
    /// </summary>
    public OperationType Operation { get; }

    /// <summary>
    /// The target thread that should execute this operation.
    /// </summary>
    public int TargetThread { get; }

    /// <summary>
    /// The execution order within the target thread.
    /// </summary>
    public int ExecutionOrder { get; }

    /// <summary>
    /// The vector data for the operation.
    /// </summary>
    public Vector Vector { get; }

    /// <summary>
    /// For Update operations, the ID of the vector to update.
    /// For Remove operations, the ID of the vector to remove.
    /// </summary>
    public Guid? TargetId { get; }

    /// <summary>
    /// Optional delay before executing this operation (in milliseconds).
    /// </summary>
    public int DelayMs { get; }

    /// <summary>
    /// A unique identifier for this action for tracking purposes.
    /// </summary>
    public Guid ActionId { get; }

    public ConcurrencyTestAction(
        OperationType operation,
        int targetThread,
        int executionOrder,
        Vector vector,
        Guid? targetId = null,
        int delayMs = 0)
    {
        Operation = operation;
        TargetThread = targetThread;
        ExecutionOrder = executionOrder;
        Vector = vector;
        TargetId = targetId;
        DelayMs = delayMs;
        ActionId = Guid.NewGuid();
    }

    public override string ToString()
    {
        return $"T{TargetThread}:{ExecutionOrder} - {Operation} {Vector?.Id} {(TargetId.HasValue ? $"-> {TargetId}" : "")}";
    }
}

/// <summary>
/// Represents a scripted sequence of operations for deterministic concurrency testing.
/// </summary>
public class ConcurrencyTestScript
{
    private readonly List<ConcurrencyTestAction> _actions;
    private readonly Dictionary<int, List<ConcurrencyTestAction>> _actionsByThread;

    /// <summary>
    /// Gets all actions in the script.
    /// </summary>
    public IReadOnlyList<ConcurrencyTestAction> Actions => _actions.AsReadOnly();

    /// <summary>
    /// Gets the number of threads required for this script.
    /// </summary>
    public int ThreadCount { get; }

    /// <summary>
    /// Gets the expected final state after executing this script.
    /// </summary>
    public IReadOnlyDictionary<Guid, Vector> ExpectedFinalState { get; }

    public ConcurrencyTestScript(IEnumerable<ConcurrencyTestAction> actions, Dictionary<Guid, Vector> expectedFinalState)
    {
        _actions = actions.ToList();
        _actionsByThread = _actions.GroupBy(a => a.TargetThread)
                                  .ToDictionary(g => g.Key, g => g.OrderBy(a => a.ExecutionOrder).ToList());
        
        ThreadCount = _actionsByThread.Keys.DefaultIfEmpty(0).Max() + 1;
        ExpectedFinalState = expectedFinalState.AsReadOnly();
    }

    /// <summary>
    /// Gets all actions for a specific thread, ordered by execution order.
    /// </summary>
    public IEnumerable<ConcurrencyTestAction> GetActionsForThread(int threadId)
    {
        return _actionsByThread.TryGetValue(threadId, out var actions) ? actions : Enumerable.Empty<ConcurrencyTestAction>();
    }

    /// <summary>
    /// Validates that the script is well-formed.
    /// </summary>
    public void Validate()
    {
        // Check for duplicate execution orders within threads
        foreach (var threadActions in _actionsByThread.Values)
        {
            var duplicateOrders = threadActions.GroupBy(a => a.ExecutionOrder)
                                              .Where(g => g.Count() > 1)
                                              .Select(g => g.Key);
            
            if (duplicateOrders.Any())
            {
                throw new InvalidOperationException($"Duplicate execution orders found: {string.Join(", ", duplicateOrders)}");
            }
        }

        // Check that Remove/Update operations reference valid targets
        var addedVectorIds = new HashSet<Guid>();
        foreach (var action in _actions.OrderBy(a => a.TargetThread).ThenBy(a => a.ExecutionOrder))
        {
            switch (action.Operation)
            {
                case ConcurrencyTestAction.OperationType.Add:
                    addedVectorIds.Add(action.Vector.Id);
                    break;
                case ConcurrencyTestAction.OperationType.Update:
                case ConcurrencyTestAction.OperationType.Remove:
                    if (action.TargetId.HasValue && !addedVectorIds.Contains(action.TargetId.Value))
                    {
                        // Note: This is a simplified validation. In real concurrent execution,
                        // the order may be different due to thread scheduling.
                        Console.WriteLine($"Warning: Action {action} references ID {action.TargetId} that may not be added yet.");
                    }
                    break;
            }
        }
    }
}

/// <summary>
/// Executes a ConcurrencyTestScript in a controlled, multi-threaded manner.
/// </summary>
public class ConcurrencyTestRunner
{
    /// <summary>
    /// Result of executing a concurrency test script.
    /// </summary>
    public class TestResult
    {
        public TimeSpan ExecutionTime { get; set; }
        public int TotalActionsExecuted { get; set; }
        public List<Exception> Exceptions { get; set; } = new();
        public Dictionary<Guid, (ConcurrencyTestAction Action, DateTime Timestamp, bool Success)> ExecutionLog { get; set; } = new();
        public bool Success => !Exceptions.Any();
    }

    /// <summary>
    /// Executes the given script against the provided vector database.
    /// </summary>
    public async Task<TestResult> ExecuteAsync(VectorDatabase database, ConcurrencyTestScript script)
    {
        script.Validate();

        var result = new TestResult();
        var executionLog = new ConcurrentDictionary<Guid, (ConcurrencyTestAction Action, DateTime Timestamp, bool Success)>();
        var exceptions = new ConcurrentBag<Exception>();

        var stopwatch = Stopwatch.StartNew();

        // Create tasks for each thread
        var tasks = new List<Task>();
        for (int threadId = 0; threadId < script.ThreadCount; threadId++)
        {
            var currentThreadId = threadId; // Capture for closure
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await ExecuteThreadActionsAsync(database, script.GetActionsForThread(currentThreadId), executionLog);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }));
        }

        // Wait for all threads to complete
        await Task.WhenAll(tasks);

        stopwatch.Stop();

        // Populate result
        result.ExecutionTime = stopwatch.Elapsed;
        result.TotalActionsExecuted = executionLog.Count;
        result.Exceptions = exceptions.ToList();
        result.ExecutionLog = executionLog.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        return result;
    }

    private async Task ExecuteThreadActionsAsync(
        VectorDatabase database,
        IEnumerable<ConcurrencyTestAction> actions,
        ConcurrentDictionary<Guid, (ConcurrencyTestAction Action, DateTime Timestamp, bool Success)> executionLog)
    {
        foreach (var action in actions)
        {
            // Apply delay if specified
            if (action.DelayMs > 0)
            {
                await Task.Delay(action.DelayMs);
            }

            bool success = false;
            try
            {
                switch (action.Operation)
                {
                    case ConcurrencyTestAction.OperationType.Add:
                        database.AddVector(action.Vector);
                        success = true;
                        break;

                    case ConcurrencyTestAction.OperationType.Update:
                        if (action.TargetId.HasValue)
                        {
                            // Check if vector exists before update
                            var existingVector = database.GetVector(action.TargetId.Value);
                            if (existingVector != null)
                            {
                                Console.WriteLine($"Found vector {action.TargetId.Value} with values [{string.Join(", ", existingVector.Values)}] before update");
                                success = database.UpdateVector(action.TargetId.Value, action.Vector);
                                if (!success)
                                {
                                    Console.WriteLine($"Update failed for vector {action.TargetId.Value}");
                                }
                                else
                                {
                                    Console.WriteLine($"Update succeeded for vector {action.TargetId.Value}");
                                    // Verify update by reading back
                                    var updatedVector = database.GetVector(action.TargetId.Value);
                                    if (updatedVector != null)
                                    {
                                        Console.WriteLine($"After update, vector {action.TargetId.Value} has values [{string.Join(", ", updatedVector.Values)}]");
                                    }
                                }
                            }
                            else
                            {
                                Console.WriteLine($"Vector {action.TargetId.Value} not found before update attempt");
                                success = false;
                            }
                        }
                        break;

                    case ConcurrencyTestAction.OperationType.Remove:
                        if (action.TargetId.HasValue)
                        {
                            var vectorToRemove = database.GetVector(action.TargetId.Value);
                            if (vectorToRemove != null)
                            {
                                success = database.RemoveVector(vectorToRemove);
                            }
                        }
                        break;

                    case ConcurrencyTestAction.OperationType.Search:
                        var results = database.Search(action.Vector, 5);
                        success = true; // Search operations are always considered successful
                        break;
                }
            }
            catch (Exception)
            {
                success = false;
                // Log but don't rethrow - we want to continue with other operations
            }

            executionLog[action.ActionId] = (action, DateTime.UtcNow, success);
        }
    }

    /// <summary>
    /// Verifies that the database state matches the expected final state from the script.
    /// </summary>
    public void VerifyFinalState(VectorDatabase database, ConcurrencyTestScript script, TestResult result)
    {
        var actualVectors = database.Vectors.ToDictionary(v => v.Id, v => v);
        var expectedVectors = script.ExpectedFinalState;

        // Check count
        if (actualVectors.Count != expectedVectors.Count)
        {
            throw new AssertionException($"Expected {expectedVectors.Count} vectors, but found {actualVectors.Count}");
        }

        // Check each expected vector exists
        foreach (var expectedVector in expectedVectors)
        {
            if (!actualVectors.TryGetValue(expectedVector.Key, out var actualVector))
            {
                throw new AssertionException($"Expected vector {expectedVector.Key} not found in database");
            }

            // Verify vector data matches (basic check - could be expanded)
            if (!actualVector.Values.SequenceEqual(expectedVector.Value.Values))
            {
                var actualValues = string.Join(", ", actualVector.Values);
                var expectedValues = string.Join(", ", expectedVector.Value.Values);
                throw new AssertionException($"Vector {expectedVector.Key} has incorrect values. Expected: [{expectedValues}], Actual: [{actualValues}]");
            }

            if (actualVector.OriginalText != expectedVector.Value.OriginalText)
            {
                throw new AssertionException($"Vector {expectedVector.Key} has incorrect OriginalText. Expected: '{expectedVector.Value.OriginalText}', Actual: '{actualVector.OriginalText}'");
            }
        }

        // Check no unexpected vectors exist
        foreach (var actualVector in actualVectors)
        {
            if (!expectedVectors.ContainsKey(actualVector.Key))
            {
                throw new AssertionException($"Unexpected vector {actualVector.Key} found in database");
            }
        }
    }
}

/// <summary>
/// Custom exception for assertion failures in concurrency tests.
/// </summary>
public class AssertionException : Exception
{
    public AssertionException(string message) : base(message) { }
    public AssertionException(string message, Exception innerException) : base(message, innerException) { }
}