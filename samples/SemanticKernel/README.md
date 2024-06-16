## Dependencies

To run the Semantic Kernel sample, you will need the following dependencies:

- Docker: Make sure Docker is installed on your machine.

## Getting Started

1. Clone the Neighborly repository to your local machine.
2. Navigate to the `samples/SemanticKernel` directory.
3. Open the `Program.cs` file.

## Program Overview

The Semantic Kernel sample demonstrates how to use the `Neighborly.Adapters.SemanticKernel` library as a text memory for [Semantic Kernel](https://aka.ms/semantic-kernel) with [Ollama](https://ollama.com/) as the language model. It provides an end-to-end example of using the vector database with.

The program performs the following steps:

1. Imports the necessary namespaces and libraries.
2. Defines the model and embedding names.
3. Creates a custom Ollama image from the [Dockerfile](Dockerfile) using the `ImageFromDockerfileBuilder` class.
4. Starts a local Ollama instance using the `ContainerBuilder` class.
5. Initializes the OLLAMA services for chat completion, text generation, and text embedding generation.
6. Sets up the Neighborly vector database and memory store.
7. Builds the Semantic Kernel using the `Kernel.CreateBuilder()` method.
8. Registers the necessary services for the Semantic Kernel.
9. Builds the kernel.
10. Saves a text file to the memory using the memory plugin.
11. Rebuilds the search indexes.
12. Prompts a question to the kernel and retrieves the answer.

## How to run

To run the Semantic Kernel program, follow these steps:

1. Make sure Docker is running on your machine.
2. Build and run the program using your preferred C# development environment or the command line.
3. The program will build a custom Ollama container image with custom models. **This will take a while on the first start.**
4. The program will start the Ollama instance and perform the necessary setup.
5. It will import the text from `Ballad.txt` and `LOTR.txt` into the Neighborly vector database.
6. It will then prompt the LLM two predfined questions.
7. The program will retrieve the answer from the Semantic Kernel and display it.

That's it! You have successfully run the Semantic Kernel sample and used `Neighborly.Adapters.SemanticKernel` as the `ISemanticTextMemory` for SemanticKernel with Ollama.

# License

This project is licensed under the MIT License. See the [LICENSE](../../LICENSE.txt) file for details. The sample was adapted from the excellent [Atc.SemanticKernel.Console.Sample](https://github.com/atc-net/atc-semantic-kernel/blob/3adb10d5f5e3c099ce999bbbd5aa4a972073e7f6/sample/Atc.SemanticKernel.Console.Sample/Program.cs) Copyright (c) 2024 [atc-net](https://atc-net.github.io/), which is also licensed under the MIT License.

All ballads were made up by Bing Chat/GPT 4.
