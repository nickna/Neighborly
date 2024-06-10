using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using CsvHelper.TypeConversion;

namespace Neighborly.ETL;

/// <summary>
/// ETL operation for importing and exporting Comma Separated Values (CSV).
/// </summary>
public sealed class Csv : EtlBase
{
    private static readonly CsvConfiguration s_configuration = new(CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = true,
        Delimiter = ";",
        IgnoreBlankLines = true,
        IgnoreReferences = true,
        TrimOptions = TrimOptions.Trim,
        Encoding = Encoding.UTF8
    };

    /// <inheritdoc />
    public override string FileExtension => ".csv";

    /// <inheritdoc />
    public async override Task ExportDataAsync(IEnumerable<Vector> vectors, string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vectors);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var stream = CreateWriteStream(path);
        using var textWriter = new StreamWriter(stream, Encoding.UTF8);
        using var writer = new CsvWriter(textWriter, s_configuration);
        writer.Context.RegisterClassMap<VectorRecordMap>();
        await writer.WriteRecordsAsync(vectors.Select(ConvertToRecord), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected async override Task ImportFileAsync(string path, ICollection<Vector> vectors, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(vectors);

        using var stream = CreateReadStream(path);
        using var textReader = new StreamReader(stream, Encoding.UTF8);
        using var reader = new CsvReader(textReader, s_configuration);
        reader.Context.RegisterClassMap<VectorRecordMap>();
        await foreach (var record in reader.EnumerateRecordsAsync(new VectorRecord(Guid.Empty, [], [], string.Empty), cancellationToken).ConfigureAwait(false))
        {
            vectors.Add(new Vector(record.Id, record.Values, record.Tags ?? [], record.OriginalText ?? string.Empty));
        }
    }

    private static VectorRecord ConvertToRecord(Vector vector) => new(vector.Id, vector.Values, vector.Tags, vector.OriginalText);

    private record class VectorRecord(Guid Id, [property: TypeConverter(typeof(ArrayConverter))] float[] Values, [property: TypeConverter(typeof(ArrayConverter))] short[] Tags, string? OriginalText);

    private class VectorRecordMap : ClassMap<VectorRecord>
    {
        public VectorRecordMap()
        {
            Map(v => v.Id).Name(nameof(VectorRecord.Id));
            Map(v => v.Values).Name(nameof(VectorRecord.Values)).TypeConverter<ArrayConverter<float>>();
            Map(v => v.Tags).Name(nameof(VectorRecord.Tags)).TypeConverter<ArrayConverter<short>>();
            Map(v => v.OriginalText).Name(nameof(VectorRecord.OriginalText));
        }

        private class ArrayConverter<T> : ArrayConverter
        {
            private const char s_separator = ',';

            public override object? ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
                => text?.Split(s_separator)
                .Where(static s => !string.IsNullOrWhiteSpace(s))
                .Select(static s => (T?)System.ComponentModel.TypeDescriptor.GetConverter(typeof(T)).ConvertFromString(s))
                .Where(static v => v != null)
                .ToArray();

            public override string ConvertToString(object value, IWriterRow row, MemberMapData memberMapData)
                => string.Join(s_separator, ((IEnumerable<T>)value).Select(static v => v?.ToString()));
        }
    }
}

