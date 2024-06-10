using Parquet;
using Parquet.Schema;
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
        using ParquetReader reader = await ParquetReader.CreateAsync(fs, cancellationToken: cancellationToken).ConfigureAwait(false);
        // Iterate through the row groups in the file
        for (int i = 0; i < reader.RowGroupCount; i++)
        {
            using ParquetRowGroupReader groupReader = reader.OpenRowGroupReader(i);
            // Iterate through the columns in the row group
            foreach (DataField field in reader.Schema.GetDataFields())
            {
                // Check if the column is a float array (i.e. a Vector)
                if (field.ClrType == typeof(float))
                {
                    // Read float array
                    var data = await groupReader.ReadColumnAsync(field, cancellationToken: cancellationToken).ConfigureAwait(false);
                    var numValues = data.NumValues;
                    if (numValues > 0)
                    {
                        // Convert float array to Vector
                        if (data.Data is float[] d)
                        {
                            // Convert float array to Vector
                            var vector = new Vector(d);
                            vectors.Add(vector);
                        }
                    }
                }
            }
        }
    }

    private static VectorRecord ConvertToRecord(Vector vector) => new(vector.Id, vector.Values, vector.Tags, vector.OriginalText);

    private record class VectorRecord(Guid Id, float[] Values, short[] Tags, string? OriginalText);
}
