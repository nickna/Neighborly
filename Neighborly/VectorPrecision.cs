using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neighborly
{
    public enum VectorPrecision
    {
        Full,       // No compression, full 32-bit float precision
        Half,       // 16-bit (half-precision) float
        Quantized8, // 8-bit quantized integer
    }
}
