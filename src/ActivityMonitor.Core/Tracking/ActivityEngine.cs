using System.Timers;
using ActivityMonitor.Core.Interfaces;
using ActivityMonitor.Core.Models;
using ActivityMonitor.Core.Win32;
using Debug = System.Diagnostics.Debug;
using Timer = System.Timers.Timer;

namespace ActivityMonitor.Core.Tracking;

/// <summary>
/// 活动引擎 — 核心协调器。
/// 统筹 <see cref="WindowTracker"/>、<see cref="IdleDetector"/>、<see cref="SleepDetector"/>、
/// <see cref="CrashRecoveryService"/>，负责整个追踪生命周期。
/// <list type="bullet">
///   <item>10 秒启动延迟，让系统开机阶段优先完成其他加载。</item>
///   <item>批量写库：每 30 秒或缓存满 50 条落盘一次。</item>
///   <item>空闲状态自动降频、恢复后延续上一条记录 (is_continued=true)。</item>
///   <item>异常全部 catch，永不崩溃。</item>
/// </list>
/// </summary>
public sealed class ActivityEngine : IDisposable
{
    // ── 依赖 ──
    private readonly IActivityTracker _tracker;
    private readonly IIdleDetector _idleDetector;
    private readonly ISleepDetector _sleepDetector;
    private readonly IActivityRepository _repository;
    private readonly IActivityCategorizer? _categorizer;
    private readonly CrashRecoveryService _crashRecovery;
    private readonly WindowSwitchLogger _windowSwitchLogger;

    // ── 状态 ──
    private ActivityEvent? _currentEvent;
    private ActivityEvent? _lastActiveEvent;    // 空闲前保存，用于延续
    private readonly List<ActivityEvent> _pendingBatch = new(capacity: 64);
    private readonly object _batchLock = new();
    private DateTime _lastFlushTime = DateTime.MinValue;

    private Timer? _batchTimer;
    private CancellationTokenSource? _engineCts;
    private bool _isRunning;
    private bool _isPaused;
    private bool _isIdle;
    private bool _isSleeping;
    private bool _disposed;

    // ── 暂停时段历史（供日报调用） ──
    private readonly List<PauseSegment> _pauseHistory = new();
    private DateTime? _pauseStartTime;

    // ── 配置 ──
    private const int StartupDelayMs = 10_000;
    private const int BatchFlushIntervalMs = 30_000;
    private const int BatchMaxSize = 50;
    private const int RecoveryWindowMinutes = 5;

    /// <summary>引擎状态变更事件。</summary>
    public event EventHandler<EngineState>? OnEngineStateChanged;

    /// <summary>空闲状态变更事件（W0-M2: Dashboard 刷新降频联动）。</summary>
    public event EventHandler<bool>? IdleStateChanged;

    /// <summary>睡眠状态变更事件（W0-M2: 唤醒后恢复刷新）。</summary>
    public event EventHandler<bool>? SleepStateChanged;

    /// <summary>当前引擎状态。</summary>
    public EngineState State { get; private set; } = EngineState.Stopped;

    /// <summary>当前是否在运行。</summary>
    public bool IsRunning => _isRunning;

    /// <summary>当前是否暂停。</summary>
    public bool IsPaused => _isPaused;

    /// <summary>当前是否空闲。</summary>
    public bool IsIdle => _isIdle;

    /// <summary>当前是否睡眠。</summary>
    public bool IsSleeping => _isSleeping;

    /// <summary>
    /// 暂停时段历史记录（供日报调用，F11.8）。
    /// </summary>
    public IReadOnlyList<PauseSegment> PauseHistory => _pauseHistory.AsReadOnly();

    // ──────────────────────────────────────────────
    // 构造函数
    // ──────────────────────────────────────────────

