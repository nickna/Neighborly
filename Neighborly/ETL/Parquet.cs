using Parquet;
using Parquet.Serialization;

namespace Neighborly.ETL;

/// <summary>
/// ETL operation for importing and exporting Parquet files.
/// </summary>
public sealed class Parquet : EtlBase
{
    /// <inheritdoc />
    public override string FileExtension => ".parquet";

    /// <inheritdoc />
    public override async Task ExportDataAsync(IEnumerable<Vector> vectors, string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vectors);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var fs = CreateWriteStream(path);
        await ParquetSerializer.SerializeAsync(vectors.Select(ConvertToRecord), fs, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async Task ImportFileAsync(string path, ICollection<Vector> vectors, CancellationToken cancellationToken)
    {
        using var fs = CreateReadStream(path);
        var records = await ParquetSerializer.DeserializeAsync<VectorRecord>(fs, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var vector = new Vector(
                id: record.Id,
                values: record.Values,
                tags: record.Tags ?? [],
                originalText: record.OriginalText
            );
            vectors.Add(vector);
        }
    }

    private static VectorRecord ConvertToRecord(Vector vector) => new(vector.Id, vector.Values, vector.Tags, vector.OriginalText);

    private class VectorRecord
    {
        public Guid Id { get; set; }
        public float[] Values { get; set; } = [];
        public short[] Tags { get; set; } = [];
        public string? OriginalText { get; set; }
        
        public VectorRecord() { }
        
        public VectorRecord(Guid id, float[] values, short[] tags, string? originalText)
        {
            Id = id;
            Values = values;
            Tags = tags;
            OriginalText = originalText;
        }
    }
}
