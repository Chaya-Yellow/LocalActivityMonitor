using ActivityMonitor.Core.Tracking;
using FluentAssertions;
using Xunit;

namespace ActivityMonitor.Tests.ClassificationTests;

/// <summary>
/// Tests for <see cref="FileTracker.Parse"/>.
/// Validates that editor/IDE/terminal/remote-desktop window titles are
/// correctly parsed into FileName, FilePath, ProjectName, and IsRemote.
/// </summary>
public class FileTrackerTests
{
    [Fact]
    public void Parse_VsCodeTitle_ExtractsFileNameAndProject()
    {
        // Arrange
        var tracker = new FileTracker();
        var title = "app.py - myproject - Visual Studio Code";
        var process = "code.exe";
        string? workingDir = null;

        // Act
        var result = tracker.Parse(title, process, workingDir);

        // Assert
        result.Should().NotBeNull();
        result!.FileName.Should().Be("app.py");
        result.ProjectName.Should().Be("myproject");
    }

    [Fact]
    public void Parse_PhotoshopTitle_WithWorkingDir_ReturnsFilePath()
    {
        // Arrange — Photoshop is not in KnownEditorProcesses but has "@" in title
        var tracker = new FileTracker();
        var title = "首页设计_v3.psd @ 100% (RGB/8)";
        var process = "photoshop.exe";
        var workingDir = @"D:\Design\Web";

        // Act
        var result = tracker.Parse(title, process, workingDir);

        // Assert
        result.Should().NotBeNull();
        result!.FileName.Should().Be("首页设计_v3.psd");
        result.FilePath.Should().Be(@"D:\Design\Web\首页设计_v3.psd");
    }

    [Fact]
    public void Parse_CmdTitle_ExtractsProjectFromPath()
    {
        // Arrange
        var tracker = new FileTracker();
        var title = @"cmd.exe - C:\Users\cccha\Code\ActivityMonitor";
        var process = "cmd.exe";
        string? workingDir = null;

        // Act
        var result = tracker.Parse(title, process, workingDir);

        // Assert
        result.Should().NotBeNull();
        result!.ProjectName.Should().Be("ActivityMonitor");
    }

    [Fact]
    public void Parse_RdpTitle_ReturnsRemoteInfo()
    {
        // Arrange
        var tracker = new FileTracker();
        var title = "远程桌面连接 - 192.168.1.100";
        var process = "mstsc.exe";
        string? workingDir = null;

        // Act
        var result = tracker.Parse(title, process, workingDir);

        // Assert
        result.Should().NotBeNull();
        result!.IsRemote.Should().BeTrue();
        result.FileName.Should().Be("192.168.1.100");
    }

    [Fact]
    public void Parse_UnknownProcess_ReturnsNonNullFileInfo()
    {
        // Arrange — unknown process falls through to ParseGenericTitle
        var tracker = new FileTracker();
        var title = "some window title";
        var process = "unknown.exe";
        string? workingDir = null;

        // Act
        var result = tracker.Parse(title, process, workingDir);

        // Assert — Parse never returns null for non-empty title;
        // Generic parser returns a FileInfo with null fields when no filename detected
        result.Should().NotBeNull();
        result!.FileName.Should().BeNull();
    }

    [Fact]
    public void Parse_EmptyTitle_ReturnsNull()
    {
        // Arrange
        var tracker = new FileTracker();
        var title = "";
        var process = "code.exe";
        string? workingDir = null;

        // Act
        var result = tracker.Parse(title, process, workingDir);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Parse_RdpEnglishTitle_ReturnsRemoteInfo()
    {
        // Arrange
        var tracker = new FileTracker();
        var title = "Remote Desktop - server-name";
        var process = "mstsc.exe";
        string? workingDir = null;

        // Act
        var result = tracker.Parse(title, process, workingDir);

        // Assert
        result.Should().NotBeNull();
        result!.IsRemote.Should().BeTrue();
        result.FileName.Should().Be("server-name");
    }
}
