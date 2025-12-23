using Neighborly;
using Neighborly.Tests.Helpers;

namespace Neighborly.Tests;

[TestFixture]
public class TransactionTests
{
    private VectorDatabase _db = null!;
    private MockLogger<VectorDatabase> _logger = new();

    [SetUp]
    public void Setup()
    {
        _db?.Dispose();
        _db = new VectorDatabase(_logger, null);
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
    }

    #region Basic Operations

    [Test]
    public void BeginTransaction_ReturnsNewTransaction()
    {
        using var transaction = _db.BeginTransaction();

        Assert.That(transaction, Is.Not.Null);
        Assert.That(transaction.TransactionId, Is.Not.EqualTo(Guid.Empty));
        Assert.That(transaction.IsCommitted, Is.False);
        Assert.That(transaction.IsRolledBack, Is.False);
        Assert.That(transaction.PendingOperationCount, Is.EqualTo(0));
    }

    [Test]
    public void Add_InTransaction_NotVisibleUntilCommit()
    {
        var vector = new Vector([1f, 2f, 3f]);

        using var transaction = _db.BeginTransaction();
        transaction.Add(vector);

        // Not visible outside transaction
        Assert.That(_db.GetVector(vector.Id), Is.Null);
        Assert.That(_db.Count, Is.EqualTo(0));
        Assert.That(transaction.PendingOperationCount, Is.EqualTo(1));

        transaction.Commit();

        // Now visible
        Assert.That(_db.GetVector(vector.Id), Is.Not.Null);
        Assert.That(_db.Count, Is.EqualTo(1));
    }

    [Test]
    public void Update_InTransaction_NotVisibleUntilCommit()
    {
        var vector = new Vector([1f, 2f, 3f]);
        _db.AddVector(vector);

        var updated = new Vector([4f, 5f, 6f]);

        using var transaction = _db.BeginTransaction();
        var result = transaction.Update(vector.Id, updated);

        Assert.That(result, Is.True);

        // Original value visible outside transaction
        var dbVector = _db.GetVector(vector.Id);
        Assert.That(dbVector!.Values, Is.EqualTo(new float[] { 1f, 2f, 3f }));

        transaction.Commit();

        // Updated value now visible
        dbVector = _db.GetVector(vector.Id);
        Assert.That(dbVector!.Values, Is.EqualTo(new float[] { 4f, 5f, 6f }));
    }

    [Test]
    public void Remove_InTransaction_NotVisibleUntilCommit()
    {
        var vector = new Vector([1f, 2f, 3f]);
        _db.AddVector(vector);

        using var transaction = _db.BeginTransaction();
        var result = transaction.Remove(vector.Id);

        Assert.That(result, Is.True);

        // Still visible outside transaction
        Assert.That(_db.GetVector(vector.Id), Is.Not.Null);
        Assert.That(_db.Count, Is.EqualTo(1));

        transaction.Commit();

        // Now removed
        Assert.That(_db.GetVector(vector.Id), Is.Null);
        Assert.That(_db.Count, Is.EqualTo(0));
    }

    [Test]
    public void Commit_AppliesAllOperationsAtomically()
    {
        var vector1 = new Vector([1f, 2f, 3f]);
        var vector2 = new Vector([4f, 5f, 6f]);
        _db.AddVector(vector1);

        var updated = new Vector([7f, 8f, 9f]);
        var added = new Vector([10f, 11f, 12f]);

        using var transaction = _db.BeginTransaction();
        transaction.Add(added);
        transaction.Update(vector1.Id, updated);
        transaction.Remove(vector2.Id); // Non-existent, should return false

        Assert.That(transaction.PendingOperationCount, Is.EqualTo(2));

        transaction.Commit();

        Assert.That(_db.Count, Is.EqualTo(2));
        Assert.That(_db.GetVector(vector1.Id)!.Values, Is.EqualTo(new float[] { 7f, 8f, 9f }));
        Assert.That(_db.GetVector(added.Id), Is.Not.Null);
    }

