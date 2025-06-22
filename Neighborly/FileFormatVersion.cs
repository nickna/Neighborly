namespace Neighborly;

internal static class FileFormatVersion
{
    public const int CURRENT_VERSION = 1;
    public const int HEADER_SIZE = 32; // Version + reserved space for future metadata
    
    public static readonly byte[] MAGIC_BYTES = "NMMF"u8.ToArray(); // Neighborly Memory Mapped File
    
    public struct FileHeader
    {
        public byte[] MagicBytes { get; set; }
        public int Version { get; set; }
        public long CreatedTimestamp { get; set; }
        public long LastModifiedTimestamp { get; set; }
        public int Reserved1 { get; set; }
        public int Reserved2 { get; set; }
        
        public static FileHeader Create()
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return new FileHeader
            {
                MagicBytes = MAGIC_BYTES,
                Version = CURRENT_VERSION,
                CreatedTimestamp = now,
                LastModifiedTimestamp = now,
                Reserved1 = 0,
                Reserved2 = 0
            };
        }
        
        public void UpdateModifiedTime()
        {
            LastModifiedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
        
        public byte[] ToBytes()
        {
            var bytes = new byte[HEADER_SIZE];
            var span = bytes.AsSpan();
            
            MagicBytes.CopyTo(span[..4]);
            BitConverter.TryWriteBytes(span[4..8], Version);
            BitConverter.TryWriteBytes(span[8..16], CreatedTimestamp);
            BitConverter.TryWriteBytes(span[16..24], LastModifiedTimestamp);
            BitConverter.TryWriteBytes(span[24..28], Reserved1);
            BitConverter.TryWriteBytes(span[28..32], Reserved2);
            
            return bytes;
        }
        
        public static FileHeader FromBytes(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length < HEADER_SIZE)
                throw new InvalidDataException("Invalid header size");
                
            return new FileHeader
            {
                MagicBytes = bytes[..4].ToArray(),
                Version = BitConverter.ToInt32(bytes[4..8]),
                CreatedTimestamp = BitConverter.ToInt64(bytes[8..16]),
                LastModifiedTimestamp = BitConverter.ToInt64(bytes[16..24]),
                Reserved1 = BitConverter.ToInt32(bytes[24..28]),
                Reserved2 = BitConverter.ToInt32(bytes[28..32])
            };
        }
        
        public bool IsValid()
        {
            return MagicBytes.SequenceEqual(MAGIC_BYTES) && 
                   Version > 0 && Version <= CURRENT_VERSION;
        }
        
        public bool IsSupported()
        {
            return IsValid() && Version <= CURRENT_VERSION;
        }
    }
}