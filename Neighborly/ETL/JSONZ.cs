namespace Neighborly.ETL;

/// <summary>
/// ETL operation for importing and exporting GZip-compressed JSON vectors
/// </summary>
public sealed class JSONZ : JSON
{
    private static readonly GZipStreamProvider s_streamProvider =
        new(FileStreamProvider.Instance);

    /// <inheritdoc />
    private protected override IStreamProvider StreamProvider => s_streamProvider;

    /// <inheritdoc />
    public override string FileExtension => ".json.gz";
}
