using ActivityMonitor.Core.Interfaces;
using ActivityMonitor.Core.Models;
using ActivityMonitor.Core.Tracking;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace ActivityMonitor.Tests.TrackerTests;

/// <summary>
/// WindowSwitchLogger tests — W1-M3 窗口切换日志记录器。
/// 使用 NSubstitute Mock 跟踪器和仓储，验证事件订阅、批量写入、生命周期管理。
/// </summary>
public class WindowSwitchLoggerTests
{
    private readonly IActivityTracker _tracker;
    private readonly IOperationLogRepository _repository;

    public WindowSwitchLoggerTests()
    {
        _tracker = Substitute.For<IActivityTracker>();
        _repository = Substitute.For<IOperationLogRepository>();
    }

    private WindowSwitchLogger CreateLogger() =>
        new(_tracker, _repository);

    // Helper: raise the EventHandler{ActivityEvent} event on the tracker mock
    private void RaiseActivityChanged(ActivityEvent evt)
    {
        // EventHandler<ActivityEvent> has no EventArgs constraint in .NET 8
        _tracker.OnActivityChanged += Raise.Event<EventHandler<ActivityEvent>>(_tracker, evt);
    }

    // ──────────────────────────────────────────────
    // Constructor validation
    // ──────────────────────────────────────────────

    [Fact]
    public void Constructor_NullTracker_ThrowsArgumentNullException()
    {
        var act = () => new WindowSwitchLogger(null!, _repository);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("tracker");
    }

