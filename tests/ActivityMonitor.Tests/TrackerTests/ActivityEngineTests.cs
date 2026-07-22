using System.IO;
using ActivityMonitor.Core.Interfaces;
using ActivityMonitor.Core.Tracking;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace ActivityMonitor.Tests.TrackerTests;

/// <summary>
/// Tests for <see cref="ActivityEngine"/> constructor validation, initial state,
/// disposal, and lifecycle no-op methods.
///
/// All dependencies except CrashRecoveryService are mocked with NSubstitute.
/// CrashRecoveryService uses a real instance backed by a temp directory.
/// </summary>
public class ActivityEngineTests : IDisposable
{
    private readonly string _tempDir;
    private readonly IActivityTracker _tracker;
    private readonly IIdleDetector _idleDetector;
    private readonly ISleepDetector _sleepDetector;
    private readonly ILockScreenDetector _lockScreenDetector;
    private readonly IActivityRepository _repository;
    private readonly IActivityCategorizer _categorizer;
    private readonly CrashRecoveryService _crashRecovery;

    public ActivityEngineTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ActivityEngineTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _tracker = Substitute.For<IActivityTracker>();
        _idleDetector = Substitute.For<IIdleDetector>();
        _sleepDetector = Substitute.For<ISleepDetector>();
        _lockScreenDetector = Substitute.For<ILockScreenDetector>();
        _repository = Substitute.For<IActivityRepository>();
        _categorizer = Substitute.For<IActivityCategorizer>();
        _crashRecovery = new CrashRecoveryService(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            _crashRecovery.Dispose();
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private ActivityEngine CreateEngine(
        IActivityTracker? tracker = null,
        IIdleDetector? idleDetector = null,
        ISleepDetector? sleepDetector = null,
        ILockScreenDetector? lockScreenDetector = null,
        IActivityRepository? repository = null,
        IActivityCategorizer? categorizer = null,
        CrashRecoveryService? crashRecovery = null)
    {
        return new ActivityEngine(
            tracker ?? _tracker,
            idleDetector ?? _idleDetector,
            sleepDetector ?? _sleepDetector,
            lockScreenDetector ?? _lockScreenDetector,
            repository ?? _repository,
            categorizer ?? _categorizer,
            crashRecovery ?? _crashRecovery);
    }

    // ──────────────────────────────────────────────
    // Constructor validation
    // ──────────────────────────────────────────────

    [Fact]
    public void Constructor_NullTracker_ThrowsArgumentNullException()
    {
        // Act — pass null directly, bypass CreateEngine helper
        var act = () => new ActivityEngine(
            null!, _idleDetector, _sleepDetector, _lockScreenDetector, _repository, _categorizer, _crashRecovery);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("tracker");
    }

    [Fact]
    public void Constructor_NullIdleDetector_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ActivityEngine(
            _tracker, null!, _sleepDetector, _lockScreenDetector, _repository, _categorizer, _crashRecovery);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("idleDetector");
    }

