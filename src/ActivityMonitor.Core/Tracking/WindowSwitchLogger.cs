using System.Timers;
using ActivityMonitor.Core.Interfaces;
using ActivityMonitor.Core.Models;
using Debug = System.Diagnostics.Debug;
using Timer = System.Timers.Timer;

namespace ActivityMonitor.Core.Tracking;

/// <summary>
/// 窗口切换日志记录器 — W1-M3。
/// 订阅 <see cref="IActivityTracker"/> 的 <see cref="IActivityTracker.OnActivityChanged"/>
/// 事件，每次前台窗口切换时记录一条 <see cref="OperationLog"/> 到 <see cref="IOperationLogRepository"/>。
///
/// <list type="bullet">
///   <item>独立于 <see cref="ActivityEngine"/> 的事件处理，记录离散的窗口切换事件而非持续活动。</item>
///   <item>批量写库：每 30 秒或缓存满 50 条落盘一次。</item>
///   <item>所有异常均被捕获，永不崩溃。</item>
/// </list>
/// </summary>
public sealed class WindowSwitchLogger : IDisposable
{
    // ── 依赖 ──
    private readonly IActivityTracker _tracker;
    private readonly IOperationLogRepository _repository;

    // ── 状态 ──
    private readonly List<OperationLog> _pendingBatch = new(capacity: 64);
    private readonly object _batchLock = new();
    private readonly Timer? _batchTimer;

    private bool _isRunning;
    private bool _disposed;

    // ── 配置 ──
    private const int BatchFlushIntervalMs = 30_000;
    private const int BatchMaxSize = 50;

    /// <summary>
    /// 初始化窗口切换日志记录器。
    /// </summary>
    /// <param name="tracker">前台窗口追踪器，用于订阅窗口切换事件。</param>
    /// <param name="repository">操作日志仓储，用于持久化日志。</param>
    /// <exception cref="ArgumentNullException">任一参数为 null 时抛出。</exception>
    public WindowSwitchLogger(IActivityTracker tracker, IOperationLogRepository repository)
    {
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));

        // 批量写入定时器
        _batchTimer = new Timer(BatchFlushIntervalMs) { AutoReset = true };
        _batchTimer.Elapsed += OnBatchTimerElapsed;
    }

    /// <summary>
    /// 当前是否正在运行。
    /// </summary>
    public bool IsRunning => _isRunning;

    // ──────────────────────────────────────────────
    // 生命周期
    // ──────────────────────────────────────────────

    /// <summary>
    /// 启动日志记录：订阅窗口切换事件并启动批量写入定时器。
    /// </summary>
    public void Start()
    {
        ThrowIfDisposed();
        if (_isRunning) return;

        _tracker.OnActivityChanged += OnWindowChanged;
        _batchTimer?.Start();
        _isRunning = true;

        Debug.WriteLine("[WindowSwitchLogger] Started");
    }

    /// <summary>
    /// 停止日志记录：取消订阅、刷新缓冲区、释放定时器。
    /// </summary>
    public async Task StopAsync()
    {
        if (!_isRunning) return;

        _tracker.OnActivityChanged -= OnWindowChanged;
        _batchTimer?.Stop();

        // 最终刷新
        await FlushBatchAsync();

        _isRunning = false;
        Debug.WriteLine("[WindowSwitchLogger] Stopped");
    }

    /// <summary>
    /// 手动触发一次缓冲刷新。
    /// </summary>
    public async Task FlushAsync()
    {
        await FlushBatchAsync();
    }

    // ──────────────────────────────────────────────
    // 事件处理
    // ──────────────────────────────────────────────

    private void OnWindowChanged(object? sender, ActivityEvent evt)
    {
        if (_disposed) return;

        try
        {
            var log = new OperationLog
            {
                Timestamp = evt.StartTime,
                WindowTitle = evt.RawWindowTitle ?? evt.WindowTitle,
                ProcessName = evt.ProcessName,
                ProcessId = evt.ProcessId,
                ProcessPath = evt.RawProcessPath ?? evt.ProcessPath,
                Category = evt.Category,
                Detail = evt.Detail ?? evt.Domain,
            };

            lock (_batchLock)
            {
                _pendingBatch.Add(log);

                if (_pendingBatch.Count >= BatchMaxSize)
                {
                    _ = FlushBatchAsync(); // fire-and-forget
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowSwitchLogger] Log error: {ex.Message}");
        }
    }

    // ──────────────────────────────────────────────
    // 批量写入
    // ──────────────────────────────────────────────

    private void OnBatchTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (_disposed) return;

        try
        {
            try { Thread.CurrentThread.Priority = ThreadPriority.BelowNormal; }
            catch { /* 非托管线程可能不支持设置优先级 */ }

            _ = FlushBatchAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowSwitchLogger] Batch timer error: {ex.Message}");
        }
    }

    private async Task FlushBatchAsync()
    {
        List<OperationLog> batch;

        lock (_batchLock)
        {
            if (_pendingBatch.Count == 0) return;
            batch = new List<OperationLog>(_pendingBatch);
            _pendingBatch.Clear();
        }

        try
        {
            await _repository.InsertBatchAsync(batch);
            Debug.WriteLine($"[WindowSwitchLogger] Flushed {batch.Count} logs");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowSwitchLogger] Flush error: {ex.Message}");
            // 写入失败时重新加入缓冲区（防止丢失）
            lock (_batchLock)
            {
                _pendingBatch.AddRange(batch);
            }
        }
    }

    // ──────────────────────────────────────────────
    // IDisposable
    // ──────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _tracker.OnActivityChanged -= OnWindowChanged;
        _batchTimer?.Stop();
        _batchTimer?.Dispose();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(WindowSwitchLogger));
    }
}