    [Fact]
    public void Constructor_NullRepository_ThrowsArgumentNullException()
    {
        var act = () => new WindowSwitchLogger(_tracker, null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("repository");
    }

    // ──────────────────────────────────────────────
    // Start / Stop lifecycle
    // ──────────────────────────────────────────────

    [Fact]
    public void Start_FirstTime_SetsIsRunningTrue()
    {
        using var logger = CreateLogger();

        logger.Start();

        logger.IsRunning.Should().BeTrue();
    }

    [Fact]
    public void Start_CalledTwice_DoesNotSubscribeDuplicate()
    {
        using var logger = CreateLogger();

        logger.Start();
        logger.Start(); // second call is no-op

        logger.IsRunning.Should().BeTrue();
    }

    [Fact]
    public async Task StopAsync_WhenRunning_SetsIsRunningFalse()
    {
        using var logger = CreateLogger();
        logger.Start();

        await logger.StopAsync();

        logger.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task StopAsync_WithoutStart_DoesNotThrow()
    {
        using var logger = CreateLogger();

        var act = async () => await logger.StopAsync();

        await act.Should().NotThrowAsync();
    }

    // ──────────────────────────────────────────────
    // Event subscription — window switch triggers log
    // ──────────────────────────────────────────────

    [Fact]
    public async Task OnWindowChanged_WhenRunning_AddsToBatch()
    {
        using var logger = CreateLogger();
        logger.Start();

        RaiseActivityChanged(new ActivityEvent
        {
            StartTime = new DateTime(2026, 7, 22, 10, 0, 0),
            WindowTitle = "Terminal - Windows PowerShell",
            RawWindowTitle = "Windows PowerShell",
            ProcessName = "powershell.exe",
            ProcessId = 5678,
            ProcessPath = @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe",
            Category = Category.App,
        });

        // Forced flush to trigger batch write (timer runs at 30s intervals)
        await logger.FlushAsync();

        _repository.Received(1).InsertBatchAsync(Arg.Is<IEnumerable<OperationLog>>(
            logs => logs.Count() == 1 && logs.First().ProcessName == "powershell.exe"));
    }

    [Fact]
    public async Task OnWindowChanged_MultipleEvents_BatchesTogether()
    {
        using var logger = CreateLogger();
        logger.Start();

        for (int i = 0; i < 3; i++)
        {
            RaiseActivityChanged(new ActivityEvent
            {
                StartTime = new DateTime(2026, 7, 22, 10, i, 0),
                WindowTitle = $"Window {i}",
                ProcessName = $"proc_{i}.exe",
                Category = Category.App,
            });
        }

        await logger.FlushAsync();

        _repository.Received(1).InsertBatchAsync(Arg.Is<IEnumerable<OperationLog>>(
            logs => logs.Count() == 3));
    }

    // ──────────────────────────────────────────────
    // FlushAsync — manual flush
    // ──────────────────────────────────────────────

    [Fact]
    public async Task FlushAsync_WithPendingBatch_FlushesToRepository()
    {
        using var logger = CreateLogger();
        logger.Start();

        RaiseActivityChanged(new ActivityEvent
        {
            StartTime = DateTime.UtcNow,
            WindowTitle = "Test",
            ProcessName = "test.exe",
            Category = Category.App,
        });

        await logger.FlushAsync();

        _repository.Received(1).InsertBatchAsync(Arg.Is<IEnumerable<OperationLog>>(
            logs => logs.Count() == 1));
    }

    [Fact]
    public async Task FlushAsync_EmptyBatch_DoesNotCallRepository()
    {
        using var logger = CreateLogger();
        logger.Start();

        await logger.FlushAsync();

        _repository.DidNotReceiveWithAnyArgs().InsertBatchAsync(Arg.Any<IEnumerable<OperationLog>>());
    }

    // ──────────────────────────────────────────────
    // Dispose
    // ──────────────────────────────────────────────

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var act = () =>
        {
            using var logger = CreateLogger();
            logger.Start();
        };
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_AfterDispose_StopsTimer()
    {
        var logger = CreateLogger();
        logger.Start();
        logger.Dispose();

        // After Dispose, Start() throws ObjectDisposedException
        var act = () => logger.Start();
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Start_AfterDispose_ThrowsObjectDisposedException()
    {
        var logger = CreateLogger();
        logger.Dispose();

        var act = () => logger.Start();

        act.Should().Throw<ObjectDisposedException>();
    }

    // ──────────────────────────────────────────────
    // Event details mapping — ActivityEvent → OperationLog
    // ──────────────────────────────────────────────

    [Fact]
    public async Task OnWindowChanged_RawWindowTitle_UsedWhenPresent()
    {
        using var logger = CreateLogger();
        logger.Start();

        RaiseActivityChanged(new ActivityEvent
        {
            StartTime = new DateTime(2026, 7, 22, 14, 0, 0),
            WindowTitle = "Cached Title",
            RawWindowTitle = "Raw Title",
            ProcessName = "test.exe",
            Category = Category.App,
        });

        await logger.FlushAsync();

        _repository.Received(1).InsertBatchAsync(Arg.Is<IEnumerable<OperationLog>>(
            logs => logs.First().WindowTitle == "Raw Title"));
    }

    [Fact]
    public async Task OnWindowChanged_NoRawWindowTitle_FallsBackToWindowTitle()
    {
        using var logger = CreateLogger();
        logger.Start();

        RaiseActivityChanged(new ActivityEvent
        {
            StartTime = new DateTime(2026, 7, 22, 14, 30, 0),
            WindowTitle = "Fallback Title",
            RawWindowTitle = null,
            ProcessName = "test.exe",
            Category = Category.App,
        });

        await logger.FlushAsync();

        _repository.Received(1).InsertBatchAsync(Arg.Is<IEnumerable<OperationLog>>(
            logs => logs.First().WindowTitle == "Fallback Title"));
    }

    [Fact]
    public async Task OnWindowChanged_DomainDetail_FillsDetailWhenPresent()
    {
        using var logger = CreateLogger();
        logger.Start();

        RaiseActivityChanged(new ActivityEvent
        {
            StartTime = new DateTime(2026, 7, 22, 15, 0, 0),
            WindowTitle = "GitHub - Chrome",
            ProcessName = "chrome.exe",
            Category = Category.Web,
            Domain = "github.com",
            Detail = null,
        });

        await logger.FlushAsync();

        _repository.Received(1).InsertBatchAsync(Arg.Is<IEnumerable<OperationLog>>(
            logs => logs.First().Detail == "github.com"));
    }
}
