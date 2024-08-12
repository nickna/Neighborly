namespace Neighborly.Tests;

using System.IO;
using NUnit.Framework;

[TestFixture]
public class VectorAttributesTests
{
    [Test]
    public void ToBinary_CanBeUsedAsInputFor_Ctor_As_BinaryReader()
    {
        // Arrange
        VectorAttributes originalAttributes = new()
        {
            Priority = 5,
            UserId = 12345,
            OrgId = 67890
        };

        // Act
        byte[] binary = originalAttributes.ToBinary();
        using MemoryStream ms = new(binary);
        using BinaryReader br = new(ms);
        VectorAttributes newAttributes = new(br);
        byte[] newBinary = newAttributes.ToBinary();

        // Assert
        Assert.That(newAttributes.Priority, Is.EqualTo(originalAttributes.Priority));
        Assert.That(newAttributes.UserId, Is.EqualTo(originalAttributes.UserId));
        Assert.That(newAttributes.OrgId, Is.EqualTo(originalAttributes.OrgId));
        Assert.That(newBinary, Is.EqualTo(binary));
    }

    [Test]
    public void ToBinary_CanBeUsedAsInputFor_Ctor_As_Array()
    {
        // Arrange
        VectorAttributes originalAttributes = new()
        {
            Priority = 5,
            UserId = 12345,
            OrgId = 67890
        };

        // Act
        byte[] binary = originalAttributes.ToBinary();
        VectorAttributes newAttributes = new(new BinaryReader(new MemoryStream(binary)));
        byte[] newBinary = newAttributes.ToBinary();

        // Assert
        Assert.That(newAttributes.Priority, Is.EqualTo(originalAttributes.Priority));
        Assert.That(newAttributes.UserId, Is.EqualTo(originalAttributes.UserId));
        Assert.That(newAttributes.OrgId, Is.EqualTo(originalAttributes.OrgId));
        Assert.That(newBinary, Is.EqualTo(binary));
    }
}
