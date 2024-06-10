using System.ComponentModel;

namespace Neighborly.ETL;

public static class EtlFactory
{
    public static IETL CreateEtl(ContentType contentType)
    {
        if (!Enum.IsDefined(contentType))
        {
            throw new InvalidEnumArgumentException(nameof(contentType), (int)contentType, typeof(ETL.ContentType));
        }

        return contentType switch
        {
            ContentType.HDF5 => new HDF5(),
            ContentType.CSV => new Csv(),
            ContentType.Parquet => new Parquet(),
            ContentType.JSON => new JSON(),
            ContentType.JSONZ => new JSONZ(),
            _ => throw new NotSupportedException($"Content type {contentType} is not supported."),
        };
    }
}