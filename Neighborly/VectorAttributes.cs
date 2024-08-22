using System;
using System.IO;
using System.Text;

namespace Neighborly;

public struct VectorAttributes : IEquatable<VectorAttributes>, IDataPersistence
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
        var stream = new MemoryStream();
        var writer = new BinaryWriter(stream);
        this.ToBinaryStream(writer);
        return stream.ToArray();
    }

    public void ToBinaryStream(BinaryWriter writer)
    {
        writer.Write(Priority);
        writer.Write(UserId);
        writer.Write(OrgId);
    }

    public bool Equals(VectorAttributes other)
    {
        return Priority == other.Priority && UserId == other.UserId && OrgId == other.OrgId;
    }
}
