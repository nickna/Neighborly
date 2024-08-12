using System;
using System.IO;
using System.Text;

namespace Neighborly;

public struct VectorAttributes
{
    public sbyte Priority { get; set; }
    public uint UserId { get; set; }
    public uint OrgId { get; set; }

    private const int s_priorityBytesLength = sizeof(sbyte);
    private const int s_userIdBytesLength = sizeof(uint);
    private const int s_orgIdBytesLength = sizeof(uint);

    public VectorAttributes() { }

    public VectorAttributes(BinaryReader stream)
    {
        Priority = stream.ReadSByte();
        UserId = stream.ReadUInt32();
        OrgId = stream.ReadUInt32();
    }

    public byte[] ToBinary()
    {
        int resultLength = s_priorityBytesLength + s_userIdBytesLength + s_orgIdBytesLength;
        Span<byte> result = stackalloc byte[resultLength];

        Span<byte> priorityBytes = result[..s_priorityBytesLength];
        priorityBytes[0] = (byte)Priority;

        Span<byte> userIdBytes = result[s_priorityBytesLength..(s_priorityBytesLength + s_userIdBytesLength)];
        if (!BitConverter.TryWriteBytes(userIdBytes, UserId))
        {
            throw new InvalidOperationException("Failed to write UserId to bytes");
        }

        Span<byte> orgIdBytes = result[(s_priorityBytesLength + s_userIdBytesLength)..];
        if (!BitConverter.TryWriteBytes(orgIdBytes, OrgId))
        {
            throw new InvalidOperationException("Failed to write OrgId to bytes");
        }

        return result.ToArray();
    }
}
