# Neighborly (Python Library)
## An Open-Source Vector Database
![image](https://github.com/user-attachments/assets/8fd15cce-5074-4f63-b132-380398d52330)


Neighborly is an open-source vector database designed to store and retrieve high-dimensional vector data efficiently. 

# Features
* Multithreaded and Thread-safe operations for CPython!
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
```python
from neighborly import *
db = VectorDatabase()
v = Vector([1, 2, 3])
db.add_vector(v)
```




