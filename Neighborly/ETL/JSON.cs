using System.Text.Json;

namespace Neighborly.ETL;

/// <summary>
/// ETL operation for importing and exporting JSON-encoded vectors
/// </summary>
public class JSON : EtlBase
{
    /// <inheritdoc />
    private protected override IStreamProvider StreamProvider => FileStreamProvider.Instance;

    /// <inheritdoc />
    public override string FileExtension => ".json";

    /// <inheritdoc />
    public override async Task ExportDataAsync(IEnumerable<Vector> vectors, string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vectors);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var stream = CreateWriteStream(path);
        await JsonSerializer.SerializeAsync(stream, vectors.Select(ConvertToRecord), cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task ExportDataAsync(IAsyncEnumerable<Vector> vectors, string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vectors);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var stream = CreateWriteStream(path);

        // Stream vectors one at a time for true streaming (requires manual JSON array writing)
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false });

        writer.WriteStartArray();
        await foreach (var vector in vectors.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            JsonSerializer.Serialize(writer, ConvertToRecord(vector));
        }
        writer.WriteEndArray();
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async Task ImportFileAsync(string path, ICollection<Vector> vectors, CancellationToken cancellationToken)
    {
        using var stream = CreateReadStream(path);
        await foreach (VectorRecord? record in JsonSerializer.DeserializeAsyncEnumerable<VectorRecord>(stream, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (record == null) continue; // Skip null entries
            Vector vector = new(record.Value.I, record.Value.V, record.Value.T ?? [], record.Value.O);
            vectors.Add(vector);
        }
    }

    private static VectorRecord ConvertToRecord(Vector vector) =>
        new(vector.Id, vector.Values, vector.Tags, vector.OriginalText);

    /// <summary>
    /// Compact JSON representation of Vector for efficient serialization.
    /// Property names are abbreviated to minimize JSON file size:
    /// I = Id, V = Values, T = Tags, O = OriginalText
    /// </summary>
    private readonly record struct VectorRecord(Guid I, float[] V, short[]? T, string? O);
}
