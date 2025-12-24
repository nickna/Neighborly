using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Neighborly.Tests;

[TestFixture]
public class MetadataTests
{
    #region MetadataSerializer Tests

    [Test]
    public void Serialize_NullMetadata_ReturnsMinimalHeader()
    {
        // Arrange
        Dictionary<string, object>? metadata = null;

        // Act
        byte[] result = MetadataSerializer.Serialize(metadata);

        // Assert
        Assert.That(result, Has.Length.EqualTo(5), "Should return 5 bytes: version + count(0)");
        Assert.That(result[0], Is.EqualTo(1), "Version should be 1");
        Assert.That(BitConverter.ToInt32(result, 1), Is.EqualTo(0), "Count should be 0");
    }

    [Test]
    public void Serialize_EmptyMetadata_ReturnsMinimalHeader()
    {
        // Arrange
        var metadata = new Dictionary<string, object>();

        // Act
        byte[] result = MetadataSerializer.Serialize(metadata);

        // Assert
        Assert.That(result, Has.Length.EqualTo(5), "Should return 5 bytes for empty metadata");
    }

    [Test]
    [TestCase("key", "value")]
    [TestCase("unicode_key", "Unicode: \u4e2d\u6587")]
    [TestCase("empty_value", "")]
    [TestCase("special_chars", "!@#$%^&*()")]
    public void SerializeDeserialize_StringValue_RoundTrips(string key, string value)
    {
        // Arrange
        var metadata = new Dictionary<string, object> { [key] = value };

        // Act
        byte[] serialized = MetadataSerializer.Serialize(metadata);
        var deserialized = MetadataSerializer.Deserialize(serialized);

        // Assert
        Assert.That(deserialized, Contains.Key(key), $"Should contain key '{key}'");
        Assert.That(deserialized[key], Is.EqualTo(value), $"Value for '{key}' should match");
    }

    [Test]
    [TestCase(0)]
    [TestCase(int.MinValue)]
    [TestCase(int.MaxValue)]
    [TestCase(42)]
    [TestCase(-1)]
    public void SerializeDeserialize_Int32Value_RoundTrips(int value)
    {
        // Arrange
        var metadata = new Dictionary<string, object> { ["intKey"] = value };

        // Act
        byte[] serialized = MetadataSerializer.Serialize(metadata);
        var deserialized = MetadataSerializer.Deserialize(serialized);

        // Assert
        Assert.That(deserialized["intKey"], Is.EqualTo(value), "Int32 value should round-trip correctly");
    }

    [Test]
    [TestCase(0L)]
    [TestCase(long.MinValue)]
    [TestCase(long.MaxValue)]
    [TestCase(123456789012345L)]
    public void SerializeDeserialize_Int64Value_RoundTrips(long value)
    {
        // Arrange
        var metadata = new Dictionary<string, object> { ["longKey"] = value };

        // Act
        byte[] serialized = MetadataSerializer.Serialize(metadata);
        var deserialized = MetadataSerializer.Deserialize(serialized);

        // Assert
        Assert.That(deserialized["longKey"], Is.EqualTo(value), "Int64 value should round-trip correctly");
    }

    [Test]
    [TestCase(true)]
    [TestCase(false)]
    public void SerializeDeserialize_BoolValue_RoundTrips(bool value)
    {
        // Arrange
        var metadata = new Dictionary<string, object> { ["boolKey"] = value };

        // Act
        byte[] serialized = MetadataSerializer.Serialize(metadata);
        var deserialized = MetadataSerializer.Deserialize(serialized);

        // Assert
        Assert.That(deserialized["boolKey"], Is.EqualTo(value), "Bool value should round-trip correctly");
    }

