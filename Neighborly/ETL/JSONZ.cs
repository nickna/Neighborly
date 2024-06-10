using System.IO.Compression;

namespace Neighborly.ETL;

public sealed class JSONZ : JSON
{
    /// <inheritdoc />
    public override string FileExtension => ".json.gz";

    /// <inheritdoc />
    protected override Stream CreateReadStream(string path) => new GZipStream(base.CreateReadStream(path), CompressionMode.Decompress);

    /// <inheritdoc />
    protected override Stream CreateWriteStream(string path) => new GZipStream(base.CreateWriteStream(path), CompressionLevel.Fastest);
}