    /// <summary>
    /// 初始化活动引擎。
    /// </summary>
    /// <param name="tracker">前台窗口追踪器（WindowTracker）。</param>
    /// <param name="idleDetector">空闲检测器。</param>
    /// <param name="sleepDetector">睡眠检测器。</param>
    /// <param name="repository">活动事件仓储。</param>
    /// <param name="categorizer">活动分类器（可选）。</param>
    /// <param name="crashRecovery">崩溃恢复服务。</param>
    /// <param name="operationLogRepository">操作日志仓储（可选）。提供时启用 W1-M3 窗口切换日志。</param>
    public ActivityEngine(
        IActivityTracker tracker,
        IIdleDetector idleDetector,
        ISleepDetector sleepDetector,
        IActivityRepository repository,
        IActivityCategorizer? categorizer,
        CrashRecoveryService crashRecovery,
        IOperationLogRepository? operationLogRepository = null)
    {
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
        _idleDetector = idleDetector ?? throw new ArgumentNullException(nameof(idleDetector));
        _sleepDetector = sleepDetector ?? throw new ArgumentNullException(nameof(sleepDetector));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _categorizer = categorizer;
        _crashRecovery = crashRecovery ?? throw new ArgumentNullException(nameof(crashRecovery));

        _windowSwitchLogger = operationLogRepository != null
            ? new WindowSwitchLogger(_tracker, operationLogRepository)
            : null!; // null 时跳过窗口切换日志
    }

    // ──────────────────────────────────────────────
    // 生命周期
    // ──────────────────────────────────────────────

    /// <summary>
    /// 启动引擎。包含 10 秒启动延迟和崩溃恢复检测。
    /// </summary>
    public async Task StartAsync()
    {
        ThrowIfDisposed();
        if (_isRunning) return;

        _engineCts = new CancellationTokenSource();
        var token = _engineCts.Token;

        SetState(EngineState.Starting);

        try
        {
            // ── Step 1: 崩溃恢复检测 ──
            await PerformCrashRecoveryAsync();

            // W0-M3: 检查上次是否因崩溃处于暂停状态
            bool wasPaused = await _crashRecovery.WasPausedOnCrashAsync();

            // ── Step 2: 启动延迟 ──
            Debug.WriteLine("[Engine] Startup delay 10s...");
            try
            {
                await Task.Delay(StartupDelayMs, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            // ── Step 3: 订阅事件 ──
            _tracker.OnActivityChanged += OnTrackerActivityChanged;
            _idleDetector.OnIdleStateChanged += OnIdleStateChanged;
            _sleepDetector.OnSleepStateChanged += OnSleepStateChanged;

            // ── Step 4: 启动子组件 ──
            // 空闲和睡眠检测器始终启动（即便处于暂停状态也要监听）
            _idleDetector.Start();
            _sleepDetector.Start();

            // W0-M3: 暂停态崩溃恢复 → 启动但不激活追踪器
            if (wasPaused)
            {
                // 崩溃发生在暂停状态 → 保持暂停，不启动追踪器
                _isRunning = true;
                _isPaused = true;
                _pauseStartTime = DateTime.Now; // 从此刻开始计时
                SetState(EngineState.Paused);
                Debug.WriteLine("[Engine] Started in paused state (crash recovery)");
            }
            else
            {
                // 正常启动 → 启动追踪器
                _tracker.Start();
            }

            // ── Step 5: 批量写入定时器 ──
            _batchTimer = new Timer(BatchFlushIntervalMs) { AutoReset = true };
            _batchTimer.Elapsed += OnBatchTimerElapsed;
            _batchTimer.Start();

            // ── Step 6: 启动窗口切换日志记录器 (W1-M3) ──
            if (_windowSwitchLogger != null)
            {
                _windowSwitchLogger.Start();
            }

            if (!wasPaused)
            {
                _isPaused = false;
                SetState(EngineState.Running);
                Debug.WriteLine("[Engine] Started");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Engine] Start error: {ex.Message}");
            SetState(EngineState.Stopped);
        }
    }

    /// <summary>
    /// 停止引擎。刷新缓冲区、写入退出标记。
    /// </summary>
    public async Task StopAsync()
    {
        if (!_isRunning) return;

        SetState(EngineState.Stopping);

        try
        {
            // 停止定时器
            _batchTimer?.Stop();
            _batchTimer?.Dispose();
            _batchTimer = null;

            // 取消引擎取消信号
            _engineCts?.Cancel();

            // 取消订阅
            _tracker.OnActivityChanged -= OnTrackerActivityChanged;
            _idleDetector.OnIdleStateChanged -= OnIdleStateChanged;
            _sleepDetector.OnSleepStateChanged -= OnSleepStateChanged;

            // W0-M3: 停止时记录进行中的暂停时段
            if (_isPaused && _pauseStartTime.HasValue)
            {
                var segment = new PauseSegment
                {
                    StartTime = _pauseStartTime.Value,
                    EndTime = DateTime.Now,
                };
                _pauseHistory.Add(segment);
                _pauseStartTime = null;
            }

            // 停止子组件
            if (_windowSwitchLogger != null)
            {
                await _windowSwitchLogger.StopAsync();
            }
            _tracker.Stop();
            _idleDetector.Stop();
            _sleepDetector.Stop();

            // 最终化当前事件
            FinalizeCurrentEvent();

            // 刷新缓冲区
            await FlushBatchAsync();

            // 标记正常退出
            DateTime? lastEndTime = _currentEvent?.EndTime ?? _currentEvent?.StartTime;
            await _crashRecovery.MarkGracefulExitAsync(lastEndTime);

            _isRunning = false;
            _isPaused = false;
            SetState(EngineState.Stopped);

            Debug.WriteLine("[Engine] Stopped");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Engine] Stop error: {ex.Message}");
            SetState(EngineState.Stopped);
        }
    }

    /// <summary>
    /// 暂停追踪。结束当前事件并刷新。
    /// 记录暂停起始时间（W0-M3 暂停时长数据）。
    /// </summary>
    public async Task PauseAsync()
    {
        ThrowIfDisposed();
        if (!_isRunning || _isPaused) return;

        _isPaused = true;
        _pauseStartTime = DateTime.Now;

        try
        {
            FinalizeCurrentEvent();
            await FlushBatchAsync();

            _tracker.Pause();

            // W0-M3: 写入暂停标记文件（暂停态崩溃恢复）
            await _crashRecovery.MarkPausedAsync();

            SetState(EngineState.Paused);
            Debug.WriteLine("[Engine] Paused");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Engine] Pause error: {ex.Message}");
        }
    }