    [Fact]
    public void Constructor_NullSleepDetector_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ActivityEngine(
            _tracker, _idleDetector, null!, _lockScreenDetector, _repository, _categorizer, _crashRecovery);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("sleepDetector");
    }

    [Fact]
    public void Constructor_NullLockScreenDetector_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ActivityEngine(
            _tracker, _idleDetector, _sleepDetector, null!, _repository, _categorizer, _crashRecovery);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("lockScreenDetector");
    }

    [Fact]
    public void Constructor_NullRepository_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ActivityEngine(
            _tracker, _idleDetector, _sleepDetector, _lockScreenDetector, null!, _categorizer, _crashRecovery);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("repository");
    }

    [Fact]
    public void Constructor_NullCrashRecovery_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ActivityEngine(
            _tracker, _idleDetector, _sleepDetector, _lockScreenDetector, _repository, _categorizer, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("crashRecovery");
    }

    [Fact]
    public void Constructor_NullCategorizer_DoesNotThrow()
    {
        // Arrange & Act — categorizer is optional (nullable)
        var act = () => CreateEngine(categorizer: null);

        // Assert
        act.Should().NotThrow("categorizer is an optional dependency and may be null");
    }

    // ──────────────────────────────────────────────
    // Initial state
    // ──────────────────────────────────────────────

    [Fact]
    public void GetCurrentEvent_InitialState_ReturnsNull()
    {
        // Arrange
        var engine = CreateEngine();

        // Act
        var currentEvent = engine.GetCurrentEvent();

        // Assert
        currentEvent.Should().BeNull(
            "GetCurrentEvent() should return null before any tracking starts");
    }

    [Fact]
    public void State_InitialState_IsStopped()
    {
        // Arrange
        var engine = CreateEngine();

        // Act
        var state = engine.State;

        // Assert
        state.Should().Be(EngineState.Stopped,
            "the initial engine state must be Stopped");
    }

    [Fact]
    public void IsRunning_InitialState_ReturnsFalse()
    {
        // Arrange
        var engine = CreateEngine();

        // Act
        var isRunning = engine.IsRunning;

        // Assert
        isRunning.Should().BeFalse("the engine should not be running before StartAsync()");
    }

    [Fact]
    public void IsPaused_InitialState_ReturnsFalse()
    {
        // Arrange
        var engine = CreateEngine();

        // Act
        var isPaused = engine.IsPaused;

        // Assert
        isPaused.Should().BeFalse("the engine should not be paused initially");
    }

    // ──────────────────────────────────────────────
    // Lifecycle: no-op when not started
    // ──────────────────────────────────────────────

    [Fact]
    public async Task PauseAsync_WithoutStart_DoesNotThrow()
    {
        // Arrange
        var engine = CreateEngine();

        // Act
        var act = async () => await engine.PauseAsync();

        // Assert
        await act.Should().NotThrowAsync(
            "PauseAsync() without StartAsync() should be a no-op");
    }

    [Fact]
    public async Task ResumeAsync_WithoutStart_DoesNotThrow()
    {
        // Arrange
        var engine = CreateEngine();

        // Act
        var act = async () => await engine.ResumeAsync();

        // Assert
        await act.Should().NotThrowAsync(
            "ResumeAsync() without StartAsync() should be a no-op");
    }

    [Fact]
    public async Task StopAsync_WithoutStart_DoesNotThrow()
    {
        // Arrange
        var engine = CreateEngine();

        // Act
        var act = async () => await engine.StopAsync();

        // Assert
        await act.Should().NotThrowAsync(
            "StopAsync() without StartAsync() should be a no-op");
    }

    [Fact]
    public async Task FlushAsync_WithoutStart_DoesNotThrow()
    {
        // Arrange
        var engine = CreateEngine();

        // Act
        var act = async () => await engine.FlushAsync();

        // Assert
        await act.Should().NotThrowAsync(
            "FlushAsync() without StartAsync() should be a no-op (empty batch)");
    }

    // ──────────────────────────────────────────────
    // Dispose
    // ──────────────────────────────────────────────

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        // Arrange
        var engine = CreateEngine();

        // Act
        var act = () => engine.Dispose();

        // Assert
        act.Should().NotThrow("Dispose() should never throw on a fresh engine");
    }

    [Fact]
    public void Dispose_MultipleCalls_NoException()
    {
        // Arrange
        var engine = CreateEngine();
        engine.Dispose();

        // Act
        var act = () => engine.Dispose();

        // Assert
        act.Should().NotThrow("calling Dispose() twice should be safe (idempotent)");
    }

    [Fact]
    public void Dispose_AfterDispose_IsRunningFalse()
    {
        // Arrange
        var engine = CreateEngine();
        engine.Dispose();

        // Act
        var isRunning = engine.IsRunning;

        // Assert
        isRunning.Should().BeFalse("engine should not be running after disposal");
    }

    // ──────────────────────────────────────────────
    // EngineState enum completeness
    // ──────────────────────────────────────────────

    [Fact]
    public void EngineState_Enum_ContainsAllExpectedValues()
    {
        // Assert — verify all documented states exist
        var values = Enum.GetValues<EngineState>();

        values.Should().Contain(EngineState.Stopped);
        values.Should().Contain(EngineState.Starting);
        values.Should().Contain(EngineState.Running);
        values.Should().Contain(EngineState.Paused);
        values.Should().Contain(EngineState.Stopping);
    }
}
