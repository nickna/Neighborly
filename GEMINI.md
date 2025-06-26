This document provides instructions for Gemini, the AI coding assistant, to understand and interact with the Neighborly project.

## Project Overview

Neighborly is an open-source vector database written in C#. It can be deployed as a gRPC service in a Docker container or used as a lightweight library.

## Build and Test

This project uses the .NET CLI for building and testing.

### Build

To build the project, run the following command from the root directory:

```bash
dotnet build --configuration Release
```

### Test

To run the tests, use the following command from the root directory:

```bash
dotnet test --configuration Release --no-build --verbosity normal
```
