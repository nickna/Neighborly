using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neighborly
{
    public partial class Vector
    {

        public static byte[] Compress(VectorPrecision precision, float[] data)
        {
            switch (precision)
            {
                case VectorPrecision.Full:
                    return data.SelectMany(BitConverter.GetBytes).ToArray();
                case VectorPrecision.Half:
                    return data.SelectMany(f => BitConverter.GetBytes(HalfPrecisionConvert.FloatToHalf(f))).ToArray();
                case VectorPrecision.Quantized8:
                    return data.Select(f => (byte)((f + 1) * 127.5f)).ToArray();
                default:
                    throw new InvalidOperationException("Unknown precision");
            }
        }

        public static float[] Decompress(VectorPrecision precision, byte[] compressedData)
        {
            switch (precision)
            {
                case VectorPrecision.Full:
                    return Enumerable.Range(0, compressedData.Length / sizeof(float))
                        .Select(i => BitConverter.ToSingle(compressedData, i * sizeof(float)))
                        .ToArray();
                case VectorPrecision.Half:
                    return Enumerable.Range(0, compressedData.Length / sizeof(ushort))
                        .Select(i => HalfPrecisionConvert.HalfToFloat(BitConverter.ToUInt16(compressedData, i * sizeof(ushort))))
                        .ToArray();
                case VectorPrecision.Quantized8:
                    return compressedData.Select(b => (b / 127.5f) - 1).ToArray();
                default:
                    throw new InvalidOperationException("Unknown precision");
            }
        }


        // Add this helper class for half-precision conversion
        private static class HalfPrecisionConvert
        {
            public static ushort FloatToHalf(float value)
            {
                int bits = BitConverter.SingleToInt32Bits(value);
                int sign = bits >> 31;
                int exp = (bits >> 23) & 0xFF;
                int mantissa = bits & 0x7FFFFF;

                if (exp == 0xFF)
                {
                    // NaN or Infinity
                    exp = 0x1F;
                    mantissa = (mantissa != 0) ? 0x200 : 0;
                }
                else if (exp > 0x8E)
                {
                    // Overflow - flush to Infinity
                    exp = 0x1F;
                    mantissa = 0;
                }
                else if (exp < 0x71)
                {
                    // Underflow - flush to zero
                    exp = 0;
                    mantissa = 0;
                }
                else
                {
                    exp = exp - 0x70;
                    mantissa = mantissa >> 13;
                }

                return (ushort)((sign << 15) | (exp << 10) | mantissa);
            }

            public static float HalfToFloat(ushort value)
            {
                int sign = (value >> 15) & 0x1;
                int exp = (value >> 10) & 0x1F;
                int mantissa = value & 0x3FF;

                if (exp == 0)
                {
                    if (mantissa == 0) return sign == 0 ? 0f : -0f;
                    return (sign == 0 ? 1f : -1f) * mantissa * MathF.Pow(2, -24);
                }
                else if (exp == 31)
                {
                    if (mantissa == 0) return sign == 0 ? float.PositiveInfinity : float.NegativeInfinity;
                    return float.NaN;
                }

                return (sign == 0 ? 1f : -1f) * MathF.Pow(2, exp - 15) * (1 + mantissa / 1024f);
            }
        }
    }
}