    /// <summary>
    /// 恢复追踪。立即捕获当前窗口。
    /// 记录暂停时段（W0-M3 暂停时长数据）。
    /// </summary>
    public async Task ResumeAsync()
    {
        ThrowIfDisposed();
        if (!_isRunning || !_isPaused) return;

        _isPaused = false;

        try
        {
            _tracker.Resume();

            // ── 记录暂停时段 (W0-M3: 暂停时长数据供日报调用) ──
            if (_pauseStartTime.HasValue)
            {
                var segment = new PauseSegment
                {
                    StartTime = _pauseStartTime.Value,
                    EndTime = DateTime.Now,
                };
                _pauseHistory.Add(segment);
                _pauseStartTime = null;
            }

            // W0-M3: 清除暂停标记（恢复后不再需要）
            await _crashRecovery.ClearPauseMarkerAsync();

            // 立即捕获当前前台窗口作为新事件
            CaptureCurrentWindow();

            SetState(EngineState.Running);
            Debug.WriteLine("[Engine] Resumed");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Engine] Resume error: {ex.Message}");
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// 获取当前活动快照（供外部 UI 使用）。
    /// </summary>
    public ActivityEvent? GetCurrentEvent() => _currentEvent;

    /// <summary>
    /// 手动触发一次缓冲刷新。
    /// </summary>
    public async Task FlushAsync()
    {
        await FlushBatchAsync();
    }

    // ──────────────────────────────────────────────
    // 崩溃恢复
    // ──────────────────────────────────────────────

    private async Task PerformCrashRecoveryAsync()
    {
        try
        {
            var result = await _crashRecovery.CheckAsync();

            if (result.WasCrashed)
            {
                // 尝试从数据库获取最后一条记录
                ActivityEvent? lastEvent = null;
                try
                {
                    var todayEvents = await _repository.GetTodayEventsAsync();
                    lastEvent = todayEvents.Count > 0 ? todayEvents[^1] : null;
                }
                catch
                {
                    // 数据库可能为空
                }

                DateTime lastEndTime = lastEvent?.EndTime ?? lastEvent?.StartTime
                    ?? DateTime.Now.AddMinutes(-RecoveryWindowMinutes);

                var elapsed = DateTime.Now - lastEndTime;

                if (elapsed <= TimeSpan.FromMinutes(RecoveryWindowMinutes))
                {
                    // 5 分钟窗口内 → 补录
                    var recoveryStart = DateTime.Now.AddMinutes(-RecoveryWindowMinutes);
                    var recovered = _crashRecovery.BuildRecoveryEvents(lastEndTime, recoveryStart);

                    if (recovered.Count > 0)
                    {
                        await _repository.InsertBatchAsync(recovered);
                        Debug.WriteLine($"[Engine] Crash recovery: {recovered.Count} events inserted");
                    }
                }
                else
                {
                    Debug.WriteLine($"[Engine] Crash detected but gap >{RecoveryWindowMinutes}min, starting fresh");
                }

                // 清除标记
                await _crashRecovery.ClearMarkerAsync();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Engine] Crash recovery error: {ex.Message}");
        }
    }

    // ──────────────────────────────────────────────
    // 事件处理
    // ──────────────────────────────────────────────

    private void OnTrackerActivityChanged(object? sender, ActivityEvent newEvent)
    {
        if (_disposed || _isPaused) return;

        try
        {
            lock (_batchLock)
            {
                // 最终化当前事件
                if (_currentEvent != null)
                {
                    _currentEvent.EndTime = newEvent.StartTime;
                    _currentEvent.DurationMs = (long)(newEvent.StartTime - _currentEvent.StartTime).TotalMilliseconds;
                    _pendingBatch.Add(_currentEvent);
                }

                // 设置新事件为当前
                _currentEvent = newEvent;

                // 应用分类器
                if (_categorizer != null)
                {
                    try
                    {
                        var (category, workTag) = _categorizer.Classify(newEvent);
                        _currentEvent.Category = category;
                        _currentEvent.WorkTag = workTag;
                    }
                    catch { }
                }

                // 检查是否需要立即刷新
                if (_pendingBatch.Count >= BatchMaxSize)
                {
                    _ = FlushBatchAsync(); // fire-and-forget
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Engine] Tracker event error: {ex.Message}");
        }
    }

    private void OnIdleStateChanged(object? sender, bool isIdle)
    {
        if (_disposed || _isPaused) return;

        _isIdle = isIdle;

        // W0-M2: 转发空闲状态变更给 Dashboard 等外部订阅者
        try
        {
            IdleStateChanged?.Invoke(this, isIdle);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Engine] Idle state forward error: {ex.Message}");
        }

        try
        {
            if (isIdle)
            {
                EnterIdle();
            }
            else
            {
                ExitIdle();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Engine] Idle event error: {ex.Message}");
        }
    }

    private void OnSleepStateChanged(object? sender, bool isSleeping)
    {
        if (_disposed || _isPaused) return;

        _isSleeping = isSleeping;

        // W0-M2: 转发睡眠状态变更给 Dashboard 等外部订阅者
        try
        {
            SleepStateChanged?.Invoke(this, isSleeping);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Engine] Sleep state forward error: {ex.Message}");
        }

        try
        {
            if (isSleeping)
            {
                EnterSleep();
            }
            else
            {
                ExitSleep();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Engine] Sleep event error: {ex.Message}");
        }
    }

