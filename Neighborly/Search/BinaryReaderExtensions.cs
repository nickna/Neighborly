namespace Neighborly;

internal static class BinaryReaderExtensions
{
    public static Guid ReadGuid(this BinaryReader reader, Span<byte> buffer)
    {
        ArgumentNullException.ThrowIfNull(reader);
        reader.Read(buffer);
        return new Guid(buffer);
    }
}
