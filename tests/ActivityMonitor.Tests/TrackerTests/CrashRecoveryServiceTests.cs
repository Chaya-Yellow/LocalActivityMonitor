using System.IO;
using ActivityMonitor.Core.Tracking;
using FluentAssertions;
using Xunit;

namespace ActivityMonitor.Tests.TrackerTests;

/// <summary>
/// Tests for <see cref="CrashRecoveryService"/> — exit marker management,
/// crash detection, and recovery event construction.
///
/// Uses isolated temp directories to avoid cross-test pollution.
/// </summary>
public class CrashRecoveryServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _markerFilePath;

    public CrashRecoveryServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"CrashRecoveryTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _markerFilePath = Path.Combine(_tempDir, "exit_marker.txt");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private CrashRecoveryService CreateService()
    {
        return new CrashRecoveryService(_tempDir);
    }

    // ──────────────────────────────────────────────
    // CheckAsync
    // ──────────────────────────────────────────────

    [Fact]
    public async Task CheckAsync_MarkerFileExists_ReturnsWasCrashedFalse()
    {
        // Arrange — write a clean-exit marker
        var service = CreateService();
        await service.MarkGracefulExitAsync();
        File.Exists(_markerFilePath).Should().BeTrue("marker file should exist after graceful exit");

        // Act
        var result = await service.CheckAsync();

        // Assert
        result.WasCrashed.Should().BeFalse(
            "when the exit marker exists, the previous shutdown was clean (not a crash)");
        result.LastEventEndTime.Should().NotBeNull(
            "the exit timestamp should be parsed from the marker file");
    }

    [Fact]
    public async Task CheckAsync_MarkerFileMissing_ReturnsWasCrashedTrue()
    {
        // Arrange — ensure no marker file exists
        var service = CreateService();
        if (File.Exists(_markerFilePath))
            File.Delete(_markerFilePath);
        File.Exists(_markerFilePath).Should().BeFalse("no marker file should exist before the check");

        // Act
        var result = await service.CheckAsync();

        // Assert
        result.WasCrashed.Should().BeTrue(
            "when the exit marker is missing, the previous shutdown was a crash");
    }

    // ──────────────────────────────────────────────
    // MarkGracefulExitAsync
    // ──────────────────────────────────────────────

    [Fact]
    public async Task MarkGracefulExitAsync_CreatesMarkerFile()
    {
        // Arrange
        var service = CreateService();
        File.Exists(_markerFilePath).Should().BeFalse("marker should not exist before call");

        // Act
        await service.MarkGracefulExitAsync();

        // Assert
        File.Exists(_markerFilePath).Should().BeTrue(
            "MarkGracefulExitAsync must create the exit marker file on disk");
    }

    [Fact]
    public async Task MarkGracefulExitAsync_WithSpecificTime_WritesTimestamp()
    {
        // Arrange
        var service = CreateService();
        var timestamp = new DateTime(2026, 7, 21, 14, 30, 0, DateTimeKind.Local);

        // Act
        await service.MarkGracefulExitAsync(timestamp);

        // Assert
        File.Exists(_markerFilePath).Should().BeTrue();
        var content = await File.ReadAllTextAsync(_markerFilePath);
        content.Trim().Should().Contain("2026-07-21",
            "the marker file should contain the specified timestamp in ISO 8601 format");
    }

    // ──────────────────────────────────────────────
    // BuildRecoveryEvents
    // ──────────────────────────────────────────────

    [Fact]
    public void BuildRecoveryEvents_LastEventWithinWindow_ReturnsRecoveryEvent()
    {
        // Arrange — last event ended 2 minutes ago (within the 5-minute recovery window)
        var service = CreateService();
        var lastEventEndTime = DateTime.Now.AddMinutes(-2);
        var recoveryStartTime = DateTime.Now.AddMinutes(-5);

        // Act
        var events = service.BuildRecoveryEvents(lastEventEndTime, recoveryStartTime);

        // Assert
        events.Should().HaveCount(1,
            "when the last event is within the recovery window, a single recovery event is created");
        events[0].IsCrashRecovered.Should().BeTrue(
            "the recovery event must be marked as crash-recovered");
        events[0].StartTime.Should().Be(lastEventEndTime,
            "the recovery event starts at the last event's end time");
        events[0].Category.Should().Be("app",
            "the recovery event within window should be categorized as app");
    }

    [Fact]
    public void BuildRecoveryEvents_LastEventBeforeWindow_ReturnsGapAndRecovery()
    {
        // Arrange — last event ended 10 minutes ago (before the 5-minute recovery window)
        var service = CreateService();
        var lastEventEndTime = DateTime.Now.AddMinutes(-10);
        var recoveryStartTime = DateTime.Now.AddMinutes(-5);

        // Act
        var events = service.BuildRecoveryEvents(lastEventEndTime, recoveryStartTime);

        // Assert
        events.Should().HaveCount(2,
            "when the last event is before the recovery window, a gap event and a recovery event are created");

        // First event: idle gap between recoveryStartTime and lastEventEndTime
        events[0].IsCrashRecovered.Should().BeTrue();
        events[0].StartTime.Should().Be(recoveryStartTime,
            "the gap event starts at the recovery window start");
        events[0].EndTime.Should().Be(lastEventEndTime,
            "the gap event ends at the last event's end time");
        events[0].Category.Should().Be("idle",
            "the gap between recovery window and last event should be classified as idle");
        events[0].Detail.Should().Contain("Gap",
            "the gap event detail should indicate a recovery gap");

        // Second event: app recovery from lastEventEndTime to now
        events[1].IsCrashRecovered.Should().BeTrue();
        events[1].StartTime.Should().Be(lastEventEndTime,
            "the recovery event starts at the last event's end time");
        events[1].Category.Should().Be("app",
            "the recovery event should be categorized as app");
    }

    [Fact]
    public void BuildRecoveryEvents_ExactlyAtBoundary_ReturnsSingleEvent()
    {
        // Arrange — lastEventEndTime equals recoveryStartTime (boundary case)
        var service = CreateService();
        var boundary = DateTime.Now.AddMinutes(-3);
        var lastEventEndTime = boundary;
        var recoveryStartTime = boundary;

        // Act
        var events = service.BuildRecoveryEvents(lastEventEndTime, recoveryStartTime);

        // Assert
        events.Should().HaveCount(1,
            "when lastEventEndTime >= recoveryStartTime, a single event is returned (the 'within window' branch)");
    }

    // ──────────────────────────────────────────────
    // ClearMarkerAsync
    // ──────────────────────────────────────────────

    [Fact]
    public async Task ClearMarkerAsync_ExistingFile_RemovesFile()
    {
        // Arrange — create the marker file
        var service = CreateService();
        await service.MarkGracefulExitAsync();
        File.Exists(_markerFilePath).Should().BeTrue("marker should exist before clearing");

        // Act
        await service.ClearMarkerAsync();

        // Assert
        File.Exists(_markerFilePath).Should().BeFalse(
            "ClearMarkerAsync must delete the exit marker file");
    }

    [Fact]
    public async Task ClearMarkerAsync_NoFile_DoesNotThrow()
    {
        // Arrange — ensure no marker exists
        var service = CreateService();
        if (File.Exists(_markerFilePath))
            File.Delete(_markerFilePath);

        // Act
        var act = async () => await service.ClearMarkerAsync();

        // Assert
        await act.Should().NotThrowAsync(
            "ClearMarkerAsync when no marker file exists should be a no-op");
    }

    // ──────────────────────────────────────────────
    // Dispose
    // ──────────────────────────────────────────────

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        // Arrange
        var service = CreateService();

        // Act
        var act = () => service.Dispose();

        // Assert
        act.Should().NotThrow("Dispose() on CrashRecoveryService should never throw");
    }
}
