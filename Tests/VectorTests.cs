namespace Neighborly.Tests;

using Neighborly;
using Neighborly.Distance;
using Neighborly.Tests.Helpers;

[TestFixture]
public class VectorTests
{
    private static readonly string[] s_originalTexts = [
        "The quick brown fox jumps over the lazy dog", // English
        "素早い茶色の狐が怠け者の犬を飛び越える", // Japanese
        "Le vif renard brun saute par-dessus le chien paresseux", // French
        "El rápido zorro marrón salta sobre el perro perezoso", // Spanish
        "Быстрая коричневая лисица перепрыгивает через ленивую собаку", // Russian
        "الثعلب البني السريع يقفز فوق الكلب الكسول", // Arabic (RTL)
        "快速的棕色狐狸跳过了懒狗", // Chinese (Simplified)
        "השועל החום המהיר קופץ מעל הכלב העצלן" // Hebrew (RTL)
    ];

    [Test]
    public void ToBinary_CanBeUsedAsInputFor_Ctor_As_BinaryReader([ValueSource(nameof(s_originalTexts))] string originalText)
    {
        // Arrange
        float[] floatArray = [1.0f, 2.1f, 3.2f, 4.5f, 5.7f];
        Vector originalVector = new(floatArray, originalText);

        // Act
        byte[] binary = originalVector.ToBinary();
        using MemoryStream ms = new(binary);
        using BinaryReader br = new(ms);
        Vector newVector = new(br);
        byte[] newBinary = newVector.ToBinary();

        // Assert
        Assert.That(newVector.Id, Is.EqualTo(originalVector.Id));
        Assert.That(newVector.OriginalText, Is.EqualTo(originalVector.OriginalText));
        Assert.That(newVector.Values, Is.EqualTo(originalVector.Values));
        Assert.That(newVector.Tags, Is.EqualTo(originalVector.Tags));
        Assert.That(newBinary, Is.EqualTo(binary));
    }

    [Test]
    public void ToBinary_CanBeUsedAsInputFor_Ctor_As_Array([ValueSource(nameof(s_originalTexts))] string originalText)
    {
        // Arrange
        float[] floatArray = [1.0f, 2.1f, 3.2f, 4.5f, 5.7f];
        Vector originalVector = new(floatArray, originalText);

        // Act
        byte[] binary = originalVector.ToBinary();
        Vector newVector = new(binary);
        byte[] newBinary = newVector.ToBinary();

        // Assert
        Assert.That(newVector.Id, Is.EqualTo(originalVector.Id));
        Assert.That(newVector.OriginalText, Is.EqualTo(originalVector.OriginalText));
        Assert.That(newVector.Values, Is.EqualTo(originalVector.Values));
        Assert.That(newVector.Tags, Is.EqualTo(originalVector.Tags));
        Assert.That(newBinary, Is.EqualTo(binary));
    }

    public void InPlaceAdd()
    {
        // Arrange
        Vector firstAddend = new([1.0f, 2.0f]);
        Vector secondAddend = new([3.1f, 4.2f]);

        // Act
        firstAddend.InPlaceAdd(secondAddend);

        // Assert
        Assert.That(firstAddend.Values[0], Is.EqualTo(4.1f).Within(0.01f));
        Assert.That(firstAddend.Values[1], Is.EqualTo(6.2f).Within(0.01f));
    }

    [Test]
    public void InPlaceSubtract()
    {
        // Arrange
        Vector minuend = new([1.0f, 2.0f]);
        Vector subtrahend = new([3.1f, 4.2f]);

        // Act
        minuend.InPlaceSubtract(subtrahend);

        // Assert
        Assert.That(minuend.Values[0], Is.EqualTo(-2.1f).Within(0.01f));
        Assert.That(minuend.Values[1], Is.EqualTo(-2.2f).Within(0.01f));
    }

    [Test]
    public void InPlaceDivideByInt()
    {
        // Arrange
        Vector divisor = new([25.0f, 100.0f]);
        int scalar = 5;

        // Act
        divisor.InPlaceDivide(scalar);

        // Assert
        Assert.That(divisor.Values[0], Is.EqualTo(5f).Within(0.01f));
        Assert.That(divisor.Values[1], Is.EqualTo(20f).Within(0.01f));
    }

    [Test]
    public void DistanceUsesProvidedCalculator()
    {
        // Arrange
        Vector vector1 = new([1.0f, 2.0f]);
        Vector vector2 = new([3.1f, 4.2f]);
        const float expectedDistance = 13.37f;
        IDistanceCalculator calculator = new MockDistanceCalculator(vector1, vector2, expectedDistance);

        // Act
        float distance = vector1.Distance(vector2, calculator);

        // Assert
        Assert.That(distance, Is.EqualTo(expectedDistance).Within(0.01f));
    }

    [Test]
    public void DistanceUsesEuclideanCalculatorByDefault()
    {
        // Arrange
        Vector vector1 = new([1.0f, 2.0f]);
        Vector vector2 = new([3.1f, 4.2f]);

        // Act
        float distance = vector1.Distance(vector2);

        // Assert
        Assert.That(distance, Is.EqualTo(3.041f).Within(0.01f));
    }

    [Test]
    public void Constructor_GeneratesEmbeddings_WhenOriginalTextIsSpecified([ValueSource(nameof(s_originalTexts))] string originalText)
    {
        // Arrange
        float[] expectedEmbeddings = Neighborly.EmbeddingFactory.Instance.GenerateEmbedding(originalText);

        // Act
        Vector vector = new Vector(originalText);

        // Assert
        Assert.That(vector.OriginalText, Is.EqualTo(originalText));
        Assert.That(vector.Values, Is.EqualTo(expectedEmbeddings));
    }
}
