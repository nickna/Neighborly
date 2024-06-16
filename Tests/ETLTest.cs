using Neighborly.ETL;

namespace Neighborly.Tests;

[TestFixture]
public class ETLTest
{
    public static IReadOnlyList<IETL> EtlImplementations =
    [
        new Csv(), new JSON(), new JSONZ(), new Neighborly.ETL.Parquet()  // HDF5 is not implemented yet
    ];

    [TestCaseSource(nameof(EtlImplementations))]
    public async Task Can_SaveAndLoad_Vectors(IETL etl)
    {
        // Arrange,
        var vectors = new List<Vector>
        {
            new([ 1f, 2, 3 ], "Original Text 1"),
            new([ 4f, 5, 6 ], "Original Text 2"),
            new([ 7f, 8, 9 ], "Original Text 3")
        };

        var path = Path.GetTempFileName();
        try
        {
            // Act
            await etl.ExportDataAsync(vectors, path).ConfigureAwait(true);
            var loadedVectors = new List<Vector>();
            await etl.ImportDataAsync(path, loadedVectors).ConfigureAwait(true);

            // Assert
            Assert.That(vectors, Has.Count.EqualTo(loadedVectors.Count));
            for (int i = 0; i < vectors.Count; i++)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(vectors[i].Id, Is.EqualTo(loadedVectors[i].Id));
                    Assert.That(vectors[i].Values, Is.EqualTo(loadedVectors[i].Values));
                    Assert.That(vectors[i].Tags, Is.EqualTo(loadedVectors[i].Tags));
                    Assert.That(vectors[i].OriginalText, Is.EqualTo(loadedVectors[i].OriginalText));
                });
            }
        }
        finally
        {
            File.Delete(path);
        }
    }
}
