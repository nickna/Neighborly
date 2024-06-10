using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neighborly.ETL;

/// <summary>
/// ETL operation for importing and exporting Hierarchical Data Format version 5 (HDF5).
/// </summary>
public sealed class HDF5 : EtlBase
{
    /// <inheritdoc />
    public override string FileExtension => ".h5";

    /// <inheritdoc />
    public override Task ExportDataAsync(IEnumerable<Vector> vectors, string path, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    protected override Task ImportFileAsync(string path, ICollection<Vector> vectors, CancellationToken cancellationToken= default)
    {
        throw new NotImplementedException();
    }
}
