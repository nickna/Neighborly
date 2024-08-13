using Microsoft.ML.Transforms.Text;
using Microsoft.ML;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ML.Data;
using System.Net.Http;
using System.Text.Json;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Neighborly;

public class EmbeddingGenerator
{
    private static readonly Lazy<EmbeddingGenerator> _instance = new Lazy<EmbeddingGenerator>(() => new EmbeddingGenerator());

    public static EmbeddingGenerator Instance => _instance.Value;

    private readonly ITransformer _model;
    private readonly MLContext _mlContext;
    private readonly EmbeddingGenerationInfo _embeddingFactoryInfo;
    private readonly HttpClient _httpClient;

    public EmbeddingGenerator(EmbeddingGenerationInfo embeddingFactoryInfo)
    {
        _embeddingFactoryInfo = embeddingFactoryInfo;
        switch (embeddingFactoryInfo.Source)
        {
            case EmbeddingSource.Internal:
                {
                    _mlContext = new MLContext();
                    var pipeline = _mlContext.Transforms.Text.NormalizeText("NormalizedText", "Text")
                        .Append(_mlContext.Transforms.Text.TokenizeIntoWords("Tokens", "NormalizedText"))
                        .Append(_mlContext.Transforms.Text.ApplyWordEmbedding("Features", "Tokens",
                            WordEmbeddingEstimator.PretrainedModelKind.SentimentSpecificWordEmbedding));

                    var emptyData = _mlContext.Data.LoadFromEnumerable(new List<InputData>());
                    _model = pipeline.Fit(emptyData);
                    break;
                }
            case EmbeddingSource.Ollama:
                {
                    _httpClient = new HttpClient();
                    break;
                }
            default:
                throw new ArgumentException("Invalid embedding source", nameof(embeddingFactoryInfo));
        }
    }

    public EmbeddingGenerator() : this(new EmbeddingGenerationInfo { Source = EmbeddingSource.Internal })
    {
    }

    public float[] GenerateEmbedding(string text)
    {
        switch (_embeddingFactoryInfo.Source)
        {
            case EmbeddingSource.Internal:
                return GenerateEmbeddingInternal(text);
            case EmbeddingSource.Ollama:
                return GenerateEmbeddingOllamaAsync(text).Result;
            default:
                throw new InvalidOperationException("Invalid embedding source");
        }
    }

    private async Task<float[]> GenerateEmbeddingOllamaAsync(string text)
    {
        if (string.IsNullOrEmpty(text))
            throw new ArgumentException("Input text cannot be null or empty", nameof(text));

        var request = new
        {
            _embeddingFactoryInfo.Model,
            prompt = text
        };
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(_embeddingFactoryInfo.Url, content);

        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync();
        Console.WriteLine(responseBody);

        var responseContent = await response.Content.ReadAsStringAsync();
        var embeddingResponse = JsonConvert.DeserializeObject<OllamaEmbeddingResponse>(responseContent);

        if (embeddingResponse?.Embedding == null || embeddingResponse.Embedding.Length == 0)
        {
            throw new InvalidOperationException("Failed to parse embedding from Ollama response");
        }

        return embeddingResponse.Embedding;
    }

    private float[] GenerateEmbeddingInternal(string text)
    {
        if (string.IsNullOrEmpty(text))
            throw new ArgumentException("Input text cannot be null or empty", nameof(text));

        // Create a single example of InputData
        var data = new List<InputData> { new InputData { Text = text } };

        // Convert the list to an IDataView
        var inputData = _mlContext.Data.LoadFromEnumerable(data);

        // Use the model to transform the data
        var transformedData = _model.Transform(inputData);
        return _mlContext.Data.CreateEnumerable<OutputData>(transformedData, reuseRowObject: false)
            .First()
            .Features;
    }
}

public class InputData
{
    public string Text { get; set; }
}

public class OutputData
{
    [VectorType(300)]
    public float[] Features { get; set; }
}

public enum EmbeddingSource
{
    Internal,
    Ollama
}

public struct EmbeddingGenerationInfo
{
    public EmbeddingSource Source { get; set; }

    private string? _url, _model;
    public string Url => string.IsNullOrEmpty(_url) ? "http://localhost:11434/api/embeddings" : _url;        
    public string Model => string.IsNullOrEmpty(_model) ? "llama3.1:latest" : _model;

}

public class OllamaEmbeddingResponse
{
    [JsonProperty("embedding")]
    public float[] Embedding { get; set; }
}