    // ──────────────────────────────────────────────
    // 空闲状态管理
    // ──────────────────────────────────────────────

    private void EnterIdle()
    {
        if (_currentEvent == null || _currentEvent.Category == Category.Idle) return;

        var now = DateTime.Now;

        lock (_batchLock)
        {
            // 保存当前事件以便延续
            _lastActiveEvent = _currentEvent;

            // 最终化当前事件
            _currentEvent.EndTime = now;
            _currentEvent.DurationMs = (long)(now - _currentEvent.StartTime).TotalMilliseconds;
            _pendingBatch.Add(_currentEvent);

            // 创建空闲事件
            _currentEvent = new ActivityEvent
            {
                StartTime = now,
                Category = Category.Idle,
                WorkTag = WorkTag.Unknown,
                Detail = "Idle",
            };
        }

        // 空闲降频
        if (_tracker is WindowTracker wt)
            wt.SetIdleMode(true);

        Debug.WriteLine($"[Engine] Idle started");
    }

    private void ExitIdle()
    {
        if (_currentEvent == null || _currentEvent.Category != Category.Idle) return;

        var now = DateTime.Now;

        lock (_batchLock)
        {
            // 最终化空闲事件
            _currentEvent.EndTime = now;
            _currentEvent.DurationMs = (long)(now - _currentEvent.StartTime).TotalMilliseconds;
            _pendingBatch.Add(_currentEvent);

            // 恢复之前的事件（延续模式）
            if (_lastActiveEvent != null)
            {
                var continued = new ActivityEvent
                {
                    StartTime = now,
                    WindowTitle = _lastActiveEvent.WindowTitle,
                    ProcessName = _lastActiveEvent.ProcessName,
                    ProcessPath = _lastActiveEvent.ProcessPath,
                    ProcessId = _lastActiveEvent.ProcessId,
                    Category = _lastActiveEvent.Category,
                    WorkTag = _lastActiveEvent.WorkTag,
                    Detail = _lastActiveEvent.Detail,
                    Domain = _lastActiveEvent.Domain,
                    Project = _lastActiveEvent.Project,
                    IsContinued = true,
                    RawWindowTitle = _lastActiveEvent.RawWindowTitle,
                    RawProcessPath = _lastActiveEvent.RawProcessPath,
                };
                _currentEvent = continued;
                _lastActiveEvent = null;
            }
            else
            {
                // 无上次事件可延续（兜底）
                CaptureCurrentWindow();
            }
        }

        // 恢复正常轮询频率
        if (_tracker is WindowTracker wt)
            wt.SetIdleMode(false);

        Debug.WriteLine($"[Engine] Idle ended, is_continued=true");
    }

