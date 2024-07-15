from setuptools import setup, find_packages

setup(
    name="neighborly_python",
    version="0.1.0",
    packages=find_packages(where="src"),
    package_dir={"": "src"},
    include_package_data=True,
    install_requires=[
        "pythonnet",
    ],
    package_data={
        "neighborly": ["Neighborly.dll"],
    },
)
