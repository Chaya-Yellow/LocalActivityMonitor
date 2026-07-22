using ActivityMonitor.Core.Classification;
using ActivityMonitor.Core.Models;
using FluentAssertions;
using Xunit;

namespace ActivityMonitor.Tests.ClassificationTests;

/// <summary>
/// Tests for <see cref="ActivityCategorizer.Classify"/>.
/// Validates that activity events are correctly classified by process name
/// into categories (web/file/app/sleep) and work tags (work/break/personal/unknown).
/// </summary>
public class ActivityCategorizerTests
{
    [Fact]
    public void Classify_ChromeProcess_ReturnsWebCategory()
    {
        // Arrange
        var categorizer = new ActivityCategorizer();
        var activity = new ActivityEvent { ProcessName = "chrome.exe" };

        // Act
        var (category, workTag) = categorizer.Classify(activity);

        // Assert
        category.Should().Be(Category.Web);
    }

    [Fact]
    public void Classify_EdgeProcess_ReturnsWebCategory()
    {
        // Arrange
        var categorizer = new ActivityCategorizer();
        var activity = new ActivityEvent { ProcessName = "msedge.exe" };

        // Act
        var (category, _) = categorizer.Classify(activity);

        // Assert
        category.Should().Be(Category.Web);
    }

    [Fact]
    public void Classify_FirefoxProcess_ReturnsWebCategory()
    {
        // Arrange
        var categorizer = new ActivityCategorizer();
        var activity = new ActivityEvent { ProcessName = "firefox.exe" };

        // Act
        var (category, _) = categorizer.Classify(activity);

        // Assert
        category.Should().Be(Category.Web);
    }

    [Fact]
    public void Classify_CodeProcess_ReturnsFileCategory()
    {
        // Arrange
        var categorizer = new ActivityCategorizer();
        var activity = new ActivityEvent { ProcessName = "code.exe" };

        // Act
        var (category, _) = categorizer.Classify(activity);

        // Assert
        category.Should().Be(Category.File);
    }

    [Fact]
    public void Classify_MstscProcess_ReturnsAppCategory()
    {
        // Arrange — mstsc.exe is a remote desktop process, classifies as "app"
        var categorizer = new ActivityCategorizer();
        var activity = new ActivityEvent { ProcessName = "mstsc.exe" };

        // Act
        var (category, _) = categorizer.Classify(activity);

        // Assert
        category.Should().Be(Category.App);
    }

    [Fact]
    public void Classify_UnknownProcess_ReturnsAppCategory()
    {
        // Arrange
        var categorizer = new ActivityCategorizer();
        var activity = new ActivityEvent { ProcessName = "unknown.exe" };

        // Act
        var (category, _) = categorizer.Classify(activity);

        // Assert
        category.Should().Be(Category.App);
    }

    [Fact]
    public void Classify_UserCategorySet_ReturnsUserCategory()
    {
        // Arrange — chrome.exe normally classifies as "web", but UserCategory="file"
        // is a valid category that should override automatic classification.
        var categorizer = new ActivityCategorizer();
        var activity = new ActivityEvent
        {
            ProcessName = "chrome.exe",
            UserCategory = "file",
        };

        // Act
        var (category, _) = categorizer.Classify(activity);

        // Assert — user category overrides automatic browser detection
        category.Should().Be(Category.File);
    }

    [Fact]
    public void Classify_NullProcess_ReturnsAppUnknown()
    {
        // Arrange — null ActivityEvent falls through to default
        var categorizer = new ActivityCategorizer();
        ActivityEvent activity = null!;

        // Act
        var (category, workTag) = categorizer.Classify(activity);

        // Assert
        category.Should().Be(Category.App);
        workTag.Should().Be(WorkTag.Unknown);
    }

    [Fact]
    public void Classify_LogonUiProcess_ReturnsBreakCategory()
    {
        // Arrange — logonui.exe is a system/lock-screen process
        var categorizer = new ActivityCategorizer();
        var activity = new ActivityEvent { ProcessName = "logonui.exe" };

        // Act
        var (category, workTag) = categorizer.Classify(activity);

        // Assert — W1-M1: 锁屏标记，category=locked
        category.Should().Be(Category.Locked);
        workTag.Should().Be(WorkTag.Unknown);
    }

    [Fact]
    public void Classify_WorkDomain_SetsWorkTagToWork()
    {
        // Arrange — chrome.exe + github.com domain → work
        var categorizer = new ActivityCategorizer();
        var activity = new ActivityEvent
        {
            ProcessName = "chrome.exe",
            Domain = "github.com",
        };

        // Act
        var (category, workTag) = categorizer.Classify(activity);

        // Assert
        category.Should().Be(Category.Web);
        workTag.Should().Be(WorkTag.Work);
    }

    [Fact]
    public void Classify_PersonalDomain_SetsWorkTagToPersonal()
    {
        // Arrange — chrome.exe + youtube.com domain → personal
        var categorizer = new ActivityCategorizer();
        var activity = new ActivityEvent
        {
            ProcessName = "chrome.exe",
            Domain = "youtube.com",
        };

        // Act
        var (category, workTag) = categorizer.Classify(activity);

        // Assert
        category.Should().Be(Category.Web);
        workTag.Should().Be(WorkTag.Personal);
    }
}
