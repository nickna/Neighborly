namespace Neighborly.ETL;

public enum ContentType
{
    CSV,        // Comma Separated Values
    Parquet,    // Apache Parquet
    JSON,       // JSON encoded vectors
    JSONZ       // JSON encoded vectors with GZip compression
}
