# Neighborly (Python Library)
## An Open-Source Vector Database
![image](https://github.com/user-attachments/assets/8fd15cce-5074-4f63-b132-380398d52330)


Neighborly is an open-source vector database designed to store and retrieve high-dimensional vector data efficiently. 

# Features
* Thread-safe operations for multi-threaded CPython!
* Disk-backed Storage: Efficiently handle large volumes of vector data with memory caching and disk storage for persistence. 
* High Performance: Optimized for fast read and write operations.
* Cross-Platform Compatibility: The backing library is cross-platform compatible and will go anywhere Python does
* Advanced Search Algorithms: Utilize k-NN, ANN, range search, and cosine similarity search for efficient vector queries.

# Installation
1. Download [.NET 8](https://dotnet.microsoft.com/en-us/download)
```shell
# macOS 
brew install dotnet

# Linux
sudo apt-get install -y dotnet-runtime-8.0

# Windows WinGet
winget install Microsoft.DotNet.SDK.8

# Windows GUI
Go to https://dot.net
```

2. Install the Python Wheel from our [latest releases](https://github.com/nickna/Neighborly/releases) (Pypi distribution coming soon)
```shell
pip install /path/to/neighborly.whl
```

# Examples
## Usage

Here's a quick example of how to use Neighborly:

```python
from neighborly import VectorDatabase, Vector

# Create a new vector database
db = VectorDatabase()

# Create and add vectors
vector1 = Vector([1.0, 2.0, 3.0])
vector2 = Vector([4.0, 5.0, 6.0])
db.add_vector(vector1)
db.add_vector(vector2)

# Perform a similarity search
query = Vector([1.1, 2.1, 3.1])
results = db.search(query, k=1)
print(f"Nearest vector: {results[0].values}")

# Use vector tags
tags = create_vector_tags([1, 2, 3])
print(f"Tags: {tags.to_list()}")

# Save and load the database
db.save("my_database_file")
db.load("my_database_file")
```
