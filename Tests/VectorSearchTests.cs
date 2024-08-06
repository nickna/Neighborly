using Neighborly.Tests.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neighborly.Tests;

[TestFixture]
public class VectorSearchTests
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

    private VectorDatabase _db;
    private MockLogger<VectorDatabase> _logger = new MockLogger<VectorDatabase>();

    [SetUp]
    public void Setup()
    {
        _db?.Dispose();
        _db = new VectorDatabase(_logger, null);
        foreach (var originalText in s_originalTexts)
        {
            var v = new Vector(originalText: originalText);
            _db.Vectors.Add(v);
        }
        _db.RebuildSearchIndexesAsync().Wait();
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
    }

    [Test]
    public void SearchByFullText([ValueSource(nameof(s_originalTexts))] string originalText)
    {
        // Arrange
        var searchText = originalText;

        // Act 
        var results = _db.Search(text: searchText, k: 1);

        // Assert
        Assert.That(results.Count, Is.EqualTo(1));
    }

    [Test]
    public void SearchByPartialText([ValueSource(nameof(s_originalTexts))] string originalText)
    {
        // Arrange -- Get first word from original text
        var searchText = originalText.Split(' ')[0];

        // Act 
        var results = _db.Search(text: searchText, k: 1);

        // Assert
        Assert.That(results.Count, Is.EqualTo(1));
    }

    [Test]
    public void SearchByWrongText([ValueSource(nameof(s_originalTexts))] string originalText)
    {
        // Arrange -- Get first word from original text
        var searchText = "this text does not exist";

        // Act 
        var results = _db.Search(text: searchText, k: 1);

        // Assert
        Assert.That(results.Count, Is.EqualTo(0));
    }

}
