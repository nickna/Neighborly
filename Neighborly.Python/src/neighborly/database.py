import clr
import sys
import os
from pkg_resources import resource_filename

def load_neighborly_dll():
    dll_path = resource_filename(__name__, 'Neighborly.dll')
    print(f"Looking for Neighborly.dll at: {dll_path}")
    
    if not os.path.exists(dll_path):
        print(f"Neighborly.dll not found at {dll_path}")
        print(f"Current working directory: {os.getcwd()}")
        print(f"Contents of directory: {os.listdir(os.path.dirname(dll_path))}")
        raise FileNotFoundError(f"Neighborly.dll not found at {dll_path}")

    # Add the directory containing the DLL to the Python path
    dll_dir = os.path.dirname(dll_path)
    if dll_dir not in sys.path:
        sys.path.append(dll_dir)

    # Load the DLL
    try:
        clr.AddReference(dll_path)
    except Exception as e:
        raise ImportError(f"Failed to load Neighborly.dll: {str(e)}")

    # Import the classes from the DLL
    try:
        from Neighborly import VectorDatabase as ClrVectorDatabase, Vector as ClrVector
        return ClrVectorDatabase, ClrVector
    except ImportError:
        raise ImportError("Failed to import VectorDatabase and Vector from Neighborly.dll")

# Load the DLL and import the classes
ClrVectorDatabase, ClrVector = load_neighborly_dll()

class Vector:
    def __init__(self, values):
        self._vector = ClrVector(Array[Single](values))

    @property
    def values(self):
        return list(self._vector.Values)

    @property
    def id(self):
        return str(self._vector.Id)

class VectorDatabase:
    def __init__(self):
        self._db = ClrVectorDatabase()

    def add_vector(self, vector):
        if isinstance(vector, list):
            vector = Vector(vector)
        self._db.Vectors.Add(vector._vector)

    def search(self, query, k):
        if isinstance(query, list):
            query = Vector(query)
        results = self._db.Search(query._vector, k)
        return [Vector(list(result.Values)) for result in results]

    @property
    def count(self):
        return self._db.Count

    def save(self, path):
        self._db.SaveAsync(path).Wait()

    def load(self, path):
        self._db.LoadAsync(path).Wait()

# Ensure these are imported at the module level
from System import Array, Single