using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neighborly.ETL
{
    /// <summary>
    /// VectorDatabase Interface for Extract Transform and Load (ETL) operations for importing and exporting Vector data.
    /// </summary>
    internal interface IETL
    {
        /// <summary>
        /// Indicates if the ETL operation should be performed on a directory or a file.
        /// </summary>
        bool isDirectory { get; set; }
        string fileExtension { get; }
        VectorDatabase vectorDatabase{ get; set; }
        Task ImportDataAsync(string path);
        Task ExportDataAsync(string path);
    }
}
