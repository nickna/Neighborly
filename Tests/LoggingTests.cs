using Microsoft.Extensions.Logging;
using Neighborly;
using Neighborly.Tests.Helpers;
using Serilog.Sinks.InMemory;

[TestFixture]
public class LoggingTests
{
    private MockLogger<VectorDatabase> _logger;
    private VectorDatabase _db;

    [SetUp]
    public void Setup()
    {
        _logger = new MockLogger<VectorDatabase>();
        _db = new VectorDatabase(_logger);
    }

    [Test]
    public async Task TestMethodThatShouldLogError()
    {
        // Act -- Load a non-existent file path
        try
        {
            await _db.LoadAsync(path: "nonexistent-file-path", createOnNew: false);
        }
        catch (FileNotFoundException ex)
        {
            // This is excepted behavior
        }

        // Assert
        Assert.That(_logger.LastLogLevel, Is.EqualTo(LogLevel.Error));
    }

}
