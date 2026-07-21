using ActivityMonitor.Core.Tracking;
using FluentAssertions;
using Xunit;

namespace ActivityMonitor.Tests.TrackerTests;

/// <summary>
/// Tests for <see cref="IdleDetector"/> lifecycle, configuration, and property accessors.
///
/// The real IdleDetector calls NativeMethods.GetLastInputInfo() directly
/// which cannot be mocked. These tests validate the public API surface
/// (lifecycle, properties, disposal) which works on any Windows machine.
/// </summary>
public class IdleDetectorTests
{
    // ──────────────────────────────────────────────
    // Lifecycle: Start / Stop
    // ──────────────────────────────────────────────

    [Fact]
    public void Start_IsIdle_FalseInitially()
    {
        // Arrange
        using var detector = new IdleDetector();

        // Act
        detector.Start();

        // Assert
        // On a real Windows machine the user is active, so IsIdle should be false
        // after the initial sample. The Start() method calls CheckIdleState()
        // internally which compares IdleSinceMs against the threshold.
        detector.IsIdle.Should().BeFalse(
            "on an active test machine, the user has recent input; IsIdle should be false");
    }

    [Fact]
    public void Stop_StopsCleanly()
    {
        // Arrange
        using var detector = new IdleDetector();
        detector.Start();

        // Act
        var act = () => detector.Stop();

        // Assert
        act.Should().NotThrow("Stop() should never throw after a valid Start()");
    }

    [Fact]
    public void Start_StartTwice_NoOp()
    {
        // Arrange
        using var detector = new IdleDetector();
        detector.Start();

        // Act
        var act = () => detector.Start();

        // Assert
        act.Should().NotThrow("calling Start() twice should be a no-op, not an exception");
    }

    [Fact]
    public void Stop_StopTwice_NoOp()
    {
        // Arrange
        using var detector = new IdleDetector();
        detector.Start();
        detector.Stop();

        // Act
        var act = () => detector.Stop();

        // Assert
        act.Should().NotThrow("calling Stop() twice should be a no-op");
    }

    // ──────────────────────────────────────────────
    // IdleThresholdMs
    // ──────────────────────────────────────────────

    [Fact]
    public void IdleThresholdMs_DefaultIs900000()
    {
        // Arrange
        using var detector = new IdleDetector();

        // Act
        var threshold = detector.IdleThresholdMs;

        // Assert
        threshold.Should().Be(900_000L,
            "the default idle threshold must be 900,000 ms (15 minutes)");
    }

    [Fact]
    public void IdleThresholdMs_SetValue_ReflectsChange()
    {
        // Arrange
        using var detector = new IdleDetector();

        // Act
        detector.IdleThresholdMs = 60_000L;

        // Assert
        detector.IdleThresholdMs.Should().Be(60_000L,
            "the getter should return the value set");
    }

    // ──────────────────────────────────────────────
    // CheckNow
    // ──────────────────────────────────────────────

    [Fact]
    public void CheckNow_DoesNotThrow()
    {
        // Arrange
        using var detector = new IdleDetector();
        detector.Start();

        // Act
        var act = () => detector.CheckNow();

        // Assert
        act.Should().NotThrow("CheckNow() should never throw on an active machine");
    }

    [Fact]
    public void CheckNow_BeforeStart_DoesNotThrow()
    {
        // Arrange
        using var detector = new IdleDetector();

        // Act
        var act = () => detector.CheckNow();

        // Assert
        act.Should().NotThrow("CheckNow() before Start() should not throw — it's guarded by _disposed check");
    }

    // ──────────────────────────────────────────────
    // Dispose
    // ──────────────────────────────────────────────

    [Fact]
    public void Dispose_StopsCleanly()
    {
        // Arrange
        var detector = new IdleDetector();
        detector.Start();

        // Act
        var act = () => detector.Dispose();

        // Assert
        act.Should().NotThrow("Dispose() should never throw");
    }

    [Fact]
    public void Dispose_DoubleDispose_NoException()
    {
        // Arrange
        var detector = new IdleDetector();
        detector.Dispose();

        // Act
        var act = () => detector.Dispose();

        // Assert
        act.Should().NotThrow("calling Dispose() twice should be safe");
    }

    [Fact]
    public void Start_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var detector = new IdleDetector();
        detector.Dispose();

        // Act
        var act = () => detector.Start();

        // Assert
        act.Should().Throw<ObjectDisposedException>(
            "calling Start() after Dispose() must throw ObjectDisposedException");
    }
}
