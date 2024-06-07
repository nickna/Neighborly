using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Neighborly
{
    /// <summary>
    /// Represents a service that asynchronously monitors system memory usage and takes action to prevent excessive swap (virtual memory) usage.
    /// </summary>
    /// <remarks>
    /// The VectorMemoryService class monitors the system memory usage at regular intervals and compares it against configurable pressure thresholds.
    /// When the memory usage exceeds the defined thresholds, the service triggers memory optimization or management tasks to prevent excessive swap usage.
    /// The service supports Windows, Linux, and FreeBSD operating systems.
    /// This runs as part of VectorList and is not intended to be called directly.
    /// </remarks>
    /// <seealso cref="VectorDatabase"/>
    public class VectorMemoryService
    {
        private double _lowPressureThreshold = 0.6;  // 60% of physical memory
        /// <summary>
        /// Gets or sets the low memory pressure threshold as a percentage of physical memory.
        /// The threshold value ranges from 0.1 (10%) to 1.0 (100%).
        /// </summary>
        /// <remarks>
        /// When the memory usage exceeds the low pressure threshold, memory optimization tasks are triggered.
        /// The default value is 0.6 (60% of physical memory).
        /// The property can only be set when the service is not running.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when attempting to change the value while the service is running.</exception>
        public double LowPressureThreshold 
        {
            get { return _lowPressureThreshold; }
            set 
            {
                if (!_IsRunning) 
                { 
                    _lowPressureThreshold = value;
                    // Cannot be greater than the high pressure threshold
                    if (value > HighPressureThreshold) { _lowPressureThreshold = HighPressureThreshold; }
                    // Max value is 1 (100% of physical memory)
                    if (value > 1) { _lowPressureThreshold = 1; }
                    // Min value is 0.1 (10% of physical memory)
                    if (value < 0) { _lowPressureThreshold = 0.1; }
                }
                else { throw new InvalidOperationException("Cannot change LowPressureThreshold while the service is running"); }
            } 
        }

        private double _highPressureThreshold = 0.8; // 80% of physical memory
        /// <summary>
        /// Gets or sets the high memory pressure threshold as a percentage of physical memory.
        /// The threshold value ranges from 0.1 (10%) to 1.0 (100%).
        /// </summary>
        /// <remarks>
        /// When the memory usage exceeds the high pressure threshold, memory management tasks are triggered to prevent excessive swap usage.
        /// The default value is 0.8 (80% of physical memory).
        /// The property can only be set when the service is not running.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when attempting to change the value while the service is running.</exception>
        public double HighPressureThreshold
        {
            get { return _highPressureThreshold; }
            set
            {
                if (!_IsRunning)
                {
                    _highPressureThreshold = value;
                    // Cannot be less than the low pressure threshold
                    if (value < LowPressureThreshold) { _highPressureThreshold = LowPressureThreshold; }
                    // Max value is 1 (100% of physical memory)
                    if (value > 1) { _highPressureThreshold = 1; }
                    // Min value is 0.1 (10% of physical memory)
                    if (value < 0) { _highPressureThreshold = 0.1; }
                }
                else
                {
                    throw new InvalidOperationException("Cannot change HighPressureThreshold while the service is running");
                }
            }
        }

        public VectorDatabase VectorDatabase { get; set; }
        private TimeSpan _Frequency = TimeSpan.FromSeconds(10);
        private bool _IsRunning = false;

        public void Start()
        {
            if (!OperatingSystem.IsWindows() && 
                !OperatingSystem.IsLinux() && 
                !OperatingSystem.IsFreeBSD())
            {
                // Memory management functions are currently supported on Windows, Linux, and FreeBSD.
                return;
            }
            if (_IsRunning)
            {
                return;
            }
            if (VectorDatabase == null)
            {
                throw new InvalidOperationException("VectorDatabase is not set");
            }

            _IsRunning = true;
            var memMgrThread = new Thread(() =>
            {
                double memoryUsagePercentage = 0; 
                while (_IsRunning)
                {
                    memoryUsagePercentage = GetMemoryUsagePercentage();
                    // Console.WriteLine($"Memory Usage: {memoryUsagePercentage:P}");

                    if (memoryUsagePercentage >= HighPressureThreshold)
                    {
                        // Console.WriteLine("High memory pressure detected. Taking action...");
                        // TODO -- Need an algorithm that picks the least used Vectors and swaps them to disk
                        // Iterate through list of Vectors and call MoveToDisk() on the least used ones based on LastAcessDate
                        // This is a placeholder for now. Experimenting with different algorithms.
                    }
                    else if (memoryUsagePercentage >= LowPressureThreshold)
                    {
                        // Console.WriteLine("Moderate memory pressure detected. Optimizing memory...");
                        // Perform memory optimization tasks...
                        
                    }

                    Thread.Sleep(_Frequency); 
                }
            });
            memMgrThread.Priority = ThreadPriority.BelowNormal;
            memMgrThread.Start();
        }

        private static double GetMemoryUsagePercentage()
        {
            long physicalMemoryUsage = 0;
            long totalPhysicalMemory = 0;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows
                physicalMemoryUsage = Process.GetCurrentProcess().WorkingSet64;
                totalPhysicalMemory = GetTotalPhysicalMemory();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                     RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
            {
                // Linux and FreeBSD
                string statmContent = File.ReadAllText("/proc/self/statm");
                string[] statmValues = statmContent.Split(' ');
                physicalMemoryUsage = long.Parse(statmValues[1]) * Environment.SystemPageSize;
                totalPhysicalMemory = GetTotalPhysicalMemory();
            }

            return (double)physicalMemoryUsage / totalPhysicalMemory;
        }

        private static long GetTotalPhysicalMemory()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (GetPhysicallyInstalledSystemMemory(out long memoryInKilobytes))
                {
                    return memoryInKilobytes * 1024;
                }
                
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
            {
                return new MemoryInfo().TotalPhysical;
            }
            return 0;
        }


        #region Windows and Linux interop
        // Linux and FreeBSD interop
        [StructLayout(LayoutKind.Sequential)]
        struct MemoryInfo
        {
            public long TotalPhysical;

            public MemoryInfo()
            {
                TotalPhysical = sysconf(_SC_PHYS_PAGES) * sysconf(_SC_PAGESIZE);
            }

            [DllImport("libc")]
            static extern long sysconf(int name);

            const int _SC_PHYS_PAGES = 84;
            const int _SC_PAGESIZE = 30;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll")]
        static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [DllImport("kernel32.dll")]
        static extern bool GetPhysicallyInstalledSystemMemory(out long totalMemoryInKilobytes);

        #endregion
        public void Stop()
        {
            _IsRunning = false;
        }
    }
}
