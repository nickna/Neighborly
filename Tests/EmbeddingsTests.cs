using Neighborly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neighborly.Tests.Embeddings;

[TestFixture]
public class EmbeddingsTests
{
    [Test]
    public void GenerateMLNet_Embedding()
    {
        // Arrange
        EmbeddingGenerator embeddingFactory = new EmbeddingGenerator();

        // Act
        float[] embedding = embeddingFactory.GenerateEmbedding("Hello, World!");

        // Assert
        Assert.That(embedding.Length, Is.GreaterThan(100)); // Array of float[] > 100        
    }

    [Test]
    public void GenerateMLNet_EmbeddingsAreDifferent()
    {
        // Arrange
        EmbeddingGenerator embeddingFactory = new EmbeddingGenerator();

        // Act
        float[] embedding1 = embeddingFactory.GenerateEmbedding("Hello, Earth!");
        float[] embedding2 = embeddingFactory.GenerateEmbedding("Hello, Mars!");

        // Assert
        Assert.That(embedding1, Is.Not.EqualTo(embedding2)); // Different embeddings
    }

    [Test]
    [Ignore("Requires Ollama running locally. Not suitable for automated builds.")]
    public void GenerateOllama_Embedding()
    {
        // Arrange
        EmbeddingGenerator embeddingFactory = new 
            EmbeddingGenerator(new EmbeddingGenerationInfo { Source = EmbeddingSource.Ollama });
        // Default Llama 3.1 model

        // Act
        float[] embedding = embeddingFactory.GenerateEmbedding("Hello, World!");

        // Assert
        Assert.That(embedding.Length, Is.EqualTo(4096)); // 4096 for Meta Llama; change dimensions for other models
    }

    [Test]
    [Ignore("Requires Ollama running locally. Not suitable for automated builds.")]
    public void GenerateOllama_EmbeddingsAreDifferent()
    {
        // Arrange
        EmbeddingGenerator embeddingFactory = new
            EmbeddingGenerator(new EmbeddingGenerationInfo { Source = EmbeddingSource.Ollama });

        // Act
        float[] embedding1 = embeddingFactory.GenerateEmbedding("Hello, Earth!");
        float[] embedding2 = embeddingFactory.GenerateEmbedding("Hello, Mars!");

        // Assert
        Assert.That(embedding1, Is.Not.EqualTo(embedding2)); // Different embeddings
    }
}
