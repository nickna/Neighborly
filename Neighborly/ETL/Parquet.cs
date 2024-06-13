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
        using ParquetReader reader = await ParquetReader.CreateAsync(fs, cancellationToken: cancellationToken).ConfigureAwait(false);

        var table = await reader.ReadAsTableAsync();
        if (table is null)
        {
            return;
        }

        int idFieldIndex = -1;
        int tagsFieldIndex = -1;
        int originalTextFieldIndex = -1;
        int valuesFieldIndex = -1;
        var dataFields = reader.Schema.GetDataFields();
        for (var i = 0; i < dataFields.Length; i++)
        {
            var currentField = dataFields[i];
            if (currentField.ClrType == typeof(Guid))
            {
                idFieldIndex = i;
            }
            else if (currentField.ClrType == typeof(short[]))
            {
                tagsFieldIndex = i;
            }
            else if (currentField.ClrType == typeof(string) && currentField.Name == "OriginalText")
            {
                originalTextFieldIndex = i;
            }
            else if (currentField.ClrType == typeof(float))
            {
                valuesFieldIndex = i;
            }
        }

        if (valuesFieldIndex == -1)
        {
            return;
        }

        foreach (var row in table)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var idCell = idFieldIndex >= 0 ? row[idFieldIndex] : null;
            var tagsCell = tagsFieldIndex >= 0 ? row[tagsFieldIndex] : null;
            var originalTextCell = originalTextFieldIndex >= 0 ? row[originalTextFieldIndex] : null;

            var valuesCell = row[valuesFieldIndex];
            if (valuesCell is object[] valuesCellData)
            {
                var vector = new Vector(
                    id: idCell is null ? Guid.NewGuid() : (Guid)idCell,
                    values: valuesCellData.OfType<float>().ToArray(),
                    tags: tagsCell is null ? [] : ((short[])tagsCell),
                    originalText: originalTextCell as string
                );
                vectors.Add(vector);
            }
        }
    }

    private static VectorRecord ConvertToRecord(Vector vector) => new(vector.Id, vector.Values, vector.Tags, vector.OriginalText);

    private record class VectorRecord(Guid Id, float[] Values, short[] Tags, string? OriginalText);
}
