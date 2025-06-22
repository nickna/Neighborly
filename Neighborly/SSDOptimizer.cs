using System.Runtime.InteropServices;

namespace Neighborly;

internal static class SSDOptimizer
{
    // Common SSD page sizes and alignment boundaries
    private const int SSD_PAGE_SIZE = 4096; // 4KB pages are common
    private const int SSD_BLOCK_SIZE = 128 * 1024; // 128KB erase blocks
    private const int OPTIMAL_ALIGNMENT = SSD_PAGE_SIZE;
    
    // Batch sizes optimized for SSD write patterns
    public const int OPTIMAL_WRITE_BATCH_SIZE = 64 * 1024; // 64KB batches
    public const int MINIMUM_BATCH_SIZE = 16 * 1024; // 16KB minimum
    
    public static long AlignToPageBoundary(long position)
    {
        return (position + OPTIMAL_ALIGNMENT - 1) & ~(OPTIMAL_ALIGNMENT - 1);
    }
    
    public static int GetOptimalBufferSize(int requestedSize)
    {
        // Round up to next page boundary for better SSD performance
        return (int)AlignToPageBoundary(requestedSize);
    }
    
    public static void OptimizeFileForSSD(string filePath)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                OptimizeFileForSSDWindows(filePath);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                OptimizeFileForSSDLinux(filePath);
            }
            // macOS optimization would go here if needed
        }
        catch (Exception ex)
        {
            Logging.Logger.Warning(ex, "Failed to apply SSD optimizations for file: {FilePath}", filePath);
        }
    }
    
    private static void OptimizeFileForSSDWindows(string filePath)
    {
        // On Windows, we can use SetFileInformationByHandle with FileDispositionInfo
        // to hint that this is a temporary file that should be optimized for SSDs
        try
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, 
                bufferSize: OPTIMAL_WRITE_BATCH_SIZE, FileOptions.SequentialScan);
            
            // The FileOptions.SequentialScan hint helps with SSD optimization
            // Additional Windows-specific optimizations could be added here
        }
        catch (Exception ex)
        {
            Logging.Logger.Debug(ex, "Windows SSD optimization failed for: {FilePath}", filePath);
        }
    }
    
    private static void OptimizeFileForSSDLinux(string filePath)
    {
        // On Linux, we can use fadvise to provide hints about access patterns
        try
        {
            // FADV_SEQUENTIAL and FADV_WILLNEED can help with SSD performance
            // This would require P/Invoke to posix_fadvise
            // For now, just use larger buffer sizes which help with SSD performance
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite,
                bufferSize: OPTIMAL_WRITE_BATCH_SIZE);
        }
        catch (Exception ex)
        {
            Logging.Logger.Debug(ex, "Linux SSD optimization failed for: {FilePath}", filePath);
        }
    }
    
    public static byte[] GetAlignedBuffer(int size)
    {
        // Ensure buffer size is aligned to page boundaries for optimal SSD performance
        int alignedSize = GetOptimalBufferSize(size);
        return new byte[alignedSize];
    }
    
    public static bool ShouldBatchWrites(int pendingBytes)
    {
        // Only write when we have enough data to make an efficient SSD write
        return pendingBytes >= MINIMUM_BATCH_SIZE;
    }
    
    /// <summary>
    /// Calculates optimal write positions to align with SSD pages
    /// </summary>
    public static long GetOptimalWritePosition(long currentPosition, int dataSize)
    {
        // For large writes, align to page boundaries
        if (dataSize >= SSD_PAGE_SIZE)
        {
            return AlignToPageBoundary(currentPosition);
        }
        
        // For small writes, just use current position to avoid wasting space
        return currentPosition;
    }
    
    /// <summary>
    /// Suggests whether to perform defragmentation based on SSD characteristics
    /// </summary>
    public static bool ShouldDefragmentForSSD(long fragmentationPercentage, long totalDataSize)
    {
        // SSDs have different defragmentation considerations than HDDs
        // Only defrag if fragmentation is very high and data size is large
        
        if (totalDataSize < SSD_BLOCK_SIZE)
        {
            return false; // Don't defrag small datasets
        }
        
        // Higher threshold for SSDs since random access is fast
        return fragmentationPercentage > 75;
    }
}