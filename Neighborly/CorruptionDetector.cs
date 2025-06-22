namespace Neighborly;

internal static class CorruptionDetector
{
    public static bool ValidateIndexFile(MemoryMappedFileHolder indexFile, long expectedCount)
    {
        try
        {
            indexFile.Stream.Seek(indexFile.GetDataStartPosition(), SeekOrigin.Begin);
            
            long validEntries = 0;
            long position = indexFile.GetDataStartPosition();
            
            Span<byte> entry = stackalloc byte[MemoryMappedList.IndexEntryByteLength];
            while (position + MemoryMappedList.IndexEntryByteLength <= indexFile.Stream.Length)
            {
                indexFile.Stream.ReadExactly(entry);
                
                Guid id = new(entry[..MemoryMappedList.IdBytesLength]);
                if (id.Equals(Guid.Empty))
                    break;
                    
                if (!id.Equals(MemoryMappedList.TombStone))
                {
                    validEntries++;
                }
                
                position += MemoryMappedList.IndexEntryByteLength;
            }
            
            return validEntries <= expectedCount;
        }
        catch (Exception ex)
        {
            Logging.Logger.Error(ex, "Failed to validate index file: {FileName}", indexFile.Filename);
            return false;
        }
    }
    
    public static bool ValidateDataFile(MemoryMappedFileHolder dataFile)
    {
        try
        {
            // Basic sanity checks
            if (dataFile.Stream.Length < dataFile.GetDataStartPosition())
                return false;
                
            // Could add more sophisticated checks here like:
            // - Validate vector data structure integrity
            // - Check for reasonable data sizes
            // - Verify checksums if implemented
            
            return true;
        }
        catch (Exception ex)
        {
            Logging.Logger.Error(ex, "Failed to validate data file: {FileName}", dataFile.Filename);
            return false;
        }
    }
    
    public static void AttemptRepair(MemoryMappedFileHolder indexFile, MemoryMappedFileHolder dataFile)
    {
        try
        {
            Logging.Logger.Warning("Attempting to repair corrupted files");
            
            // Truncate to valid data only
            long lastValidIndexPosition = FindLastValidIndexEntry(indexFile);
            if (lastValidIndexPosition > indexFile.GetDataStartPosition())
            {
                indexFile.Stream.SetLength(lastValidIndexPosition);
                Logging.Logger.Information("Truncated index file to position: {Position}", lastValidIndexPosition);
            }
            
            long lastValidDataPosition = FindLastValidDataPosition(indexFile, dataFile);
            if (lastValidDataPosition > dataFile.GetDataStartPosition())
            {
                dataFile.Stream.SetLength(lastValidDataPosition);
                Logging.Logger.Information("Truncated data file to position: {Position}", lastValidDataPosition);
            }
        }
        catch (Exception ex)
        {
            Logging.Logger.Error(ex, "Failed to repair corrupted files");
            throw;
        }
    }
    
    private static long FindLastValidIndexEntry(MemoryMappedFileHolder indexFile)
    {
        long position = indexFile.GetDataStartPosition();
        long lastValidPosition = position;
        
        try
        {
            indexFile.Stream.Seek(position, SeekOrigin.Begin);
            
            Span<byte> entry = stackalloc byte[MemoryMappedList.IndexEntryByteLength];
            while (position + MemoryMappedList.IndexEntryByteLength <= indexFile.Stream.Length)
            {
                if (indexFile.Stream.Read(entry) != MemoryMappedList.IndexEntryByteLength)
                    break;
                
                Guid id = new(entry[..MemoryMappedList.IdBytesLength]);
                if (id.Equals(Guid.Empty))
                    break;
                
                lastValidPosition = position + MemoryMappedList.IndexEntryByteLength;
                position += MemoryMappedList.IndexEntryByteLength;
            }
        }
        catch (Exception ex)
        {
            Logging.Logger.Warning(ex, "Error while finding last valid index entry");
        }
        
        return lastValidPosition;
    }
    
    private static long FindLastValidDataPosition(MemoryMappedFileHolder indexFile, MemoryMappedFileHolder dataFile)
    {
        long lastValidDataPosition = dataFile.GetDataStartPosition();
        
        try
        {
            indexFile.Stream.Seek(indexFile.GetDataStartPosition(), SeekOrigin.Begin);
            
            long position = indexFile.GetDataStartPosition();
            Span<byte> entry = stackalloc byte[MemoryMappedList.IndexEntryByteLength];
            while (position + MemoryMappedList.IndexEntryByteLength <= indexFile.Stream.Length)
            {
                if (indexFile.Stream.Read(entry) != MemoryMappedList.IndexEntryByteLength)
                    break;
                
                Guid id = new(entry[..MemoryMappedList.IdBytesLength]);
                if (id.Equals(Guid.Empty))
                    break;
                
                if (!id.Equals(MemoryMappedList.TombStone))
                {
                    long offset = BitConverter.ToInt64(entry.Slice(MemoryMappedList.IdBytesLength, sizeof(long)));
                    int length = BitConverter.ToInt32(entry.Slice(MemoryMappedList.IdBytesLength + sizeof(long), sizeof(int)));
                    
                    if (offset >= dataFile.GetDataStartPosition() && length > 0 && offset + length <= dataFile.Stream.Length)
                    {
                        lastValidDataPosition = Math.Max(lastValidDataPosition, offset + length);
                    }
                }
                
                position += MemoryMappedList.IndexEntryByteLength;
            }
        }
        catch (Exception ex)
        {
            Logging.Logger.Warning(ex, "Error while finding last valid data position");
        }
        
        return lastValidDataPosition;
    }
}