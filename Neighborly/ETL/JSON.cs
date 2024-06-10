using System.Text.Json;

namespace Neighborly.ETL;

public class JSON : EtlBase
{
    /// <inheritdoc />
    public override string FileExtension => ".json";

    /// <inheritdoc />
    public sealed override async Task ExportDataAsync(IEnumerable<Vector> vectors, string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vectors);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var stream = CreateWriteStream(path);
        await JsonSerializer.SerializeAsync(stream, vectors.Select(ConvertToRecord), cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected sealed override async Task ImportFileAsync(string path, ICollection<Vector> vectors, CancellationToken cancellationToken)
    {
        using var stream = CreateReadStream(path);
        await foreach (VectorRecord record in JsonSerializer.DeserializeAsyncEnumerable<VectorRecord>(stream, cancellationToken: cancellationToken))
        {
            Vector vector = new(record.I, record.V, record.T ?? [], record.O);
            vectors.Add(vector);
        }
    }

    private static VectorRecord ConvertToRecord(Vector vector) => new(vector.Id, vector.Values, vector.Tags, vector.OriginalText);

    private readonly record struct VectorRecord(Guid I, float[] V, short[] T, string? O);
}
