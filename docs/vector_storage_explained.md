# Neighborly Vector Representation and Storage

## 1. Vector Representation

In the Neighborly system, vectors are fundamental data structures used to represent high-dimensional data points. These vectors are typically used to encode information in a format suitable for various operations, including similarity searches and machine learning tasks.

### 1.1 Vector Components

A vector in Neighborly consists of:

- A unique identifier
- An array of float values representing the vector's coordinates
- Associated metadata (tags and original text)

### 1.2 Dimensionality

The dimensionality of a vector is determined by the length of its float array. This can vary depending on the specific use case or the embedding model used to generate the vector.

## 2. The Vector Class

The `Vector` class is the core data structure for representing vectors in Neighborly. Here's an overview of its key properties and methods:

### 2.1 Properties

- `Id` (Guid): A unique identifier for the vector.
- `Values` (float[]): The array of float values representing the vector's coordinates.
- `Tags` (short[]): An array of short values used for categorizing or grouping vectors.
- `OriginalText` (string): The original text associated with the vector, if applicable.
- `Dimension` (int): The number of dimensions in the vector (derived from the length of `Values`).

### 2.2 Key Methods

- Constructors: Multiple constructors for creating vectors with different input combinations.
- `Distance`: Calculates the distance between this vector and another vector.
- `ToBinary`: Converts the vector to a binary representation for storage or transmission.
- Arithmetic operations: Overloaded operators for addition, subtraction, and division.

### 2.3 Serialization

The `Vector` class includes methods for efficient serialization and deserialization:

- `ToBinary()`: Converts the vector to a byte array.
- Constructors that accept `byte[]` or `ReadOnlySpan<byte>`: Allow reconstruction of vectors from binary data.

## 3. Efficient Storage: MemoryMappedList

Neighborly uses a `MemoryMappedList` for efficient storage and retrieval of vectors, especially when dealing with large datasets that may not fit entirely in memory.

### 3.1 MemoryMappedList Structure

The `MemoryMappedList` consists of two main components:

1. Index File: Stores metadata about each vector (ID, offset, and length).
2. Data File: Stores the actual binary representation of vectors.

### 3.2 Key Features

- Memory-mapped files: Allows efficient access to large datasets without loading everything into memory.
- Disk-backed storage: Enables handling of datasets larger than available RAM.
- Fast random access: Provides quick retrieval of vectors by index or ID.

### 3.3 Operations

- `Add`: Appends a new vector to the list.
- `GetVector`: Retrieves a vector by index or ID.
- `Remove`: Marks a vector as deleted (using a tombstone).
- `Update`: Replaces an existing vector with a new version.

### 3.4 Optimizations

- Defragmentation: The `Defrag` method compacts the storage by removing tombstoned entries and reorganizing data.
- Batch operations: Methods like `DefragBatch` allow for incremental optimization of large datasets.

## 4. Compression

To further optimize storage, Neighborly implements compression techniques:

- GZip compression is used when saving the entire database to disk.
- The system is designed to potentially use more advanced compression methods like fpzip for floating-point data compression, although this is not fully implemented in the current version.

## Conclusion

Neighborly's vector representation and storage system is designed for efficiency and scalability. The `Vector` class provides a flexible structure for representing high-dimensional data, while the `MemoryMappedList` offers an efficient storage solution that can handle large datasets. This combination allows Neighborly to perform fast similarity searches and other vector operations on substantial amounts of data.
