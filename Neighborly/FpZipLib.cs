using System;
using System.Reflection;
using System.Runtime.InteropServices;

/// <summary>
/// Bindings for the fpzip, a library for lossless compression of floating-point data.
/// Source: https://github.com/llnl/fpzip
/// </summary>
public static class FpZipLib
{
    private static readonly Dictionary<OSPlatform, Dictionary<Architecture, string>> SupportedPlatforms =
        new Dictionary<OSPlatform, Dictionary<Architecture, string>>
        {
            { OSPlatform.Windows, new Dictionary<Architecture, string>
                {
                    { Architecture.X64, "fpzip.dll" },
                    { Architecture.Arm, "fpzip.dll" }
                }
            },
            { OSPlatform.Linux, new Dictionary<Architecture, string>
                {
                    { Architecture.X64, "libfpzip.so" },
                    { Architecture.Arm, "libfpzip.so" }
                }
            },
            { OSPlatform.OSX, new Dictionary<Architecture, string>
                {
                    { Architecture.X64, "libfpzip.dylib" }
                }
            },
            { OSPlatform.Create("ANDROID"), new Dictionary<Architecture, string>
                {
                    { Architecture.X64, "libfpzip.so" }
                }
            }
        };

    static FpZipLib()
    {
        NativeLibrary.SetDllImportResolver(typeof(FpZipLib).Assembly, ImportResolver);
    }

    private static IntPtr ImportResolver(string libraryName, System.Reflection.Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != "fpzip")
            return IntPtr.Zero;

        foreach (var platform in SupportedPlatforms)
        {
            if (RuntimeInformation.IsOSPlatform(platform.Key))
            {
                if (platform.Value.TryGetValue(RuntimeInformation.ProcessArchitecture, out string libName))
                {
                    // Search path is fpzip_runtimes/{os}/{cpu}/
                    var path = $"./fpzip_runtimes/{platform.Key}/{RuntimeInformation.ProcessArchitecture}/{libName}";
                    return NativeLibrary.Load(path, assembly, searchPath);

                    //return NativeLibrary.Load(libName, assembly, searchPath);
                }
                break;
            }
        }

        throw new PlatformNotSupportedException($"Unsupported platform: OS={RuntimeInformation.OSDescription}, Arch={RuntimeInformation.ProcessArchitecture}");
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FPZ
    {
        public int type;
        public int prec;
        public int nx;
        public int ny;
        public int nz;
        public int nf;
    }

    [DllImport("fpzip", CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr fpzip_write_to_buffer(IntPtr buffer, IntPtr size);

    [DllImport("fpzip", CallingConvention = CallingConvention.Cdecl)]
    public static extern int fpzip_write_header(IntPtr fpz);

    [DllImport("fpzip", CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr fpzip_write(IntPtr fpz, IntPtr data);

    [DllImport("fpzip", CallingConvention = CallingConvention.Cdecl)]
    public static extern void fpzip_write_close(IntPtr fpz);

    [DllImport("fpzip", CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr fpzip_read_from_buffer(IntPtr buffer);

    [DllImport("fpzip", CallingConvention = CallingConvention.Cdecl)]
    public static extern int fpzip_read_header(IntPtr fpz);

    [DllImport("fpzip", CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr fpzip_read(IntPtr fpz, IntPtr data);

    [DllImport("fpzip", CallingConvention = CallingConvention.Cdecl)]
    public static extern void fpzip_read_close(IntPtr fpz);

    [DllImport("fpzip", CallingConvention = CallingConvention.Cdecl)]
    public static extern int fpzip_errno();
}
