# Neighborly
## An Open-Source Vector database

![neighborly-header](https://github.com/nickna/Neighborly/assets/4017153/2dd8a22d-511d-4457-bde5-ac4ceaecf166)

Neighborly is an open-source vector database that efficiently stores and retrieves vector data. Built with C#, it provides functionality for handling high-dimensional vectors, making it ideal for machine learning, data science applications, and more.

# Features
* Disk-backed Storage: Efficiently store large volumes of vector data with disk-backed lists.
* High Performance: Optimized for fast read and write operations.
* Unit Testing: Comprehensive test suite to ensure reliability and stability.

# Code Overview
## DiskBackedList
This class implements a list backed by disk storage, allowing for efficient handling of large data sets that exceed memory capacity. It provides methods for adding, retrieving, and managing vectors stored on disk.

## VectorDatabase
The core class of Neighborly, VectorDatabase manages the vector data. It includes methods for inserting, updating, deleting, and querying vectors. The class ensures high performance through optimized indexing and retrieval algorithms.

## Search Algorithms
Neighborly uses advanced search algorithms to query vector data efficiently:

* k-Nearest Neighbors (k-NN): Finds the k closest vectors to a given query vector based on a specified distance metric (e.g., Euclidean distance).
* Approximate Nearest Neighbor (ANN): Uses techniques like locality-sensitive hashing (LSH) to quickly find approximate nearest neighbors, providing a balance between speed and accuracy.
* Range Search: Retrieves all vectors within a specified distance from a query vector.
* Cosine Similarity Search: This method finds vectors that have the highest cosine similarity to the query vector. It is useful for applications involving text and other high-dimensional data.

## Usage
To use Neighborly in your project, add a reference to the compiled DLL and utilize the provided classes and methods for managing vector data.

## Contributing
We welcome contributions! If you have ideas for new features or have found bugs, please open an issue or submit a pull request. For major changes, please discuss them in an issue first.

## License
This project is licensed under the MIT License. See the LICENSE file for details.

## Contact
For any questions or further assistance, feel free to contact [![GitHub](https://img.shields.io/badge/GitHub-nickna-blue)](https://github.com/nickna)
.
