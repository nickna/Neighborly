namespace Neighborly.ETL;

public enum ContentType
{
    HDF5,       // Hierarchical Data Format version 5
    CSV,        // Comma Separated Values
    Parquet,    // Apache Parquet
    JSON,        // JSON encoded vectors
    JSONZ       // JSON encoded vectors with GZip compression
}
