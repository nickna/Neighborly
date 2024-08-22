using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neighborly.Tests.DataPersistence;

[TestFixture]
class IDataPersistenceTests
{
    private VectorList vectorList;
    private BinaryWriter writer;
    private BinaryReader reader;

    [SetUp]
    public void Setup()
    {
        vectorList = new VectorList();
        writer = new BinaryWriter(new MemoryStream());
        reader = new BinaryReader(new MemoryStream());
    }

    [TearDown]
    public void TearDown()
    {
        if (vectorList != null)
        {
            vectorList.Dispose();
        }
        if (writer != null)
        {
            writer.Dispose();
        }
        if (reader != null)
        {
            reader.Dispose();
        }
    }

    [Test]
    public void Test_ToBinaryStream_Tags()
    {
        // Arrange
        var Tags = new VectorTags(vectorList);
        var tagStrings = new[] { "tag1", "tag2", "tag3", "tag4", "tag5" };
        foreach (var tag in tagStrings)
        {
            Tags.Add(tag);
        }

        // Act
        Tags.ToBinaryStream(writer);
        writer.Flush();
        reader = new BinaryReader(new MemoryStream((writer.BaseStream as MemoryStream)!.ToArray()));
        var newTags = new VectorTags(reader, new VectorList());

        // Assert
        Assert.That(Tags.Count, Is.EqualTo(newTags.Count)); 

        foreach (var tag in tagStrings)
        {
            Assert.That(newTags.Contains(tag), Is.True, $"Tag '{tag}' not found in newTags");
            Assert.That(newTags[tag], Is.EqualTo(Tags[tag]), $"Tag ID mismatch for '{tag}'");
        }
    }

    [Test]
    public void Test_ToBinaryStream_VectorAttributes()
    {
        // Arrange
        VectorAttributes va = new VectorAttributes();
        va.Priority = 100;
        va.OrgId = 200;
        va.UserId = 150;

        // Act
        va.ToBinaryStream(writer);
        writer.Flush();
        reader = new BinaryReader(new MemoryStream((writer.BaseStream as MemoryStream)!.ToArray()));
        var newVa = new VectorAttributes(reader);

        // Assert
        Assert.That(newVa.Priority, Is.EqualTo(va.Priority));
        Assert.That(newVa.OrgId, Is.EqualTo(va.OrgId));
    }

    [Test]
    public void Test_ToBinaryStream_Vector()
    {
        // Arrange
        VectorAttributes va = new VectorAttributes();
        va.Priority = 100;
        va.OrgId = 200;
        va.UserId = 150;
        
        short[] tags = new short[] { 10, 20, 30 };

        var originalText = "This is a test";
        var values = new float[] { 1.5f, 2.3f, 3.9f };

        Vector originalVector = new Vector(
            vectorAttributes:va,
            originalText: originalText,
            values: values,
            tags:tags);
        
        // Act
        originalVector.ToBinaryStream(writer);
        writer.Flush();
        reader = new BinaryReader(new MemoryStream((writer.BaseStream as MemoryStream)!.ToArray()));
        var newVector = new Vector(reader);

        // Assert
        Assert.That(newVector.OriginalText, Is.EqualTo(originalVector.OriginalText));
        Assert.That(newVector.Values, Is.EqualTo(originalVector.Values));
        Assert.That(newVector.Id, Is.EqualTo(originalVector.Id));
        Assert.That(newVector.Attributes, Is.EqualTo(originalVector.Attributes));
        Assert.That(newVector.Tags, Is.EqualTo(originalVector.Tags));
    }

    [Test]
    public void Test_ToBinaryStream_VectorList()
    {
        // Arrange
        List<Vector> originalVectors = new List<Vector>();
        for (int x = 0; x < 127; x++)
        {
            VectorAttributes va = new VectorAttributes();
            va.Priority = (sbyte)Random.Shared.Next(0,100);
            va.OrgId = (uint)Random.Shared.Next(0, int.MaxValue);
            va.UserId = (uint)Random.Shared.Next(0, int.MaxValue);

            short[] tags = new short[] 
            { 
                (short)Random.Shared.Next(0,short.MaxValue),
                (short)Random.Shared.Next(0,short.MaxValue),
                (short)Random.Shared.Next(0,short.MaxValue) 
            };
            var originalText = $"This is a test {DateTime.Now.Ticks} -- {Random.Shared.Next()}";
            var values = new float[] 
            { 
                Random.Shared.NextSingle(),
                Random.Shared.NextSingle(),
                Random.Shared.NextSingle()
            };

            Vector originalVector = new Vector(
                vectorAttributes: va,
                originalText: originalText,
                values: values,
                tags: tags);
            originalVectors.Add(originalVector);
        }
        vectorList.AddRange(originalVectors);
        
        // Act
        vectorList.ToBinaryStream(writer);
        writer.Flush();
        reader = new BinaryReader(new MemoryStream((writer.BaseStream as MemoryStream)!.ToArray()));
        var newVectorList = new VectorList(reader);
        
        // Assert
        Assert.That(newVectorList.Count, Is.EqualTo(vectorList.Count));

        for ( int i = 0; i < vectorList.Count; i++)
        {
            Assert.That(newVectorList[i].OriginalText, Is.EqualTo(vectorList[i].OriginalText));
            Assert.That(newVectorList[i].Values, Is.EqualTo(vectorList[i].Values));
            Assert.That(newVectorList[i].Id, Is.EqualTo(vectorList[i].Id));
            Assert.That(newVectorList[i].Attributes, Is.EqualTo(vectorList[i].Attributes));
            Assert.That(newVectorList[i].Tags, Is.EqualTo(vectorList[i].Tags));
        }
    }
}
