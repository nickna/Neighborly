using NUnit.Framework;
using Neighborly;

namespace Neighborly.Tests;

[TestFixture]
public class BackgroundIndexServiceOptionsTests
{
    [Test]
    public void Default_CreatesValidConfiguration()
    {
        var options = BackgroundIndexServiceOptions.Default();

        Assert.DoesNotThrow(() => options.Validate());
        Assert.That(options.RebuildDelay, Is.EqualTo(TimeSpan.FromSeconds(5)));
        Assert.That(options.CheckInterval, Is.EqualTo(TimeSpan.FromSeconds(5)));
        Assert.That(options.ShutdownTimeout, Is.EqualTo(TimeSpan.FromSeconds(5)));
        Assert.That(options.Enabled, Is.True);
        Assert.That(options.AutoDisableOnMobile, Is.True);
    }

    [Test]
    public void Aggressive_HasShortDelays()
    {
        var options = BackgroundIndexServiceOptions.Aggressive();

        Assert.DoesNotThrow(() => options.Validate());
        Assert.That(options.RebuildDelay, Is.EqualTo(TimeSpan.FromSeconds(1)));
        Assert.That(options.CheckInterval, Is.EqualTo(TimeSpan.FromSeconds(1)));
        Assert.That(options.ShutdownTimeout, Is.EqualTo(TimeSpan.FromSeconds(3)));
    }

    [Test]
    public void Conservative_HasLongDelays()
    {
        var options = BackgroundIndexServiceOptions.Conservative();

        Assert.DoesNotThrow(() => options.Validate());
        Assert.That(options.RebuildDelay, Is.EqualTo(TimeSpan.FromSeconds(30)));
        Assert.That(options.CheckInterval, Is.EqualTo(TimeSpan.FromSeconds(10)));
    }

    [Test]
    public void Disabled_HasEnabledSetToFalse()
    {
        var options = BackgroundIndexServiceOptions.Disabled();

        Assert.That(options.Enabled, Is.False);
        Assert.That(options.AutoDisableOnMobile, Is.False);
    }

    [Test]
    public void Mobile_HasEnabledSetToFalse()
    {
        var options = BackgroundIndexServiceOptions.Mobile();

        Assert.That(options.Enabled, Is.False);
        Assert.That(options.AutoDisableOnMobile, Is.True);
    }

    [Test]
    public void Validate_WithTooShortRebuildDelay_ThrowsArgumentOutOfRangeException()
    {
        var options = new BackgroundIndexServiceOptions
        {
            RebuildDelay = TimeSpan.FromMilliseconds(50)
        };

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
        Assert.That(ex!.ParamName, Is.EqualTo("RebuildDelay"));
    }

    [Test]
    public void Validate_WithTooLongRebuildDelay_ThrowsArgumentOutOfRangeException()
    {
        var options = new BackgroundIndexServiceOptions
        {
            RebuildDelay = TimeSpan.FromMinutes(10)
        };

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
        Assert.That(ex!.ParamName, Is.EqualTo("RebuildDelay"));
    }

    [Test]
    public void Validate_WithTooShortCheckInterval_ThrowsArgumentOutOfRangeException()
    {
        var options = new BackgroundIndexServiceOptions
        {
            CheckInterval = TimeSpan.FromMilliseconds(50)
        };

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
        Assert.That(ex!.ParamName, Is.EqualTo("CheckInterval"));
    }

    [Test]
    public void Validate_WithTooShortShutdownTimeout_ThrowsArgumentOutOfRangeException()
    {
        var options = new BackgroundIndexServiceOptions
        {
            ShutdownTimeout = TimeSpan.FromMilliseconds(500)
        };

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
        Assert.That(ex!.ParamName, Is.EqualTo("ShutdownTimeout"));
    }

    [Test]
    public void Validate_WithValidCustomValues_DoesNotThrow()
    {
        var options = new BackgroundIndexServiceOptions
        {
            RebuildDelay = TimeSpan.FromSeconds(10),
            CheckInterval = TimeSpan.FromSeconds(2),
            ShutdownTimeout = TimeSpan.FromSeconds(7)
        };

        Assert.DoesNotThrow(() => options.Validate());
    }
}
