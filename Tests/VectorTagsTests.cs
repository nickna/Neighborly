using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Neighborly.Tests;

[TestFixture]
public class VectorTagsTests
{
    private VectorList _vectorList = null!;
    private VectorTags _tags = null!;

    [SetUp]
    public void SetUp()
    {
        _vectorList = new VectorList();
        _tags = _vectorList.Tags;
    }

    [TearDown]
    public void TearDown()
    {
        _vectorList.Dispose();
    }

    #region Basic CRUD Operations

    [Test]
    public void Add_NewTag_ReturnsNewId()
    {
        // Act
        var id = _tags.Add("test-tag");

        // Assert
        Assert.That(id, Is.EqualTo((short)1));
        Assert.That(_tags.Count, Is.EqualTo(1));
    }

    [Test]
    public void Add_ExistingTag_ReturnsSameId()
    {
        // Arrange
        var id1 = _tags.Add("test-tag");

        // Act
        var id2 = _tags.Add("test-tag");
        var id3 = _tags.Add("TEST-TAG"); // Case insensitive

        // Assert
        Assert.That(id2, Is.EqualTo(id1));
        Assert.That(id3, Is.EqualTo(id1));
        Assert.That(_tags.Count, Is.EqualTo(1));
    }

    [Test]
    public void Add_NullOrWhitespace_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _tags.Add(null!));
        Assert.Throws<ArgumentException>(() => _tags.Add(""));
        Assert.Throws<ArgumentException>(() => _tags.Add("   "));
    }

    [Test]
    public void Add_TrimsWhitespace()
    {
        // Act
        var id1 = _tags.Add("  test  ");
        var id2 = _tags.Add("test");

        // Assert
        Assert.That(id1, Is.EqualTo(id2));
        Assert.That(_tags["test"], Is.EqualTo((short)1));
    }

    [Test]
    public void GetId_ExistingTag_ReturnsId()
    {
        // Arrange
        var expectedId = _tags.Add("my-tag");

        // Act
        var id = _tags.GetId("my-tag");

        // Assert
        Assert.That(id, Is.EqualTo(expectedId));
    }

    [Test]
    public void GetId_NonExistingTag_ReturnsNegativeOne()
    {
        // Act
        var id = _tags.GetId("non-existent");

        // Assert
        Assert.That(id, Is.EqualTo((short)-1));
    }

    [Test]
    public void GetId_NullOrWhitespace_ReturnsNegativeOne()
    {
        Assert.That(_tags.GetId(null!), Is.EqualTo((short)-1));
        Assert.That(_tags.GetId(""), Is.EqualTo((short)-1));
        Assert.That(_tags.GetId("   "), Is.EqualTo((short)-1));
    }

    [Test]
    public void TryGetId_ExistingTag_ReturnsTrueWithId()
    {
        // Arrange
        var expectedId = _tags.Add("my-tag");

        // Act
        var result = _tags.TryGetId("my-tag", out var id);

        // Assert
        Assert.That(result, Is.True);
        Assert.That(id, Is.EqualTo(expectedId));
    }

    [Test]
    public void TryGetId_NonExistingTag_ReturnsFalse()
    {
        // Act
        var result = _tags.TryGetId("non-existent", out var id);

        // Assert
        Assert.That(result, Is.False);
        Assert.That(id, Is.EqualTo((short)-1));
    }

    [Test]
    public void Remove_ById_ExistingTag_ReturnsTrue()
    {
        // Arrange
        var id = _tags.Add("to-remove");

        // Act
        var result = _tags.Remove(id);

        // Assert
        Assert.That(result, Is.True);
        Assert.That(_tags.Count, Is.EqualTo(0));
        Assert.That(_tags.Contains("to-remove"), Is.False);
    }

    [Test]
    public void Remove_ById_NonExistingTag_ReturnsFalse()
    {
        // Act
        var result = _tags.Remove((short)999);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void Remove_ByName_ExistingTag_ReturnsTrue()
    {
        // Arrange
        _tags.Add("to-remove");

        // Act
        var result = _tags.Remove("to-remove");

        // Assert
        Assert.That(result, Is.True);
        Assert.That(_tags.Count, Is.EqualTo(0));
    }

    [Test]
    public void Remove_ByName_NonExistingTag_ReturnsFalse()
    {
        // Act
        var result = _tags.Remove("non-existent");

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void Contains_ExistingTag_ReturnsTrue()
    {
        // Arrange
        _tags.Add("exists");

        // Act & Assert
        Assert.That(_tags.Contains("exists"), Is.True);
        Assert.That(_tags.Contains("EXISTS"), Is.True); // Case insensitive
    }

    [Test]
    public void Contains_NonExistingTag_ReturnsFalse()
    {
        Assert.That(_tags.Contains("not-exists"), Is.False);
    }

    [Test]
    public void Clear_RemovesAllTags()
    {
        // Arrange
        _tags.Add("tag1");
        _tags.Add("tag2");
        _tags.Add("tag3");

        // Act
        _tags.Clear();

        // Assert
        Assert.That(_tags.Count, Is.EqualTo(0));
    }

    [Test]
    public void Indexer_ShortId_ReturnsTagName()
    {
        // Arrange
        var id = _tags.Add("my-tag");

        // Act
        var name = _tags[id];

        // Assert
        Assert.That(name, Is.EqualTo("my-tag"));
    }

    [Test]
    public void Indexer_String_ReturnsTagId()
    {
        // Arrange
        var expectedId = _tags.Add("my-tag");

        // Act
        var id = _tags["my-tag"];

        // Assert
        Assert.That(id, Is.EqualTo(expectedId));
    }

    #endregion

    #region Range Operations

    [Test]
    public void GetIdRange_MultipleTags_ReturnsIds()
    {
        // Arrange
        var id1 = _tags.Add("tag1");
        var id2 = _tags.Add("tag2");
        _tags.Add("tag3");

        // Act
        var ids = _tags.GetIdRange(new[] { "tag1", "tag2", "non-existent" });

        // Assert
        Assert.That(ids, Has.Length.EqualTo(3));
        Assert.That(ids[0], Is.EqualTo(id1));
        Assert.That(ids[1], Is.EqualTo(id2));
        Assert.That(ids[2], Is.EqualTo((short)-1));
    }

    [Test]
    public void GetRange_MultipleIds_ReturnsNames()
    {
        // Arrange
        var id1 = _tags.Add("tag1");
        var id2 = _tags.Add("tag2");

        // Act
        var names = _tags.GetRange(new[] { id1, id2 });

        // Assert
        Assert.That(names, Has.Length.EqualTo(2));
        Assert.That(names[0], Is.EqualTo("tag1"));
        Assert.That(names[1], Is.EqualTo("tag2"));
    }

    [Test]
    public void GetRangeAsString_MultipleIds_ReturnsCommaSeparated()
    {
        // Arrange
        var id1 = _tags.Add("alpha");
        var id2 = _tags.Add("beta");
        var id3 = _tags.Add("gamma");

        // Act
        var result = _tags.GetRangeAsString(new[] { id1, id2, id3 });

        // Assert
        Assert.That(result, Is.EqualTo("alpha, beta, gamma"));
    }

    [Test]
    public void GetRangeAsString_EmptyArray_ReturnsEmpty()
    {
        Assert.That(_tags.GetRangeAsString(Array.Empty<short>()), Is.EqualTo(string.Empty));
        Assert.That(_tags.GetRangeAsString(null!), Is.EqualTo(string.Empty));
    }

    #endregion

    #region Tag-to-Vector Query Methods

    [Test]
    public void GetVectorIdsByTag_AfterBuildMap_ReturnsVectorIds()
    {
        // Arrange
        var tag1 = _tags.Add("category-a");
        var tag2 = _tags.Add("category-b");

        var vector1 = new Vector(new float[] { 1, 2, 3 });
        var vector2 = new Vector(new float[] { 4, 5, 6 });

        // Create vectors with tags using internal constructor via reflection or setup
        var vectorWithTag1 = CreateVectorWithTags(new[] { tag1 });
        var vectorWithTag2 = CreateVectorWithTags(new[] { tag1, tag2 });
        var vectorWithTag3 = CreateVectorWithTags(new[] { tag2 });

        _vectorList.Add(vectorWithTag1);
        _vectorList.Add(vectorWithTag2);
        _vectorList.Add(vectorWithTag3);

        // Act
        _tags.BuildMap();
        var result = _tags.GetVectorIdsByTag(tag1);

        // Assert
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result, Contains.Item(vectorWithTag1.Id));
        Assert.That(result, Contains.Item(vectorWithTag2.Id));
    }

    [Test]
    public void GetVectorIdsByTag_NonExistingTag_ReturnsEmpty()
    {
        // Arrange
        _tags.BuildMap();

        // Act
        var result = _tags.GetVectorIdsByTag((short)999);

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void GetVectorIdsByTags_Intersection_ReturnsMatchingVectors()
    {
        // Arrange
        var tag1 = _tags.Add("red");
        var tag2 = _tags.Add("large");
        var tag3 = _tags.Add("heavy");

        var vectorBoth = CreateVectorWithTags(new[] { tag1, tag2 });
        var vectorOnlyTag1 = CreateVectorWithTags(new[] { tag1 });
        var vectorOnlyTag2 = CreateVectorWithTags(new[] { tag2 });

        _vectorList.Add(vectorBoth);
        _vectorList.Add(vectorOnlyTag1);
        _vectorList.Add(vectorOnlyTag2);

        _tags.BuildMap();

        // Act
        var result = _tags.GetVectorIdsByTags(new[] { tag1, tag2 });

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result, Contains.Item(vectorBoth.Id));
    }

    [Test]
    public void GetVectorIdsByTags_NoIntersection_ReturnsEmpty()
    {
        // Arrange
        var tag1 = _tags.Add("red");
        var tag2 = _tags.Add("blue");

        var vectorTag1 = CreateVectorWithTags(new[] { tag1 });
        var vectorTag2 = CreateVectorWithTags(new[] { tag2 });

        _vectorList.Add(vectorTag1);
        _vectorList.Add(vectorTag2);

        _tags.BuildMap();

        // Act
        var result = _tags.GetVectorIdsByTags(new[] { tag1, tag2 });

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void GetVectorIdsByAnyTag_Union_ReturnsAllMatching()
    {
        // Arrange
        var tag1 = _tags.Add("red");
        var tag2 = _tags.Add("blue");
        var tag3 = _tags.Add("green");

        var vectorTag1 = CreateVectorWithTags(new[] { tag1 });
        var vectorTag2 = CreateVectorWithTags(new[] { tag2 });
        var vectorTag3 = CreateVectorWithTags(new[] { tag3 });

        _vectorList.Add(vectorTag1);
        _vectorList.Add(vectorTag2);
        _vectorList.Add(vectorTag3);

        _tags.BuildMap();

        // Act
        var result = _tags.GetVectorIdsByAnyTag(new[] { tag1, tag2 });

        // Assert
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result, Contains.Item(vectorTag1.Id));
        Assert.That(result, Contains.Item(vectorTag2.Id));
    }

    [Test]
    public void GetVectorIdsByAnyTag_EmptyInput_ReturnsEmpty()
    {
        Assert.That(_tags.GetVectorIdsByAnyTag(Array.Empty<short>()), Is.Empty);
        Assert.That(_tags.GetVectorIdsByAnyTag(null!), Is.Empty);
    }

    #endregion

    #region Serialization Tests

    [Test]
    public void ToBinary_EmptyTags_ReturnsEmptyArray()
    {
        // Act
        var result = _tags.ToBinary();

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void ToBinary_FromBinary_RoundTrip()
    {
        // Arrange
        var id1 = _tags.Add("alpha");
        var id2 = _tags.Add("beta");
        var id3 = _tags.Add("gamma");

        // Act
        var binary = _tags.ToBinary();
        _tags.Clear();
        _tags.FromBinary(binary);

        // Assert
        Assert.That(_tags.Count, Is.EqualTo(3));
        Assert.That(_tags[id1], Is.EqualTo("alpha"));
        Assert.That(_tags[id2], Is.EqualTo("beta"));
        Assert.That(_tags[id3], Is.EqualTo("gamma"));
        Assert.That(_tags.GetId("alpha"), Is.EqualTo(id1));
        Assert.That(_tags.GetId("beta"), Is.EqualTo(id2));
    }

    [Test]
    public void FromBinary_EmptyData_ClearsTags()
    {
        // Arrange
        _tags.Add("existing");

        // Act
        _tags.FromBinary(Array.Empty<byte>());

        // Assert
        Assert.That(_tags.Count, Is.EqualTo(0));
    }

    [Test]
    public void FromBinary_NullData_ClearsTags()
    {
        // Arrange
        _tags.Add("existing");

        // Act
        _tags.FromBinary(null!);

        // Assert
        Assert.That(_tags.Count, Is.EqualTo(0));
    }

    [Test]
    public void ToBinary_UnicodeCharacters_RoundTrips()
    {
        // Arrange
        _tags.Add("日本語");
        _tags.Add("中文");
        _tags.Add("emoji-🎉");

        // Act
        var binary = _tags.ToBinary();
        _tags.Clear();
        _tags.FromBinary(binary);

        // Assert
        Assert.That(_tags.Contains("日本語"), Is.True);
        Assert.That(_tags.Contains("中文"), Is.True);
        Assert.That(_tags.Contains("emoji-🎉"), Is.True);
    }

    #endregion

    #region GetAllTags and GetAllAsync

    [Test]
    public void GetAllTags_ReturnsAllTagNames()
    {
        // Arrange
        _tags.Add("alpha");
        _tags.Add("beta");
        _tags.Add("gamma");

        // Act
        var result = _tags.GetAllTags();

        // Assert
        Assert.That(result, Has.Count.EqualTo(3));
        Assert.That(result, Contains.Item("alpha"));
        Assert.That(result, Contains.Item("beta"));
        Assert.That(result, Contains.Item("gamma"));
    }

    [Test]
    public async Task GetAllAsync_ReturnsAllTagNames()
    {
        // Arrange
        _tags.Add("alpha");
        _tags.Add("beta");

        // Act
        var result = new List<string>();
        await foreach (var tag in _tags.GetAllAsync())
        {
            result.Add(tag);
        }

        // Assert
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result, Contains.Item("alpha"));
        Assert.That(result, Contains.Item("beta"));
    }

    [Test]
    public async Task GetAllAsync_WithCancellation_Throws()
    {
        // Arrange
        _tags.Add("alpha");
        _tags.Add("beta");
        _tags.Add("gamma");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var tag in _tags.GetAllAsync(cts.Token))
            {
                // Should throw before completing
            }
        });
    }

    #endregion

    #region Thread Safety Tests

    [Test]
    public void ConcurrentAdds_NoExceptions()
    {
        // Arrange
        var tasks = new List<Task>();
        var exceptions = new List<Exception>();

        // Act
        for (int i = 0; i < 100; i++)
        {
            int index = i;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    _tags.Add($"tag-{index}");
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert
        Assert.That(exceptions, Is.Empty);
        Assert.That(_tags.Count, Is.EqualTo(100));
    }

    [Test]
    public void ConcurrentReadsAndWrites_NoExceptions()
    {
        // Arrange
        _tags.Add("initial");
        var exceptions = new List<Exception>();
        var tasks = new List<Task>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // Act - concurrent reads
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        _ = _tags.GetId("initial");
                        _ = _tags.Contains("initial");
                        _ = _tags.Count;
                        _ = _tags.GetAllTags();
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
            }, cts.Token));
        }

        // Act - concurrent writes
        for (int i = 0; i < 5; i++)
        {
            int index = i;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    for (int j = 0; j < 20; j++)
                    {
                        _tags.Add($"tag-{index}-{j}");
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert
        Assert.That(exceptions, Is.Empty);
    }

    [Test]
    public void BuildMap_ConcurrentWithReads_NoExceptions()
    {
        // Arrange
        var tag1 = _tags.Add("tag1");
        var vector = CreateVectorWithTags(new[] { tag1 });
        _vectorList.Add(vector);

        var exceptions = new List<Exception>();
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    for (int j = 0; j < 100; j++)
                    {
                        _ = _tags.GetVectorIdsByTag(tag1);
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
            }));
        }

        // Concurrent BuildMap calls
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    _tags.BuildMap();
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert
        Assert.That(exceptions, Is.Empty);
    }

    #endregion

    #region Modified Event Tests

    [Test]
    public void Add_RaisesModifiedEvent()
    {
        // Arrange
        bool eventRaised = false;
        _tags.Modified += (s, e) => eventRaised = true;

        // Act
        _tags.Add("new-tag");

        // Assert
        Assert.That(eventRaised, Is.True);
    }

    [Test]
    public void Add_ExistingTag_DoesNotRaiseEvent()
    {
        // Arrange
        _tags.Add("existing");
        int eventCount = 0;
        _tags.Modified += (s, e) => eventCount++;

        // Act
        _tags.Add("existing");

        // Assert
        Assert.That(eventCount, Is.EqualTo(0));
    }

    [Test]
    public void Remove_RaisesModifiedEvent()
    {
        // Arrange
        var id = _tags.Add("to-remove");
        bool eventRaised = false;
        _tags.Modified += (s, e) => eventRaised = true;

        // Act
        _tags.Remove(id);

        // Assert
        Assert.That(eventRaised, Is.True);
    }

    [Test]
    public void Clear_RaisesModifiedEvent()
    {
        // Arrange
        _tags.Add("tag1");
        bool eventRaised = false;
        _tags.Modified += (s, e) => eventRaised = true;

        // Act
        _tags.Clear();

        // Assert
        Assert.That(eventRaised, Is.True);
    }

    #endregion

    #region Helper Methods

    private static Vector CreateVectorWithTags(short[] tags)
    {
        return new Vector(
            id: Guid.NewGuid(),
            values: new float[] { 1.0f, 2.0f, 3.0f },
            tags: tags,
            originalText: null
        );
    }

    #endregion
}
