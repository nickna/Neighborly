namespace Neighborly;

internal static class CorruptionDetector
{
    public static bool ValidateIndexFile(RandomAccessFileHolder indexFile, long expectedCount)
    {
        try
        {
            long validEntries = 0;
            long position = indexFile.GetDataStartPosition();
            long fileLength = indexFile.GetLength();

            Span<byte> entry = stackalloc byte[MemoryMappedList.IndexEntryByteLength];
            while (position + MemoryMappedList.IndexEntryByteLength <= fileLength)
            {
                int bytesRead = indexFile.Read(position, entry);
                if (bytesRead != MemoryMappedList.IndexEntryByteLength)
                    break;

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

    public static bool ValidateDataFile(RandomAccessFileHolder dataFile)
    {
        try
        {
            // Basic sanity checks
            if (dataFile.GetLength() < dataFile.GetDataStartPosition())
                return false;

            return true;
        }
        catch (Exception ex)
        {
            Logging.Logger.Error(ex, "Failed to validate data file: {FileName}", dataFile.Filename);
            return false;
        }
    }

    public static void AttemptRepair(RandomAccessFileHolder indexFile, RandomAccessFileHolder dataFile)
    {
        try
        {
            Logging.Logger.Warning("Attempting to repair corrupted files");

            // Truncate to valid data only
            long lastValidIndexPosition = FindLastValidIndexEntry(indexFile);
            if (lastValidIndexPosition > indexFile.GetDataStartPosition())
            {
                indexFile.SetLength(lastValidIndexPosition);
                Logging.Logger.Information("Truncated index file to position: {Position}", lastValidIndexPosition);
            }

            long lastValidDataPosition = FindLastValidDataPosition(indexFile, dataFile);
            if (lastValidDataPosition > dataFile.GetDataStartPosition())
            {
                dataFile.SetLength(lastValidDataPosition);
                Logging.Logger.Information("Truncated data file to position: {Position}", lastValidDataPosition);
            }
        }
        catch (Exception ex)
        {
            Logging.Logger.Error(ex, "Failed to repair corrupted files");
            throw;
        }
    }

    private static long FindLastValidIndexEntry(RandomAccessFileHolder indexFile)
    {
        long position = indexFile.GetDataStartPosition();
        long lastValidPosition = position;
        long fileLength = indexFile.GetLength();

        try
        {
            Span<byte> entry = stackalloc byte[MemoryMappedList.IndexEntryByteLength];
            while (position + MemoryMappedList.IndexEntryByteLength <= fileLength)
            {
                if (indexFile.Read(position, entry) != MemoryMappedList.IndexEntryByteLength)
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

    private static long FindLastValidDataPosition(RandomAccessFileHolder indexFile, RandomAccessFileHolder dataFile)
    {
        long lastValidDataPosition = dataFile.GetDataStartPosition();
        long indexFileLength = indexFile.GetLength();
        long dataFileLength = dataFile.GetLength();

        try
        {
            long position = indexFile.GetDataStartPosition();
            Span<byte> entry = stackalloc byte[MemoryMappedList.IndexEntryByteLength];
            while (position + MemoryMappedList.IndexEntryByteLength <= indexFileLength)
            {
                if (indexFile.Read(position, entry) != MemoryMappedList.IndexEntryByteLength)
                    break;

                Guid id = new(entry[..MemoryMappedList.IdBytesLength]);
                if (id.Equals(Guid.Empty))
                    break;

                if (!id.Equals(MemoryMappedList.TombStone))
                {
                    long offset = BitConverter.ToInt64(entry.Slice(MemoryMappedList.IdBytesLength, sizeof(long)));
                    int length = BitConverter.ToInt32(entry.Slice(MemoryMappedList.IdBytesLength + sizeof(long), sizeof(int)));

                    if (offset >= dataFile.GetDataStartPosition() && length > 0 && offset + length <= dataFileLength)
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