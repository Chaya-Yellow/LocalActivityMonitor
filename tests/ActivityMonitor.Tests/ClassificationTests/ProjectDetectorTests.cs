using System.IO;
using ActivityMonitor.Core.Classification;
using FluentAssertions;
using Xunit;

namespace ActivityMonitor.Tests.ClassificationTests;

/// <summary>
/// Tests for <see cref="ProjectDetector"/>.
/// Validates project detection from file paths and window titles
/// using Git repo detection, fallback heuristics, VS Code title parsing,
/// and terminal title parsing.
/// </summary>
public class ProjectDetectorTests : IDisposable
{
    private readonly List<string> _tempDirs = new();

    // ──────────────────────────────────────────────
    // DetectFromPath
    // ──────────────────────────────────────────────

    [Fact]
    public void DetectFromPath_PathWithGit_ReturnsRepoName()
    {
        // Arrange — create a temp directory with a .git subdirectory to simulate a repo
        var tempDir = CreateTempDir();
        var repoDir = Path.Combine(tempDir, "my-awesome-repo");
        Directory.CreateDirectory(repoDir);

        var gitDir = Path.Combine(repoDir, ".git");
        Directory.CreateDirectory(gitDir);

        var nestedDir = Path.Combine(repoDir, "src", "components");
        Directory.CreateDirectory(nestedDir);
        var filePath = Path.Combine(nestedDir, "main.cs");
        File.WriteAllText(filePath, "// test file");

        var detector = new ProjectDetector();

        // Act
        var result = detector.DetectFromPath(filePath);

        // Assert
        result.Should().Be("my-awesome-repo");
    }

    [Fact]
    public void DetectFromPath_PathWithoutGit_ReturnsLastTwoFolders()
    {
        // Arrange — create a real directory structure: TempDir\Website\v3\index.html
        var tempDir = CreateTempDir();
        var websiteDir = Path.Combine(tempDir, "Website", "v3");
        Directory.CreateDirectory(websiteDir);
        var filePath = Path.Combine(websiteDir, "index.html");
        File.WriteAllText(filePath, "test");

        var detector = new ProjectDetector();

        // Act
        var result = detector.DetectFromPath(filePath);

        // Assert — no .git found, falls back to last two folder segments
        result.Should().Be("Website/v3");
    }

    [Fact]
    public void DetectFromPath_EmptyPath_ReturnsUnknown()
    {
        // Arrange
        var detector = new ProjectDetector();

        // Act
        var result = detector.DetectFromPath("");

        // Assert
        result.Should().Be("unknown");
    }

    [Fact]
    public void DetectFromPath_NullPath_ReturnsUnknown()
    {
        // Arrange
        var detector = new ProjectDetector();

        // Act
        var result = detector.DetectFromPath(null!);

        // Assert
        result.Should().Be("unknown");
    }

    // ──────────────────────────────────────────────
    // DetectFromVsCodeTitle
    // ──────────────────────────────────────────────

    [Fact]
    public void DetectFromVsCodeTitle_StandardFormat_ReturnsProject()
    {
        // Arrange
        var detector = new ProjectDetector();
        var title = "app.py - myproject - Visual Studio Code";

        // Act
        var result = detector.DetectFromVsCodeTitle(title);

        // Assert
        result.Should().Be("myproject");
    }

    [Fact]
    public void DetectFromVsCodeTitle_FolderOnly_ReturnsFolderName()
    {
        // Arrange — VS Code with only a folder open, no file
        var detector = new ProjectDetector();
        var title = "myproject - Visual Studio Code";

        // Act
        var result = detector.DetectFromVsCodeTitle(title);

        // Assert
        result.Should().Be("myproject");
    }

    // ──────────────────────────────────────────────
    // DetectFromTerminalTitle
    // ──────────────────────────────────────────────

    [Fact]
    public void DetectFromTerminalTitle_StandardFormat_ReturnsProject()
    {
        // Arrange
        var detector = new ProjectDetector();
        var title = @"cmd.exe - C:\Users\name\project";

        // Act
        var result = detector.DetectFromTerminalTitle(title);

        // Assert — extracts the last path segment as the project name
        result.Should().Be("project");
    }

    [Fact]
    public void DetectFromTerminalTitle_NoPath_ReturnsProcessName()
    {
        // Arrange — no " - " delimiter or path, just the process name
        var detector = new ProjectDetector();
        var title = "cmd.exe";

        // Act
        var result = detector.DetectFromTerminalTitle(title);

        // Assert — falls back to returning the full input as the last segment
        result.Should().Be("cmd.exe");
    }

    // ──────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────

    /// <summary>
    /// Creates a uniquely-named temporary directory and registers it for cleanup.
    /// </summary>
    private string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), $"am_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        _tempDirs.Add(path);
        return path;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            try { Directory.Delete(dir, true); } catch { /* best-effort cleanup */ }
        }
    }
}
