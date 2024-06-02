using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neighborly.ETL
{
    internal enum ContentType
    {
        HDF5,       // Hierarchical Data Format version 5
        CSV,        // Comma Separated Values
        Parquet     // Apache Parquet
    }
}
