from setuptools import setup, find_packages
import os
import datetime

# Function to find all files in a directory
def find_files(directory, pattern):
    files = []
    for root, _, filenames in os.walk(directory):
        for filename in filenames:
            if filename.endswith(pattern):
                files.append(os.path.relpath(os.path.join(root, filename), directory))
    return files

# Generate a date-formatted version string
version = datetime.datetime.now().strftime("%Y.%m.%d")

# Specify the directory containing your DLLs
dll_directory = os.path.join('src', 'neighborly')

# Find all DLL files in the directory
dll_files = find_files(dll_directory, '.dll')

setup(
    name='Neighborly',
    version=version,
    packages=find_packages(),
    package_data={
        '': dll_files,
    },
    data_files=[
        (dll_directory, [os.path.join(dll_directory, f) for f in dll_files]),
    ],
)
