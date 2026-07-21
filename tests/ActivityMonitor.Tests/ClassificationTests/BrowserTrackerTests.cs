using ActivityMonitor.Core.Tracking;
using FluentAssertions;
using Xunit;

namespace ActivityMonitor.Tests.ClassificationTests;

/// <summary>
/// Tests for <see cref="BrowserTracker.Parse"/>.
/// Validates that browser window titles are correctly parsed into
/// PageTitle, Domain, and IsPrivate.
/// </summary>
public class BrowserTrackerTests
{
    [Fact]
    public void Parse_ChromeTitle_ExtractsPageTitle()
    {
        // Arrange
        var tracker = new BrowserTracker();
        var title = "GitHub - Google Chrome";
        var process = "chrome.exe";

        // Act
        var result = tracker.Parse(title, process);

        // Assert
        result.Should().NotBeNull();
        result!.PageTitle.Should().Be("GitHub");
        // "GitHub" (alone) has no recognizable domain pattern with a dot + TLD
        result.Domain.Should().BeNull();
        result.IsPrivate.Should().BeFalse();
    }

    [Fact]
    public void Parse_EdgeTitle_ExtractsPageTitle()
    {
        // Arrange
        var tracker = new BrowserTracker();
        var title = "Stack Overflow - Microsoft Edge";
        var process = "msedge.exe";

        // Act
        var result = tracker.Parse(title, process);

        // Assert
        result.Should().NotBeNull();
        result!.PageTitle.Should().Be("Stack Overflow");
        result.Domain.Should().BeNull(); // "Stack Overflow" has no dot pattern
        result.IsPrivate.Should().BeFalse();
    }

    [Fact]
    public void Parse_FirefoxTitle_ExtractsPageTitle()
    {
        // Arrange
        var tracker = new BrowserTracker();
        var title = "百度一下 - Mozilla Firefox";
        var process = "firefox.exe";

        // Act
        var result = tracker.Parse(title, process);

        // Assert
        result.Should().NotBeNull();
        result!.PageTitle.Should().Be("百度一下");
        result.IsPrivate.Should().BeFalse();
    }

    [Fact]
    public void Parse_NonBrowserProcess_ReturnsNull()
    {
        // Arrange
        var tracker = new BrowserTracker();
        var title = "program.cs - Visual Studio Code";
        var process = "code.exe";

        // Act
        var result = tracker.Parse(title, process);

        // Assert — code.exe is not a recognized browser
        result.Should().BeNull();
    }

    [Fact]
    public void Parse_EmptyTitle_ReturnsNull()
    {
        // Arrange
        var tracker = new BrowserTracker();
        var title = "";
        var process = "chrome.exe";

        // Act
        var result = tracker.Parse(title, process);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Parse_NullProcessName_ReturnsNull()
    {
        // Arrange
        var tracker = new BrowserTracker();
        var title = "some title";
        string? process = null;

        // Act
        var result = tracker.Parse(title, process!);

        // Assert — null/empty processName → BrowserKind.Unknown → null
        result.Should().BeNull();
    }

    [Fact]
    public void Parse_IncognitoTitle_MarksIsPrivate()
    {
        // Arrange
        var tracker = new BrowserTracker();
        var title = "Private Browsing - Incognito - Google Chrome";
        var process = "chrome.exe";

        // Act
        var result = tracker.Parse(title, process);

        // Assert
        result.Should().NotBeNull();
        result!.IsPrivate.Should().BeTrue();
    }

    [Fact]
    public void Parse_ChromeTitleWithChromiumSuffix_ParsesCorrectly()
    {
        // Arrange — Chromium-suffixed Chrome-based browsers
        var tracker = new BrowserTracker();
        var title = "My Dashboard - Chromium";
        var process = "chrome.exe"; // "chrome" in name → Chrome kind

        // Act
        var result = tracker.Parse(title, process);

        // Assert
        result.Should().NotBeNull();
        result!.PageTitle.Should().Be("My Dashboard");
        result.IsPrivate.Should().BeFalse();
    }
}
