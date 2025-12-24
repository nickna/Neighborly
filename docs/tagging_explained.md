# Neighborly's Tagging System: Implementation and Efficient Lookups

## Introduction

Neighborly, a vector database system, implements a robust tagging system that allows for efficient organization and retrieval of vector data. This article explores how tags are implemented and used within Neighborly, as well as the mechanisms that enable efficient tag-based lookups and filtering.

## Tag Implementation

### The VectorTags Class

At the heart of Neighborly's tagging system is the `VectorTags` class. This class is responsible for managing tags associated with vectors in the database. Here are the key components:

1. **Tag Storage**: Tags are stored as `short` values, which allows for compact representation and efficient processing.

2. **Tag-to-Vector Mapping**: The `VectorTags` class maintains a dictionary (`_tagToVectorIds`) that maps each tag to a set of vector IDs. This structure enables quick lookups of vectors associated with specific tags.

3. **Vector-to-Tag Mapping**: Another dictionary (`_vectorIdToTags`) maps each vector ID to its associated tags, allowing for efficient retrieval of tags for a given vector.

### Integration with VectorList

The `VectorList` class, which is the primary container for vectors in Neighborly, integrates the `VectorTags` system:

```csharp
public class VectorList : IList<Vector>, IDisposable
{
    private readonly VectorTags _tags;
    public VectorTags Tags => _tags;

    public VectorList()
    {
        _tags = new VectorTags(this);
        _tags.Modified += (sender, e) => Modified?.Invoke(this, EventArgs.Empty);
    }
    // ...
}
```

This integration ensures that tag operations are tightly coupled with vector management, maintaining consistency and enabling efficient lookups.

## Using Tags in Neighborly

### Adding Tags to Vectors

When adding or updating vectors in the database, tags can be associated with them. The `Vector` class includes a `Tags` property of type `short[]`:

```csharp
public class Vector : IEquatable<Vector>
{
    public short[] Tags { get; }
    // ...
}
```

### Tag-Based Lookups

The `VectorTags` class provides methods for efficient tag-based lookups:

1. **GetVectorIdsByTag**: Retrieves all vector IDs associated with a specific tag.
2. **GetVectorIdsByTags**: Finds vector IDs that match all specified tags (intersection).
3. **GetVectorIdsByAnyTag**: Retrieves vector IDs that match any of the specified tags (union).

These methods leverage the `_tagToVectorIds` dictionary for quick lookups without needing to scan the entire vector collection.

## Efficient Tag-Based Filtering

### Building the Tag Map

The `VectorTags` class includes a `BuildMap` method that constructs the tag-to-vector and vector-to-tag mappings:

```csharp
public void BuildMap()
{
    _tagToVectorIds.Clear();
    _vectorIdToTags.Clear();

    foreach (var vector in _vectors)
    {
        foreach (var tag in vector.Tags)
        {
            AddTagToMap(tag, vector.Id);
        }
    }
}
```

This method is called when the database is loaded or when significant changes occur, ensuring that the tag mappings are up-to-date for efficient lookups.

### Optimized Filtering Operations

Neighborly's tagging system enables efficient filtering operations:

1. **Intersection Queries**: Finding vectors that match multiple tags is optimized by starting with the smallest tag set and intersecting with larger sets.

2. **Union Queries**: Retrieving vectors matching any of the specified tags is efficiently handled by combining the pre-computed sets of vector IDs for each tag.

3. **Exclusion Queries**: The system can efficiently exclude vectors with specific tags by leveraging the `_vectorIdToTags` mapping.

## Performance Considerations

1. **Memory Efficiency**: Using `short` values for tags and maintaining separate mappings allows for compact storage and quick access.

2. **Time Complexity**: Most tag-based lookup operations have O(1) average time complexity due to the use of hash-based data structures.

3. **Scalability**: The tagging system is designed to handle a large number of vectors and tags efficiently, with minimal impact on overall database performance.

## Conclusion

Neighborly's tagging system provides a powerful and efficient mechanism for organizing and retrieving vector data. By leveraging compact tag representation and optimized data structures, it enables fast tag-based lookups and filtering operations. This system enhances the overall functionality of Neighborly, making it a versatile solution for managing and querying large vector databases.
