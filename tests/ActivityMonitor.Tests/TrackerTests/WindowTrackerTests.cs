using ActivityMonitor.Core.Tracking;
using FluentAssertions;
using Xunit;

namespace ActivityMonitor.Tests.TrackerTests;

/// <summary>
/// Tests for <see cref="WindowTracker"/> lifecycle and configuration.
///
/// The real WindowTracker calls NativeMethods.GetForegroundWindow() directly
/// which cannot be mocked. These tests validate the public API surface
/// (lifecycle, properties, disposal) which works on any Windows machine.
/// </summary>
public class WindowTrackerTests
{
    // ──────────────────────────────────────────────
    // Lifecycle: Start / Stop
    // ──────────────────────────────────────────────

    [Fact]
    public void Start_IsRunning_ReturnsTrue()
    {
        // Arrange
        using var tracker = new WindowTracker();

        // Act
        tracker.Start();

        // Assert
        tracker.IsRunning.Should().BeTrue("tracker should be running after Start()");
    }

    [Fact]
    public void Stop_IsRunning_ReturnsFalse()
    {
        // Arrange
        using var tracker = new WindowTracker();
        tracker.Start();

        // Act
        tracker.Stop();

        // Assert
        tracker.IsRunning.Should().BeFalse("tracker should not be running after Stop()");
    }

    [Fact]
    public void Start_StartTwice_NoOp()
    {
        // Arrange
        using var tracker = new WindowTracker();
        tracker.Start();

        // Act
        var act = () => tracker.Start();

        // Assert
        act.Should().NotThrow("calling Start() twice should be a no-op, not an exception");
        tracker.IsRunning.Should().BeTrue("tracker should remain running after second Start()");
    }

    [Fact]
    public void Stop_StopTwice_NoOp()
    {
        // Arrange
        using var tracker = new WindowTracker();
        tracker.Start();
        tracker.Stop();

        // Act
        var act = () => tracker.Stop();

        // Assert
        act.Should().NotThrow("calling Stop() twice should be a no-op, not an exception");
        tracker.IsRunning.Should().BeFalse();
    }

    // ──────────────────────────────────────────────
    // PollIntervalMs
    // ──────────────────────────────────────────────

    [Fact]
    public void PollIntervalMs_DefaultIs2000()
    {
        // Arrange
        using var tracker = new WindowTracker();

        // Act
        var interval = tracker.PollIntervalMs;

        // Assert
        interval.Should().Be(2000, "the default poll interval must be 2000 ms");
    }

    [Fact]
    public void PollIntervalMs_SetValue_ReflectsChange()
    {
        // Arrange
        using var tracker = new WindowTracker();

        // Act
        tracker.PollIntervalMs = 5000;

        // Assert
        tracker.PollIntervalMs.Should().Be(5000, "the getter should return the value set");
    }

    [Fact]
    public void PollIntervalMs_Minimum500_ClampsLowerValues()
    {
        // Arrange
        using var tracker = new WindowTracker();

        // Act
        tracker.PollIntervalMs = 100;

        // Assert
        tracker.PollIntervalMs.Should().Be(500,
            "values below 500 ms must be clamped to the minimum of 500 ms");
    }

    // ──────────────────────────────────────────────
    // Pause / Resume
    // ──────────────────────────────────────────────

    [Fact]
    public void Pause_IsPaused_ReturnsTrue()
    {
        // Arrange
        using var tracker = new WindowTracker();
        tracker.Start();

        // Act
        tracker.Pause();

        // Assert
        tracker.IsPaused.Should().BeTrue("tracker should be paused after Pause()");
    }

    [Fact]
    public void Resume_AfterPause_IsPausedFalse()
    {
        // Arrange
        using var tracker = new WindowTracker();
        tracker.Start();
        tracker.Pause();

        // Act
        tracker.Resume();

        // Assert
        tracker.IsPaused.Should().BeFalse("tracker should not be paused after Resume()");
    }

    [Fact]
    public void Pause_WithoutStart_NoOp()
    {
        // Arrange
        using var tracker = new WindowTracker();

        // Act
        var act = () => tracker.Pause();

        // Assert
        act.Should().NotThrow("calling Pause() without Start() should be a no-op");
    }

    [Fact]
    public void Resume_WithoutStart_NoOp()
    {
        // Arrange
        using var tracker = new WindowTracker();

        // Act
        var act = () => tracker.Resume();

        // Assert
        act.Should().NotThrow("calling Resume() without Start() should be a no-op");
    }

    // ──────────────────────────────────────────────
    // Dispose
    // ──────────────────────────────────────────────

    [Fact]
    public void Dispose_StopsCleanly()
    {
        // Arrange
        var tracker = new WindowTracker();
        tracker.Start();

        // Act
        var act = () => tracker.Dispose();

        // Assert
        act.Should().NotThrow("Dispose() should never throw");
        tracker.IsRunning.Should().BeFalse("tracker should be stopped after Dispose()");
    }

    [Fact]
    public void Dispose_DoubleDispose_NoException()
    {
        // Arrange
        var tracker = new WindowTracker();
        tracker.Dispose();

        // Act
        var act = () => tracker.Dispose();

        // Assert
        act.Should().NotThrow("calling Dispose() twice should be safe");
    }
}
