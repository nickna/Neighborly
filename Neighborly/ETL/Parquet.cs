using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Parquet;

namespace Neighborly.ETL
{
    /// <summary>
    /// ETL operation for importing and exporting Parquet files.
    /// </summary>
    internal class Parquet : IETL
    {
        public bool isDirectory { get; set; }
        public string fileExtension => ".parquet";
        public VectorDatabase vectorDatabase { get; set; }

        public Task ExportDataAsync(string path)
        {
            if (isDirectory)
            {
                var files = System.IO.Directory.GetFiles(path, "*" + fileExtension);
                foreach (var file in files)
                {
                    // Export the data
                }
            }
            throw new NotImplementedException();
        }

        public Task ImportDataAsync(string path)
        {
            var files = System.IO.Directory.GetFiles(path, "*" + fileExtension);
            foreach (var file in files)
            {
                // Import the data
                if (File.Exists(file))
                {
                    using (var stream = new FileStream(file, FileMode.Open))
                    {
                        using (var reader = new StreamReader(stream))
                        {
                            var parqueOptions = new ParquetOptions
                            {
                                TreatByteArrayAsString = false
                            };
                            // Pass the stream into ParquetReader
                            using (var parquetReader = ParquetReader.CreateAsync(stream,parqueOptions))
                            {
                                
                            }
                        }
                    }
                }
            }

            throw new NotImplementedException();
        }
    }
}
