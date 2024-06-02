using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neighborly.ETL
{
    /// <summary>
    /// ETL operation for importing and exporting Hierarchical Data Format version 5 (HDF5).
    /// </summary>
    internal class HDF5 : IETL
    {
        public bool isDirectory { get; set; }
        public string fileExtension => ".h5";
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
