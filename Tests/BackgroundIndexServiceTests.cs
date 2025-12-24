using Microsoft.Extensions.Logging;
using Neighborly;
using Neighborly.Tests.Helpers;
using NUnit.Framework;

namespace Neighborly.Tests;

[TestFixture]
public class BackgroundIndexServiceTests
{
    private MockLogger<BackgroundIndexService> _logger = null!;

    [SetUp]
    public void Setup()
    {
        _logger = new MockLogger<BackgroundIndexService>();
    }

    [Test]
    public async Task Start_CallsCallbackAfterDelay()
    {
        // Arrange
        var callbackInvoked = false;
        var options = new BackgroundIndexServiceOptions
        {
            RebuildDelay = TimeSpan.FromMilliseconds(500),
            CheckInterval = TimeSpan.FromMilliseconds(200)
        };

        IndexRebuildCallback callback = async (ct) =>
        {
            await Task.CompletedTask;
            callbackInvoked = true;
        };

        await using var service = new BackgroundIndexService(callback, options, _logger);

        // Act
        service.Start();
        service.NotifyModified();

        // Wait for rebuild to occur (delay + check interval + buffer)
        await Task.Delay(1000);

        // Assert
        Assert.That(callbackInvoked, Is.True, "Callback should have been invoked after delay.");
    }

    [Test]
    public async Task NotifyModified_DoesNotRebuildBeforeDelay()
    {
        // Arrange
        var callbackCount = 0;
        var options = new BackgroundIndexServiceOptions
        {
            RebuildDelay = TimeSpan.FromSeconds(2),
            CheckInterval = TimeSpan.FromMilliseconds(200)
        };

        IndexRebuildCallback callback = async (ct) =>
        {
            await Task.CompletedTask;
            callbackCount++;
        };

        await using var service = new BackgroundIndexService(callback, options, _logger);

        // Act
        service.Start();
        service.NotifyModified();

        // Wait less than rebuild delay
        await Task.Delay(500);

        // Assert
        Assert.That(callbackCount, Is.EqualTo(0), "Callback should not be invoked before delay expires.");
    }

    [Test]
    public async Task TriggerRebuildAsync_BypassesDelay()
    {
        // Arrange
        var callbackInvoked = false;
        var options = new BackgroundIndexServiceOptions
        {
            RebuildDelay = TimeSpan.FromSeconds(10), // Long delay
            CheckInterval = TimeSpan.FromSeconds(1)
        };

        IndexRebuildCallback callback = async (ct) =>
        {
            await Task.CompletedTask;
            callbackInvoked = true;
        };

        await using var service = new BackgroundIndexService(callback, options, _logger);

        // Act
        await service.TriggerRebuildAsync();

        // Assert
        Assert.That(callbackInvoked, Is.True, "Manual trigger should bypass delay.");
    }

    [Test]
    public async Task StopAsync_CompletesGracefully()
    {
        // Arrange
        var options = BackgroundIndexServiceOptions.Default();
        IndexRebuildCallback callback = async (ct) =>
        {
            await Task.CompletedTask;
        };

        var service = new BackgroundIndexService(callback, options, _logger);
        service.Start();

        // Act
        var result = await service.StopAsync(TimeSpan.FromSeconds(2));

        // Assert
        Assert.That(result, Is.True, "Service should stop gracefully.");

        // Cleanup
        await service.DisposeAsync();
    }

    [Test]
    public async Task MultipleNotifications_CoalescesIntoSingleRebuild()
    {
        // Arrange
        var callbackCount = 0;
        var options = new BackgroundIndexServiceOptions
        {
            RebuildDelay = TimeSpan.FromMilliseconds(500),
            CheckInterval = TimeSpan.FromMilliseconds(100)
        };

        IndexRebuildCallback callback = async (ct) =>
        {
            await Task.CompletedTask;
            callbackCount++;
        };

        await using var service = new BackgroundIndexService(callback, options, _logger);

        // Act
        service.Start();

        // Trigger multiple modifications rapidly
        for (int i = 0; i < 5; i++)
        {
            service.NotifyModified();
            await Task.Delay(50); // Less than rebuild delay
        }

        // Wait for rebuild to complete
        await Task.Delay(1000);

        // Assert
        Assert.That(callbackCount, Is.EqualTo(1), "Multiple rapid notifications should coalesce into single rebuild.");
    }

    [Test]
    public async Task CallbackThrows_WorkerContinuesRunning()
    {
        // Arrange
        var callbackCount = 0;
        var shouldThrow = true;
        var options = new BackgroundIndexServiceOptions
        {
            RebuildDelay = TimeSpan.FromMilliseconds(300),
            CheckInterval = TimeSpan.FromMilliseconds(100)
        };

        IndexRebuildCallback callback = async (ct) =>
        {
            await Task.CompletedTask;
            callbackCount++;
            if (shouldThrow)
            {
                shouldThrow = false; // Only throw once
                throw new InvalidOperationException("Test exception");
            }
        };

        await using var service = new BackgroundIndexService(callback, options, _logger);

        // Act
        service.Start();
        service.NotifyModified();

        // Wait for first rebuild (which throws)
        await Task.Delay(600);

        // Trigger another rebuild
        service.NotifyModified();
        await Task.Delay(600);

        // Assert
        Assert.That(callbackCount, Is.GreaterThanOrEqualTo(2), "Worker should continue after callback throws.");
    }

    [Test]
    public void DisabledOptions_DoesNotStart()
    {
        // Arrange
        var callbackInvoked = false;
        var options = BackgroundIndexServiceOptions.Disabled();

        IndexRebuildCallback callback = async (ct) =>
        {
            await Task.CompletedTask;
            callbackInvoked = true;
        };

        using var service = new BackgroundIndexService(callback, options, _logger);

        // Act
        service.Start();
        service.NotifyModified();

        // Assert
        Assert.That(callbackInvoked, Is.False, "Disabled service should not invoke callback.");
    }

    [Test]
    public async Task DisposeAsync_StopsBackgroundTask()
    {
        // Arrange
        var options = BackgroundIndexServiceOptions.Default();
        IndexRebuildCallback callback = async (ct) =>
        {
            await Task.Delay(100, ct);
        };

        var service = new BackgroundIndexService(callback, options, _logger);
        service.Start();

        // Act
        await service.DisposeAsync();

        // Assert - no exception should be thrown
        Assert.Pass("Service disposed successfully.");
    }

    [Test]
    public async Task Start_WhenAlreadyRunning_LogsWarning()
    {
        // Arrange
        var options = BackgroundIndexServiceOptions.Default();
        IndexRebuildCallback callback = async (ct) => await Task.CompletedTask;

        await using var service = new BackgroundIndexService(callback, options, _logger);

        // Act
        service.Start();
        service.Start(); // Start again

        // Give time for logging
        await Task.Delay(100);

        // Assert
        var warnings = _logger.GetLogEntries().Where(e => e.LogLevel == LogLevel.Warning).ToList();
        Assert.That(warnings, Has.Count.GreaterThan(0), "Should log warning when starting already-running service.");
    }
}
