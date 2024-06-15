using Atc.SemanticKernel.Connectors.Ollama;
using Atc.SemanticKernel.Connectors.Ollama.ChatCompletion;
using Atc.SemanticKernel.Connectors.Ollama.Extensions;
using Atc.SemanticKernel.Connectors.Ollama.TextEmbeddingGeneration;
using Atc.SemanticKernel.Connectors.Ollama.TextGenerationService;
using NeighborlyMemory;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Plugins.Memory;
using DotNet.Testcontainers.Builders;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.TextGeneration;

const string modelName = "phi3";
const string embeddingModelName = "all-minilm";

// Start local ollama instance
var ollama = new ContainerBuilder()
    .WithImage("ollama/ollama:latest")
    .WithPortBinding(11434, true)
    .Build();

await ollama.StartAsync().ConfigureAwait(false);

Uri ollamaUri = new Uri($"http://localhost:{ollama.GetMappedPublicPort(11434)}");

await ollama.ExecAsync(["ollama", "pull", modelName]).ConfigureAwait(false);
await ollama.ExecAsync(["ollama", "pull", embeddingModelName]).ConfigureAwait(false);

OllamaChatCompletionService ollamaChat = new(ollamaUri, modelName);
OllamaTextGenerationService ollamaText = new(ollamaUri, modelName);
OllamaTextEmbeddingGenerationService ollamaEmbedding = new(ollamaUri, embeddingModelName);

#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable SKEXP0050 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
var memory = new MemoryBuilder()
    .WithMemoryStore(new NeighborlyMemoryStore(new Neighborly.VectorDatabase())) // Use NeighborlyMemoryStore
    .WithTextEmbeddingGeneration(ollamaEmbedding)
    .Build();
#pragma warning restore SKEXP0050 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

// semantic kernel builder
var builder = Kernel.CreateBuilder();
builder.Services.AddSingleton<IChatCompletionService>(ollamaChat);
builder.Services.AddSingleton<ITextGenerationService>(ollamaText);
#pragma warning disable SKEXP0001
builder.Services.AddSingleton<ITextEmbeddingGenerationService>(ollamaEmbedding);
#pragma warning restore SKEXP0001

var kernel = builder.Build();

#pragma warning disable SKEXP0050 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
var memoryPlugin = kernel.ImportPluginFromObject(new TextMemoryPlugin(memory));
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning restore SKEXP0050 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

/*Console.WriteLine("====================");
Console.WriteLine("CHAT COMPLETION DEMO");
Console.WriteLine("====================");
var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
var history = new ChatHistory();
history.AddSystemMessage("You are a useful assistant that replies with short messages.");
Console.WriteLine("Hint: type your question or type 'exit' to leave the conversation");
Console.WriteLine();

// Chat loop
while (true)
{
    Console.Write("You: ");
    var input = Console.ReadLine();

    if (string.IsNullOrEmpty(input) ||
        input.Equals("exit", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    history.AddUserMessage(input);
    var chatResponse = await chatCompletionService.GetChatMessageContentsAsync(history);

    Console.WriteLine(chatResponse[^1].Content);
    Console.WriteLine("---");
}*/

Console.WriteLine("====================");
Console.WriteLine("EMBEDDING DEMO");
Console.WriteLine("====================");

string[] texts = await File.ReadAllLinesAsync("Ballad.txt").ConfigureAwait(false);

#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable SKEXP0050 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
await kernel.InvokeAsync(memoryPlugin["Save"], new()
{
    [TextMemoryPlugin.InputParam] = texts,
    [TextMemoryPlugin.CollectionParam] = "ballads",
    [TextMemoryPlugin.KeyParam] = "info5",
});
#pragma warning restore SKEXP0050 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

const string RecallFunctionDefinition = @"
Consider only the facts below when answering questions:

BEGIN FACTS
About me: {{recall 'live in Seattle?'}}
About me: {{recall 'my family is from?'}}
END FACTS

Question: {{$input}}

Answer:
";
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable SKEXP0050 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
var result = await kernel.InvokePromptAsync(RecallFunctionDefinition, new(new OpenAIPromptExecutionSettings { MaxTokens = 1000 })
{
    [TextMemoryPlugin.InputParam] = "Where are my family from?",
    [TextMemoryPlugin.CollectionParam] = "ballads",
    [TextMemoryPlugin.LimitParam] = "2",
    [TextMemoryPlugin.RelevanceParam] = "0.79",
});
#pragma warning restore SKEXP0050 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

Console.WriteLine("Ask: Where are my family from?");
Console.WriteLine($"Answer: {result.GetValue<string>()}");

Console.ReadLine();
