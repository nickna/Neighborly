using Microsoft.ML.Transforms.Text;
using Microsoft.ML;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ML.Data;

namespace Neighborly;

public class EmbeddingFactory
{
    private static readonly Lazy<EmbeddingFactory> _instance = new Lazy<EmbeddingFactory>(() => new EmbeddingFactory());

    public static EmbeddingFactory Instance => _instance.Value;

    private readonly ITransformer _model;
    private readonly MLContext _mlContext;

    public EmbeddingFactory()
    {
        _mlContext = new MLContext();
        var pipeline = _mlContext.Transforms.Text.NormalizeText("NormalizedText", "Text")
            .Append(_mlContext.Transforms.Text.TokenizeIntoWords("Tokens", "NormalizedText"))
            .Append(_mlContext.Transforms.Text.ApplyWordEmbedding("Features", "Tokens",
                WordEmbeddingEstimator.PretrainedModelKind.SentimentSpecificWordEmbedding));

        var emptyData = _mlContext.Data.LoadFromEnumerable(new List<InputData>());
        _model = pipeline.Fit(emptyData);
    }

    public float[] GenerateEmbedding(string text)
    {
        if (string.IsNullOrEmpty(text))
            throw new ArgumentException("Input text cannot be null or empty", nameof(text));

        var inputData = new List<InputData> { new InputData { Text = text } };
        var dataView = _mlContext.Data.LoadFromEnumerable(inputData);
        var transformedData = _model.Transform(dataView);
        return _mlContext.Data.CreateEnumerable<OutputData>(transformedData, reuseRowObject: false)
            .First()
            .Features;
    }

    private class InputData
    {
        public string Text { get; set; }
    }

    private class OutputData
    {
        [VectorType]
        public float[] Features { get; set; }
    }

}
