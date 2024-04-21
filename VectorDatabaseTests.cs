using NUnit.Framework;
using Neighborly;
using System.Collections.Generic;

[TestFixture]
public class VectorDatabaseTests
{
    [Test]
    public void TestAdd()
    {
        var db = new VectorDatabase();
        var vector = new Vector(1, 2, 3);
        db.Add(vector);
        Assert.AreEqual(1, db.Count);
        Assert.IsTrue(db.Contains(vector));
    }

    [Test]
    public void TestRemove()
    {
        var db = new VectorDatabase();
        var vector = new Vector(1, 2, 3);
        db.Add(vector);
        db.Remove(vector);
        Assert.AreEqual(0, db.Count);
        Assert.IsFalse(db.Contains(vector));
    }

    [Test]
    public void TestUpdate()
    {
        var db = new VectorDatabase();
        var oldVector = new Vector(1, 2, 3);
        var newVector = new Vector(4, 5, 6);
        db.Add(oldVector);
        db.Update(oldVector, newVector);
        Assert.AreEqual(1, db.Count);
        Assert.IsFalse(db.Contains(oldVector));
        Assert.IsTrue(db.Contains(newVector));
    }

    [Test]
    public void TestAddRange()
    {
        var db = new VectorDatabase();
        var vectors = new List<Vector> { new Vector(1, 2, 3), new Vector(4, 5, 6) };
        db.AddRange(vectors);
        Assert.AreEqual(2, db.Count);
        foreach (var vector in vectors)
        {
            Assert.IsTrue(db.Contains(vector));
        }
    }

    [Test]
    public void TestRemoveRange()
    {
        var db = new VectorDatabase();
        var vectors = new List<Vector> { new Vector(1, 2, 3), new Vector(4, 5, 6) };
        db.AddRange(vectors);
        db.RemoveRange(vectors);
        Assert.AreEqual(0, db.Count);
        foreach (var vector in vectors)
        {
            Assert.IsFalse(db.Contains(vector));
        }
    }

    // ... other tests ...
}
