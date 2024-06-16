using Neighborly;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests
{
   
    [TestFixture]

    public class MemoryMappedFileTests
    {
        private VectorDatabase _db;

        [SetUp]
        public void Setup()
        {
            _db?.Vectors?.Dispose();

            _db = new VectorDatabase();
        }

        [Test]
        public void Defrag_WhenCalled_RemovesTombstonedEntriesAndCompactsData()
        {
            // Arrange
            var vector1 = new Vector(new float[] { 1, 2, 3 });
            var vector2 = new Vector(new float[] { 4, 5, 6 });
            var vector3 = new Vector(new float[] { 7, 8, 9 });

            _db.Vectors.Add(vector1);
            _db.Vectors.Add(vector2);
            _db.Vectors.Add(vector3);

            // Act: Remove vector2 and defragment
            _db.Vectors.Remove(vector2);
            int countBeforeDefrag = _db.Vectors.Count;
            _db.Vectors.Defrag();
            int countAfterDefrag = _db.Vectors.Count;

            // Assert
            Assert.That(_db.Vectors.Contains(vector1), Is.True, "Database should contain vector1.");
            Assert.That(_db.Vectors.Contains(vector2), Is.False, "Database should not contain removed vector2.");
            Assert.That(_db.Vectors.Contains(vector3), Is.True, "Database should contain vector3.");
            Assert.That(countBeforeDefrag, Is.EqualTo(2), "Count should be 2 before defragmentation.");
            Assert.That(countAfterDefrag, Is.EqualTo(2), "Count should remain 2 after defragmentation.");

            // Check that the valid entries can be accessed correctly
            var retrievedVector1 = _db.Vectors.Find(v => v.Id == vector1.Id);
            var retrievedVector3 = _db.Vectors.Find(v => v.Id == vector3.Id);
            Assert.That(retrievedVector1, Is.EqualTo(vector1), "Retrieved vector1 should match the original vector1.");
            Assert.That(retrievedVector3, Is.EqualTo(vector3), "Retrieved vector3 should match the original vector3.");
        }

        [Test]
        public void Defrag_WithLargeNumberOfEntries_CompletesInReasonableTime()
        {
            // Arrange
            int entryCount = 10000; // Adjust based on expected performance
            for (int i = 0; i < entryCount; i++)
            {
                var vector = new Vector(new float[] { i, i + 1, i + 2 });
                _db.Vectors.Add(vector);
            }

            // Act
            var stopwatch = Stopwatch.StartNew();
            _db.Vectors.Defrag();
            stopwatch.Stop();

            // Assert
            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(1000), "Defrag should complete in under 1 second for 10,000 entries."); // Adjust time based on expected performance
        }

        [Test]
        public void Defrag_WhenCalledOnEmptyDatabase_DoesNotThrow()
        {
            // Arrange - Ensure database is empty
            _db.Vectors.Clear();

            // Act & Assert
            Assert.DoesNotThrow(() => _db.Vectors.Defrag(), "Defrag should not throw an exception when called on an empty database.");
        }

        [Test]
        public void Defrag_WhenCalledOnDatabaseWithNoTombstonedEntries_DoesNotThrow()
        {
            // Arrange - Ensure database has no tombstoned entries
            var vector1 = new Vector(new float[] { 1, 2, 3 });
            var vector2 = new Vector(new float[] { 4, 5, 6 });
            var vector3 = new Vector(new float[] { 7, 8, 9 });

            _db.Vectors.Add(vector1);
            _db.Vectors.Add(vector2);
            _db.Vectors.Add(vector3);

            // Act & Assert
            Assert.DoesNotThrow(() => _db.Vectors.Defrag(), "Defrag should not throw an exception when called on a database with no tombstoned entries.");
        }
        
        [Test]
        public void Defrag_WhenCalledOnDatabaseWithNoTombstonedEntries_CountRemainsUnchanged()
        {
            // Arrange - Ensure database has no tombstoned entries
            var vector1 = new Vector(new float[] { 1, 2, 3 });
            var vector2 = new Vector(new float[] { 4, 5, 6 });
            var vector3 = new Vector(new float[] { 7, 8, 9 });

            _db.Vectors.Add(vector1);
            _db.Vectors.Add(vector2);
            _db.Vectors.Add(vector3);

            // Act
            _db.Vectors.Defrag();

            // Assert
            Assert.That(_db.Count, Is.EqualTo(3), "Count should remain unchanged when defrag is called on a database with no tombstoned entries.");
        }

        // Test the CalculateFragmentation method
        [Test]
        public void CalculateFragmentation_WhenCalled_ReturnsCorrectValue()
        {
            // Arrange
            var vector1 = new Vector(new float[] { 1, 2, 3 });
            var vector2 = new Vector(new float[] { 4, 5, 6 });
            var vector3 = new Vector(new float[] { 7, 8, 9 });

            // Create a MemoryMappedList with a smaller capacity
            var capacity = 10L;
            using var db = new MemoryMappedList(capacity);

            db.Add(vector1);
            db.Add(vector2);
            db.Add(vector3);

            // Act
            db.Remove(vector2);
            var fragmentation = db.CalculateFragmentation();

            // Assert
            Assert.That(fragmentation, Is.EqualTo(50), "Fragmentation should be 50% after removing one entry.");
        }

        [Test]
        public void DefragBatch_WhenCalled_RemovesTombstonedEntriesAndCompactsData()
        {
            // Arrange
            var vectors = new List<Vector>();
            int numBatches = 3;
            int entriesPerBatch = 100;

            for (int i = 0; i < numBatches * entriesPerBatch; i++)
            {
                var vector = new Vector(new float[] { i, i + 1, i + 2 });
                vectors.Add(vector);
                _db.Vectors.Add(vector);
            }

            // Remove some vectors to create tombstoned entries
            for (int i = 0; i < numBatches * entriesPerBatch; i += 2)
            {
                _db.Vectors.Remove(vectors[i]);
            }

            int countBeforeDefrag = _db.Vectors.Count;

            // Act: Defragment in batches
            while (_db.Vectors.CalculateFragmentation() > 0)
            {
                _db.Vectors.DefragBatch();
            }

            int countAfterDefrag = _db.Vectors.Count;

            // Assert
            Assert.That(countBeforeDefrag, Is.EqualTo(countAfterDefrag), "Count should remain unchanged after defragmentation.");

            // Check that the valid entries can be accessed correctly
            for (int i = 1; i < numBatches * entriesPerBatch; i += 2)
            {
                var retrievedVector = _db.Vectors.Find(v => v.Id == vectors[i].Id);
                Assert.That(retrievedVector, Is.EqualTo(vectors[i]), $"Retrieved vector at index {i} should match the original vector.");
            }
        }

        [Test]
        public void DefragBatch_WithLargeNumberOfEntries_CompletesInReasonableTime()
        {
            // Arrange
            var vectors = new List<Vector>();
            int numBatches = 3;
            int entriesPerBatch = 100;

            for (int i = 0; i < numBatches * entriesPerBatch; i++)
            {
                var vector = new Vector(new float[] { i, i + 1, i + 2 });
                vectors.Add(vector);
                _db.Vectors.Add(vector);
            }

            // Remove some vectors to create tombstoned entries
            for (int i = 0; i < numBatches * entriesPerBatch; i += 2)
            {
                _db.Vectors.Remove(vectors[i]);
            }

            // Act
            // Keep in mind that the CalculateFragmentation method is I/O intensive
            // and pollutes the measured time to complete a defragmentation.
            var stopwatch = Stopwatch.StartNew();
            while (_db.Vectors.CalculateFragmentation() > 0) 
            {
                _db.Vectors.DefragBatch();
            }
            {
                _db.Vectors.DefragBatch();
            }
            stopwatch.Stop();

            // Assert
            var maxAcceptableTime = TimeSpan.FromSeconds(30); // Adjust the threshold as needed
            Assert.That(stopwatch.Elapsed, Is.LessThan(maxAcceptableTime), $"Defragmentation should complete within {maxAcceptableTime.TotalSeconds} seconds.");
        }


    }
}