    // ──────────────────────────────────────────────
    // 睡眠状态管理
    // ──────────────────────────────────────────────

    private void EnterSleep()
    {
        var now = DateTime.Now;

        lock (_batchLock)
        {
            // 最终化当前事件
            if (_currentEvent != null)
            {
                _currentEvent.EndTime = now;
                _currentEvent.DurationMs = (long)(now - _currentEvent.StartTime).TotalMilliseconds;
                _pendingBatch.Add(_currentEvent);
            }

            // 创建睡眠事件
            _currentEvent = new ActivityEvent
            {
                StartTime = now,
                Category = Category.Sleep,
                WorkTag = WorkTag.Unknown,
                Detail = "Sleep",
            };
        }

        Debug.WriteLine($"[Engine] Sleep started");
    }

    private void ExitSleep()
    {
        if (_currentEvent == null || _currentEvent.Category != Category.Sleep) return;

        var now = DateTime.Now;

        lock (_batchLock)
        {
            // 最终化睡眠事件
            _currentEvent.EndTime = now;
            _currentEvent.DurationMs = (long)(now - _currentEvent.StartTime).TotalMilliseconds;
            _pendingBatch.Add(_currentEvent);

            // 恢复追踪 — 立即捕获当前窗口
            CaptureCurrentWindow();
        }

        Debug.WriteLine($"[Engine] Sleep ended, resuming tracking");
    }

    // ──────────────────────────────────────────────
    // 辅助方法
    // ──────────────────────────────────────────────

    /// <summary>
    /// 最终化当前事件（设置 EndTime 和 DurationMs），加入待写入缓冲区。
    /// </summary>
    private void FinalizeCurrentEvent()
    {
        if (_currentEvent == null) return;

        lock (_batchLock)
        {
            _currentEvent.EndTime = DateTime.Now;
            _currentEvent.DurationMs = (long)(_currentEvent.EndTime.Value - _currentEvent.StartTime).TotalMilliseconds;
            _pendingBatch.Add(_currentEvent);
            _currentEvent = null;
        }
    }

    /// <summary>
    /// 立即捕获当前前台窗口作为新事件。
    /// </summary>
    private void CaptureCurrentWindow()
    {
        try
        {
            IntPtr handle = NativeMethods.GetForegroundWindow();
            if (handle == IntPtr.Zero) return;

            NativeMethods.GetWindowThreadProcessId(handle, out uint processId);
            if (processId == 0) return;

            var titleBuf = new System.Text.StringBuilder(1024);
            NativeMethods.GetWindowText(handle, titleBuf, 1024);
            string title = titleBuf.ToString();

            string? processName = "unknown";
            string? processPath = null;
            try
            {
                using var proc = System.Diagnostics.Process.GetProcessById((int)processId);
                processName = proc.ProcessName + ".exe";
                processPath = proc.MainModule?.FileName;
            }
            catch { }

            _currentEvent = new ActivityEvent
            {
                StartTime = DateTime.Now,
                WindowTitle = title,
                ProcessName = processName,
                ProcessPath = processPath,
                ProcessId = (int)processId,
                Category = Category.App,
                WorkTag = WorkTag.Unknown,
                RawWindowTitle = title,
                RawProcessPath = processPath,
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Engine] CaptureWindow error: {ex.Message}");
        }
    }

