using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neighborly.ETL
{
    /// <summary>
    /// ETL operation for importing and exporting Comma Separated Values (CSV).
    /// </summary>
    internal class Csv : IETL
    {
        public bool isDirectory { get; set; }
        public string fileExtension => ".csv";
        public VectorDatabase vectorDatabase { get; set; }

        public Task ExportDataAsync(string path)
        {
            throw new NotImplementedException();
        }

        public Task ImportDataAsync(string path)
        {
            throw new NotImplementedException();
        }
    }
}
    
