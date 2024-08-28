using Neighborly.Search;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neighborly;

public partial class VectorDatabase : IDisposable
{

    /// <summary>
    /// Passes in details about how to generate embeddings.
    /// </summary>
    /// <seealso cref="EmbeddingGenerationInfo"/>
    public void SetEmbeddingGenerationInfo(EmbeddingGenerationInfo embeddingGeneratorInfo)
    {
        ArgumentNullException.ThrowIfNull(embeddingGeneratorInfo);
        _searchService.EmbeddingGenerator = new EmbeddingGenerator(embeddingGeneratorInfo);

    }

    /// <summary>
    /// Generates a Vector class from text.
    /// </summary>
    /// <param name="originalText"></param>
    /// <returns></returns>
    public Vector GenerateVector(string originalText)
    {
        float[] embedding = _searchService.EmbeddingGenerator.GenerateEmbedding(originalText);
        return new Vector(embedding, originalText);
    }
}