    // ──────────────────────────────────────────────
    // 批量写入
    // ──────────────────────────────────────────────

    private void OnBatchTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (_disposed || _isPaused) return;

        try
        {
            // 设置线程优先级
            try { Thread.CurrentThread.Priority = ThreadPriority.BelowNormal; }
            catch { }

            // 定时刷新的同时，同时更新当前事件的 DurationMs
            if (_currentEvent != null)
            {
                _currentEvent.DurationMs = (long)(DateTime.Now - _currentEvent.StartTime).TotalMilliseconds;
            }

            _ = FlushBatchAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Engine] Batch timer error: {ex.Message}");
        }
    }

    private async Task FlushBatchAsync()
    {
        List<ActivityEvent> batch;

        lock (_batchLock)
        {
            if (_pendingBatch.Count == 0) return;
            batch = new List<ActivityEvent>(_pendingBatch);
            _pendingBatch.Clear();
        }

        try
        {
            await _repository.InsertBatchAsync(batch);
            _lastFlushTime = DateTime.Now;
            Debug.WriteLine($"[Engine] Flushed {batch.Count} events");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Engine] Flush error: {ex.Message}");
            // 写入失败时重新加入缓冲区（防止丢失）
            lock (_batchLock)
            {
                _pendingBatch.AddRange(batch);
            }
        }
    }

    // ──────────────────────────────────────────────
    // 状态管理
    // ──────────────────────────────────────────────

    private void SetState(EngineState newState)
    {
        if (State == newState) return;
        State = newState;
        try
        {
            OnEngineStateChanged?.Invoke(this, newState);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Engine] State change handler error: {ex.Message}");
        }
    }

    // ──────────────────────────────────────────────
    // IDisposable
    // ──────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _batchTimer?.Stop();
        _batchTimer?.Dispose();
        _batchTimer = null;

        _tracker.OnActivityChanged -= OnTrackerActivityChanged;
        _idleDetector.OnIdleStateChanged -= OnIdleStateChanged;
        _sleepDetector.OnSleepStateChanged -= OnSleepStateChanged;

        (_tracker as IDisposable)?.Dispose();
        (_idleDetector as IDisposable)?.Dispose();
        (_sleepDetector as IDisposable)?.Dispose();
        _windowSwitchLogger?.Dispose();
        _crashRecovery.Dispose();

        _engineCts?.Cancel();
        _engineCts?.Dispose();
        _engineCts = null;

        OnEngineStateChanged = null;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ActivityEngine));
    }
}

/// <summary>
/// 引擎运行状态。
/// </summary>
public enum EngineState
{
    /// <summary>已停止 / 初始状态。</summary>
    Stopped = 0,

    /// <summary>正在启动（含延迟）。</summary>
    Starting,

    /// <summary>正常运行中。</summary>
    Running,

    /// <summary>已暂停。</summary>
    Paused,

    /// <summary>正在停止。</summary>
    Stopping,
}

/// <summary>
/// 暂停时段记录（W0-M3 / F11.8 暂停时长数据）。
/// 一次暂停从 PauseAsync() 到 ResumeAsync() 的时间窗口。
/// </summary>
public class PauseSegment
{
    /// <summary>暂停开始时间。</summary>
    public DateTime StartTime { get; set; }

    /// <summary>恢复时间。</summary>
    public DateTime EndTime { get; set; }

    /// <summary>暂停时长（毫秒）。</summary>
    public long DurationMs => (long)(EndTime - StartTime).TotalMilliseconds;

    /// <summary>格式化的暂停时段描述，如 "09:30-09:45"。</summary>
    public string FormattedRange => $"{StartTime:HH:mm}-{EndTime:HH:mm}";
}