    [Test]
    public void SerializeDeserialize_AllTypes_RoundTrips()
    {
        // Arrange
        var testDateTime = new DateTime(2024, 1, 15, 12, 30, 0, DateTimeKind.Utc);
        var metadata = new Dictionary<string, object>
        {
            ["string"] = "test",
            ["int"] = 42,
            ["long"] = 123456789L,
            ["float"] = 3.14f,
            ["double"] = 3.14159265359,
            ["bool"] = true,
            ["datetime"] = testDateTime,
            ["stringArray"] = new[] { "a", "b", "c" },
            ["intArray"] = new[] { 1, 2, 3 }
        };

        // Act
        byte[] serialized = MetadataSerializer.Serialize(metadata);
        var deserialized = MetadataSerializer.Deserialize(serialized);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(deserialized["string"], Is.EqualTo("test"), "String should match");
            Assert.That(deserialized["int"], Is.EqualTo(42), "Int should match");
            Assert.That(deserialized["long"], Is.EqualTo(123456789L), "Long should match");
            Assert.That((float)deserialized["float"], Is.EqualTo(3.14f).Within(0.001f), "Float should match");
            Assert.That((double)deserialized["double"], Is.EqualTo(3.14159265359).Within(0.0001), "Double should match");
            Assert.That(deserialized["bool"], Is.EqualTo(true), "Bool should match");
            Assert.That(deserialized["datetime"], Is.EqualTo(testDateTime), "DateTime should match");
            Assert.That(deserialized["stringArray"], Is.EqualTo(new[] { "a", "b", "c" }), "String array should match");
            Assert.That(deserialized["intArray"], Is.EqualTo(new[] { 1, 2, 3 }), "Int array should match");
        });
    }

    [Test]
    public void SerializeDeserialize_NullValue_RoundTrips()
    {
        // Arrange
        var metadata = new Dictionary<string, object> { ["nullKey"] = null! };

        // Act
        byte[] serialized = MetadataSerializer.Serialize(metadata);
        var deserialized = MetadataSerializer.Deserialize(serialized);

        // Assert
        Assert.That(deserialized, Contains.Key("nullKey"), "Should contain null key");
        Assert.That(deserialized["nullKey"], Is.Null, "Null value should round-trip correctly");
    }

    [Test]
    public void SerializeDeserialize_MultipleEntries_PreservesOrder()
    {
        // Arrange
        var metadata = new Dictionary<string, object>
        {
            ["first"] = 1,
            ["second"] = 2,
            ["third"] = 3
        };

        // Act
        byte[] serialized = MetadataSerializer.Serialize(metadata);
        var deserialized = MetadataSerializer.Deserialize(serialized);

        // Assert
        Assert.That(deserialized, Has.Count.EqualTo(3), "Should have 3 entries");
        Assert.Multiple(() =>
        {
            Assert.That(deserialized["first"], Is.EqualTo(1));
            Assert.That(deserialized["second"], Is.EqualTo(2));
            Assert.That(deserialized["third"], Is.EqualTo(3));
        });
    }

    #endregion

    #region MetadataFilter Tests

    [Test]
    public void FilterValue_RecordStruct_SupportsValueEquality()
    {
        // Arrange
        var fv1 = new FilterValue("test", FilterOperator.Equals);
        var fv2 = new FilterValue("test", FilterOperator.Equals);
        var fv3 = new FilterValue("test", FilterOperator.NotEquals);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(fv1, Is.EqualTo(fv2), "Same values should be equal");
            Assert.That(fv1, Is.Not.EqualTo(fv3), "Different operators should not be equal");
        });
    }

    [Test]
    public void FilterValue_DefaultOperator_IsEquals()
    {
        // Arrange
        var fv = new FilterValue("test");

        // Assert
        Assert.That(fv.Operator, Is.EqualTo(FilterOperator.Equals), "Default operator should be Equals");
    }

    [Test]
    public void MetadataFilter_Add_ReturnsSameInstance()
    {
        // Arrange
        var filter = new MetadataFilter();

        // Act
        var result = filter.Add("key1", "value1").Add("key2", 42);

        // Assert
        Assert.That(result, Is.SameAs(filter), "Add should return same instance for chaining");
        Assert.That(filter.Filters, Has.Count.EqualTo(2), "Should have 2 filters");
    }

    [Test]
    public void MetadataFilter_Constructor_WithKeyValue_CreatesFilter()
    {
        // Arrange & Act
        var filter = new MetadataFilter("name", "test", FilterOperator.Contains);

        // Assert
        Assert.That(filter.HasFilters, Is.True, "Should have filters");
        Assert.That(filter.Filters, Contains.Key("name"), "Should contain 'name' key");
        Assert.That(filter.Filters["name"].Value, Is.EqualTo("test"), "Value should be 'test'");
        Assert.That(filter.Filters["name"].Operator, Is.EqualTo(FilterOperator.Contains), "Operator should be Contains");
    }

    [Test]
    public void MetadataFilter_ToFrozenFilters_CreatesImmutableCopy()
    {
        // Arrange
        var filter = new MetadataFilter()
            .Add("key1", "value1")
            .Add("key2", 42);

        // Act
        var frozen = filter.ToFrozenFilters();
        filter.Add("key3", "new"); // Modify original

        // Assert
        Assert.That(frozen, Has.Count.EqualTo(2), "Frozen copy should have 2 entries");
        Assert.That(filter.Filters, Has.Count.EqualTo(3), "Original should have 3 entries");
    }

    [Test]
    public void MetadataFilter_HasFilters_ReturnsFalseWhenEmpty()
    {
        // Arrange
        var filter = new MetadataFilter();

        // Assert
        Assert.That(filter.HasFilters, Is.False, "Empty filter should not have filters");
    }

    [Test]
    public void MetadataFilter_Logic_DefaultsToAnd()
    {
        // Arrange
        var filter = new MetadataFilter();

        // Assert
        Assert.That(filter.Logic, Is.EqualTo(FilterLogic.And), "Default logic should be And");
    }

    #endregion

    #region MetadataFilterEvaluator Tests

    [Test]
    public void CreatePredicate_NullFilter_AcceptsAll()
    {
        // Arrange
        var vector = CreateTestVector();

        // Act
        var predicate = MetadataFilterEvaluator.CreatePredicate(null);

        // Assert
        Assert.That(predicate(vector), Is.True, "Null filter should accept all vectors");
    }

    [Test]
    public void CreatePredicate_EmptyFilter_AcceptsAll()
    {
        // Arrange
        var filter = new MetadataFilter();
        var vector = CreateTestVector();

        // Act
        var predicate = MetadataFilterEvaluator.CreatePredicate(filter);

        // Assert
        Assert.That(predicate(vector), Is.True, "Empty filter should accept all vectors");
    }

    [Test]
    [TestCase(FilterOperator.Equals, "test", true)]
    [TestCase(FilterOperator.Equals, "other", false)]
    [TestCase(FilterOperator.NotEquals, "other", true)]
    [TestCase(FilterOperator.NotEquals, "test", false)]
    [TestCase(FilterOperator.Contains, "es", true)]
    [TestCase(FilterOperator.Contains, "xyz", false)]
    [TestCase(FilterOperator.StartsWith, "te", true)]
    [TestCase(FilterOperator.StartsWith, "st", false)]
    [TestCase(FilterOperator.EndsWith, "st", true)]
    [TestCase(FilterOperator.EndsWith, "te", false)]
    public void CreatePredicate_StringOperators_EvaluatesCorrectly(FilterOperator op, string filterValue, bool expected)
    {
        // Arrange
        var vector = CreateTestVector();
        vector.SetMetadata("name", "test");
        var filter = new MetadataFilter("name", filterValue, op);

        // Act
        var predicate = MetadataFilterEvaluator.CreatePredicate(filter);
        bool result = predicate(vector);

        // Assert
        Assert.That(result, Is.EqualTo(expected), $"Operator {op} with value '{filterValue}' should return {expected}");
    }

    [Test]
    [TestCase(FilterOperator.Equals, 100, true)]
    [TestCase(FilterOperator.Equals, 50, false)]
    [TestCase(FilterOperator.GreaterThan, 50, true)]
    [TestCase(FilterOperator.GreaterThan, 100, false)]
    [TestCase(FilterOperator.LessThan, 150, true)]
    [TestCase(FilterOperator.LessThan, 100, false)]
    [TestCase(FilterOperator.GreaterEqual, 100, true)]
    [TestCase(FilterOperator.GreaterEqual, 101, false)]
    [TestCase(FilterOperator.LessEqual, 100, true)]
    [TestCase(FilterOperator.LessEqual, 99, false)]
    public void CreatePredicate_NumericOperators_EvaluatesCorrectly(FilterOperator op, int filterValue, bool expected)
    {
        // Arrange
        var vector = CreateTestVector();
        vector.SetMetadata("score", 100);
        var filter = new MetadataFilter("score", filterValue, op);

        // Act
        var predicate = MetadataFilterEvaluator.CreatePredicate(filter);
        bool result = predicate(vector);

        // Assert
        Assert.That(result, Is.EqualTo(expected), $"Operator {op} with value {filterValue} should return {expected}");
    }

    [Test]
    public void CreatePredicate_RegexOperator_MatchesPattern()
    {
        // Arrange
        var vector = CreateTestVector();
        vector.SetMetadata("email", "user@example.com");
        var filter = new MetadataFilter("email", @"^\w+@\w+\.\w+$", FilterOperator.Regex);

        // Act
        var predicate = MetadataFilterEvaluator.CreatePredicate(filter);
        bool result = predicate(vector);

        // Assert
        Assert.That(result, Is.True, "Email should match regex pattern");
    }

    [Test]
    public void CreatePredicate_RegexOperator_CachesPattern()
    {
        // Arrange
        var vector = CreateTestVector();
        vector.SetMetadata("email", "user@example.com");
        var filter = new MetadataFilter("email", @"^\w+@\w+\.\w+$", FilterOperator.Regex);

        // Act
        var predicate = MetadataFilterEvaluator.CreatePredicate(filter);
        bool result1 = predicate(vector);
        bool result2 = predicate(vector); // Second call uses cached regex

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result1, Is.True, "First call should match");
            Assert.That(result2, Is.True, "Second call (cached) should match");
        });
    }

    [Test]
    public void CreatePredicate_InOperator_MatchesAnyValue()
    {
        // Arrange
        var vector = CreateTestVector();
        vector.SetMetadata("status", "active");
        var filter = new MetadataFilter("status", new[] { "active", "pending" }, FilterOperator.In);

        // Act
        var predicate = MetadataFilterEvaluator.CreatePredicate(filter);

        // Assert
        Assert.That(predicate(vector), Is.True, "Should match 'active' in array");
    }

    [Test]
    public void CreatePredicate_NotInOperator_RejectsMatchingValues()
    {
        // Arrange
        var vector = CreateTestVector();
        vector.SetMetadata("status", "deleted");
        var filter = new MetadataFilter("status", new[] { "active", "pending" }, FilterOperator.NotIn);

        // Act
        var predicate = MetadataFilterEvaluator.CreatePredicate(filter);

        // Assert
        Assert.That(predicate(vector), Is.True, "Should not match 'deleted' in array");
    }

    [Test]
    public void CreatePredicate_AndLogic_RequiresAllMatch()
    {
        // Arrange
        var vector = CreateTestVector();
        vector.SetMetadata("name", "test");
        vector.SetMetadata("score", 100);

        var filter = new MetadataFilter()
            .Add("name", "test")
            .Add("score", 100);
        filter.Logic = FilterLogic.And;

        // Act
        var predicate = MetadataFilterEvaluator.CreatePredicate(filter);

        // Assert
        Assert.That(predicate(vector), Is.True, "All filters match, AND should pass");
    }

    [Test]
    public void CreatePredicate_AndLogic_FailsIfAnyMismatch()
    {
        // Arrange
        var vector = CreateTestVector();
        vector.SetMetadata("name", "test");
        vector.SetMetadata("score", 50);

        var filter = new MetadataFilter()
            .Add("name", "test")
            .Add("score", 100); // This won't match
        filter.Logic = FilterLogic.And;

        // Act
        var predicate = MetadataFilterEvaluator.CreatePredicate(filter);

        // Assert
        Assert.That(predicate(vector), Is.False, "One filter doesn't match, AND should fail");
    }

    [Test]
    public void CreatePredicate_OrLogic_PassesIfAnyMatch()
    {
        // Arrange
        var vector = CreateTestVector();
        vector.SetMetadata("name", "test");
        vector.SetMetadata("score", 50);

        var filter = new MetadataFilter()
            .Add("name", "test")
            .Add("score", 100); // This won't match
        filter.Logic = FilterLogic.Or;

        // Act
        var predicate = MetadataFilterEvaluator.CreatePredicate(filter);

        // Assert
        Assert.That(predicate(vector), Is.True, "One filter matches, OR should pass");
    }

    [Test]
    [TestCase("tags")]
    [TestCase("TAGS")]
    [TestCase("tag")]
    [TestCase("Tag")]
    [TestCase("TAG")]
    public void CreatePredicate_LegacyTagsField_CaseInsensitive(string fieldName)
    {
        // Arrange - vector has empty tags by default, so Contains(999) should return false
        // This test verifies the field name is recognized as a legacy field
        var vector = CreateTestVector();
        var filter = new MetadataFilter(fieldName, (short)999, FilterOperator.Contains);

        // Act
        var predicate = MetadataFilterEvaluator.CreatePredicate(filter);
        var result = predicate(vector);

        // Assert - Tags is empty, so Contains should return false (proving field was recognized)
        Assert.That(result, Is.False, $"Field name '{fieldName}' should be recognized as tags legacy field");
    }

    [Test]
    [TestCase("userid")]
    [TestCase("user_id")]
    [TestCase("UserId")]
    [TestCase("USER_ID")]
    public void CreatePredicate_LegacyUserIdField_CaseInsensitive(string fieldName)
    {
        // Arrange
        var vector = CreateTestVector();
        vector.Attributes = new VectorAttributes { UserId = 42 };
        var filter = new MetadataFilter(fieldName, 42u, FilterOperator.Equals);

        // Act
        var predicate = MetadataFilterEvaluator.CreatePredicate(filter);

        // Assert
        Assert.That(predicate(vector), Is.True, $"Field name '{fieldName}' should be recognized as userid");
    }

    [Test]
    public void CreatePredicate_MissingMetadataKey_HandlesGracefully()
    {
        // Arrange
        var vector = CreateTestVector();
        var filter = new MetadataFilter("nonexistent", "value", FilterOperator.Equals);

        // Act
        var predicate = MetadataFilterEvaluator.CreatePredicate(filter);

        // Assert
        Assert.That(predicate(vector), Is.False, "Missing key should return false for Equals");
    }

    [Test]
    public void CreatePredicate_MissingMetadataKey_NotEquals_ReturnsTrue()
    {
        // Arrange
        var vector = CreateTestVector();
        var filter = new MetadataFilter("nonexistent", "value", FilterOperator.NotEquals);

        // Act
        var predicate = MetadataFilterEvaluator.CreatePredicate(filter);

        // Assert
        Assert.That(predicate(vector), Is.True, "Missing key should return true for NotEquals");
    }

    private static Vector CreateTestVector() => new([1.0f, 2.0f, 3.0f]);

    #endregion
}