    [Test]
    public void Rollback_DiscardsAllBufferedOperations()
    {
        var vector = new Vector([1f, 2f, 3f]);

        using var transaction = _db.BeginTransaction();
        transaction.Add(vector);
        Assert.That(transaction.PendingOperationCount, Is.EqualTo(1));

        transaction.Rollback();

        Assert.That(transaction.IsRolledBack, Is.True);
        Assert.That(_db.GetVector(vector.Id), Is.Null);
        Assert.That(_db.Count, Is.EqualTo(0));
    }

    #endregion

    #region Read-Your-Writes

    [Test]
    public void GetVector_AfterAdd_ReturnsBufferedVector()
    {
        var vector = new Vector([1f, 2f, 3f]);

        using var transaction = _db.BeginTransaction();
        transaction.Add(vector);

        // Read-your-writes: visible inside transaction
        var result = transaction.GetVector(vector.Id);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Values, Is.EqualTo(new float[] { 1f, 2f, 3f }));
    }

    [Test]
    public void GetVector_AfterUpdate_ReturnsUpdatedVector()
    {
        var vector = new Vector([1f, 2f, 3f]);
        _db.AddVector(vector);

        var updated = new Vector([4f, 5f, 6f]);

        using var transaction = _db.BeginTransaction();
        transaction.Update(vector.Id, updated);

        // Read-your-writes: returns updated value
        var result = transaction.GetVector(vector.Id);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Values, Is.EqualTo(new float[] { 4f, 5f, 6f }));
    }

    [Test]
    public void GetVector_AfterDelete_ReturnsNull()
    {
        var vector = new Vector([1f, 2f, 3f]);
        _db.AddVector(vector);

        using var transaction = _db.BeginTransaction();
        transaction.Remove(vector.Id);

        // Read-your-writes: deleted vector returns null
        var result = transaction.GetVector(vector.Id);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void GetVector_FallsBackToDatabase()
    {
        var vector = new Vector([1f, 2f, 3f]);
        _db.AddVector(vector);

        using var transaction = _db.BeginTransaction();

        // No buffered operation, should return from database
        var result = transaction.GetVector(vector.Id);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Values, Is.EqualTo(new float[] { 1f, 2f, 3f }));
    }

    #endregion

    #region Edge Cases

    [Test]
    public void Update_AfterAdd_UpdatesBufferedVector()
    {
        var vector = new Vector([1f, 2f, 3f]);
        var updated = new Vector([4f, 5f, 6f]);

        using var transaction = _db.BeginTransaction();
        transaction.Add(vector);
        var result = transaction.Update(vector.Id, updated);

        Assert.That(result, Is.True);
        Assert.That(transaction.PendingOperationCount, Is.EqualTo(1)); // Still just one add

        var retrieved = transaction.GetVector(vector.Id);
        Assert.That(retrieved!.Values, Is.EqualTo(new float[] { 4f, 5f, 6f }));
    }

    [Test]
    public void Delete_AfterAdd_RemovesFromBuffer()
    {
        var vector = new Vector([1f, 2f, 3f]);

        using var transaction = _db.BeginTransaction();
        transaction.Add(vector);
        Assert.That(transaction.PendingOperationCount, Is.EqualTo(1));

        var result = transaction.Remove(vector.Id);
        Assert.That(result, Is.True);
        Assert.That(transaction.PendingOperationCount, Is.EqualTo(0)); // Removed from buffer

        Assert.That(transaction.GetVector(vector.Id), Is.Null);
    }

    [Test]
    public void Delete_ThenAdd_SameId_AddsVector()
    {
        var vector = new Vector([1f, 2f, 3f]);
        _db.AddVector(vector);

        var newVector = new Vector([4f, 5f, 6f]);
        newVector.Id = vector.Id; // Same ID

        using var transaction = _db.BeginTransaction();
        transaction.Remove(vector.Id);
        transaction.Add(newVector);

        // Should see the new vector
        var result = transaction.GetVector(vector.Id);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Values, Is.EqualTo(new float[] { 4f, 5f, 6f }));

        transaction.Commit();

        var dbResult = _db.GetVector(vector.Id);
        Assert.That(dbResult!.Values, Is.EqualTo(new float[] { 4f, 5f, 6f }));
    }

    [Test]
    public void Update_NonExistentVector_ReturnsFalse()
    {
        var nonExistentId = Guid.NewGuid();
        var vector = new Vector([1f, 2f, 3f]);

        using var transaction = _db.BeginTransaction();
        var result = transaction.Update(nonExistentId, vector);

        Assert.That(result, Is.False);
        Assert.That(transaction.PendingOperationCount, Is.EqualTo(0));
    }

    [Test]
    public void Update_DeletedVector_ReturnsFalse()
    {
        var vector = new Vector([1f, 2f, 3f]);
        _db.AddVector(vector);

        var updated = new Vector([4f, 5f, 6f]);

        using var transaction = _db.BeginTransaction();
        transaction.Remove(vector.Id);
        var result = transaction.Update(vector.Id, updated);

        Assert.That(result, Is.False);
    }

    [Test]
    public void Remove_NonExistentVector_ReturnsFalse()
    {
        var nonExistentId = Guid.NewGuid();

        using var transaction = _db.BeginTransaction();
        var result = transaction.Remove(nonExistentId);

        Assert.That(result, Is.False);
        Assert.That(transaction.PendingOperationCount, Is.EqualTo(0));
    }

    [Test]
    public void Remove_AlreadyDeleted_ReturnsFalse()
    {
        var vector = new Vector([1f, 2f, 3f]);
        _db.AddVector(vector);

        using var transaction = _db.BeginTransaction();
        var result1 = transaction.Remove(vector.Id);
        var result2 = transaction.Remove(vector.Id);

        Assert.That(result1, Is.True);
        Assert.That(result2, Is.False);
    }

    #endregion

    #region State Validation

    [Test]
    public void DoubleCommit_ThrowsInvalidOperationException()
    {
        using var transaction = _db.BeginTransaction();
        transaction.Commit();

        Assert.That(transaction.IsCommitted, Is.True);
        Assert.Throws<InvalidOperationException>(() => transaction.Commit());
    }

    [Test]
    public void OperationAfterCommit_ThrowsInvalidOperationException()
    {
        var vector = new Vector([1f, 2f, 3f]);

        using var transaction = _db.BeginTransaction();
        transaction.Commit();

        Assert.Throws<InvalidOperationException>(() => transaction.Add(vector));
        Assert.Throws<InvalidOperationException>(() => transaction.Update(Guid.NewGuid(), vector));
        Assert.Throws<InvalidOperationException>(() => transaction.Remove(Guid.NewGuid()));
        Assert.Throws<InvalidOperationException>(() => transaction.GetVector(Guid.NewGuid()));
    }

    [Test]
    public void OperationAfterRollback_ThrowsInvalidOperationException()
    {
        var vector = new Vector([1f, 2f, 3f]);

        using var transaction = _db.BeginTransaction();
        transaction.Rollback();

        Assert.Throws<InvalidOperationException>(() => transaction.Add(vector));
        Assert.Throws<InvalidOperationException>(() => transaction.Update(Guid.NewGuid(), vector));
        Assert.Throws<InvalidOperationException>(() => transaction.Remove(Guid.NewGuid()));
        Assert.Throws<InvalidOperationException>(() => transaction.GetVector(Guid.NewGuid()));
    }

    [Test]
    public void DisposeWithoutCommit_ImplicitRollback()
    {
        var vector = new Vector([1f, 2f, 3f]);
        IVectorTransaction transaction = _db.BeginTransaction();
        transaction.Add(vector);
        transaction.Dispose();

        Assert.That(transaction.IsRolledBack, Is.True);
        Assert.That(_db.GetVector(vector.Id), Is.Null);
    }

    [Test]
    public void EmptyTransaction_CommitSucceeds()
    {
        using var transaction = _db.BeginTransaction();
        Assert.That(transaction.PendingOperationCount, Is.EqualTo(0));

        transaction.Commit();

        Assert.That(transaction.IsCommitted, Is.True);
    }

    #endregion

    #region Concurrency

    [Test]
    public async Task ConcurrentTransactions_BothSucceed()
    {
        var vector1 = new Vector([1f, 2f, 3f]);
        var vector2 = new Vector([4f, 5f, 6f]);

        var task1 = Task.Run(() =>
        {
            using var tx = _db.BeginTransaction();
            tx.Add(vector1);
            Thread.Sleep(10);
            tx.Commit();
        });

        var task2 = Task.Run(() =>
        {
            using var tx = _db.BeginTransaction();
            tx.Add(vector2);
            Thread.Sleep(10);
            tx.Commit();
        });

        await Task.WhenAll(task1, task2);

        Assert.That(_db.Count, Is.EqualTo(2));
        Assert.That(_db.GetVector(vector1.Id), Is.Not.Null);
        Assert.That(_db.GetVector(vector2.Id), Is.Not.Null);
    }

    [Test]
    public async Task Transaction_DoesNotBlockReads()
    {
        var existingVector = new Vector([1f, 2f, 3f]);
        _db.AddVector(existingVector);

        var readCompleted = false;
        var transactionStarted = new ManualResetEventSlim(false);

        var txTask = Task.Run(() =>
        {
            using var tx = _db.BeginTransaction();
            tx.Add(new Vector([4f, 5f, 6f]));
            transactionStarted.Set();
            Thread.Sleep(50); // Hold transaction open
            tx.Commit();
        });

        var readTask = Task.Run(() =>
        {
            transactionStarted.Wait();
            // Read should succeed while transaction is open (before commit)
            var result = _db.GetVector(existingVector.Id);
            readCompleted = result != null;
        });

        await Task.WhenAll(txTask, readTask);

        Assert.That(readCompleted, Is.True);
    }

    [Test]
    public async Task CommitAsync_Works()
    {
        var vector = new Vector([1f, 2f, 3f]);

        using var transaction = _db.BeginTransaction();
        transaction.Add(vector);

        await transaction.CommitAsync();

        Assert.That(transaction.IsCommitted, Is.True);
        Assert.That(_db.GetVector(vector.Id), Is.Not.Null);
    }

    [Test]
    public async Task CommitAsync_CanBeCancelled()
    {
        var vector = new Vector([1f, 2f, 3f]);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        using var transaction = _db.BeginTransaction();
        transaction.Add(vector);

        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await transaction.CommitAsync(cts.Token));
    }

    #endregion

    #region Modified Event

    [Test]
    public void Transaction_ModifiedEvent_FiresOnceOnCommit()
    {
        var modifiedCount = 0;
        _db.Vectors.Modified += (_, _) => modifiedCount++;

        var vector1 = new Vector([1f, 2f, 3f]);
        var vector2 = new Vector([4f, 5f, 6f]);
        var vector3 = new Vector([7f, 8f, 9f]);

        using var transaction = _db.BeginTransaction();
        transaction.Add(vector1);
        transaction.Add(vector2);
        transaction.Add(vector3);

        Assert.That(modifiedCount, Is.EqualTo(0)); // No events during transaction

        transaction.Commit();

        Assert.That(modifiedCount, Is.EqualTo(1)); // Single event on commit
    }

    [Test]
    public void Transaction_Rollback_NoModifiedEvent()
    {
        var modifiedCount = 0;
        _db.Vectors.Modified += (_, _) => modifiedCount++;

        var vector = new Vector([1f, 2f, 3f]);

        using var transaction = _db.BeginTransaction();
        transaction.Add(vector);
        transaction.Rollback();

        Assert.That(modifiedCount, Is.EqualTo(0));
    }

    #endregion
}
