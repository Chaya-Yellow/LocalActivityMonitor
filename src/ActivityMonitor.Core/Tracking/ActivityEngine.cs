using System.Timers;
using ActivityMonitor.Core.Interfaces;
using ActivityMonitor.Core.Models;
using ActivityMonitor.Core.Win32;
using Debug = System.Diagnostics.Debug;
using Timer = System.Timers.Timer;

namespace ActivityMonitor.Core.Tracking;

public sealed class ActivityEngine : IDisposable
{
    private readonly IActivityTracker _tracker;
    private readonly IIdleDetector _idleDetector;
    private readonly ISleepDetector _sleepDetector;
    private readonly ILockScreenDetector _lockScreenDetector;
    private readonly IActivityRepository _repository;
    private readonly IActivityCategorizer? _categorizer;
    private readonly CrashRecoveryService _crashRecovery;
    private readonly WindowSwitchLogger _windowSwitchLogger;

    private ActivityEvent? _currentEvent;
    private ActivityEvent? _lastActiveEvent;
    private readonly List<ActivityEvent> _pendingBatch = new(capacity: 64);
    private readonly object _batchLock = new();
    private DateTime _lastFlushTime = DateTime.MinValue;

    private Timer? _batchTimer;
    private CancellationTokenSource? _engineCts;
    private bool _isRunning;
    private bool _isPaused;
    private bool _isIdle;
    private bool _isSleeping;
    private bool _isLocked;
    private bool _disposed;

    private readonly List<PauseSegment> _pauseHistory = new();
    private DateTime? _pauseStartTime;

    private const int StartupDelayMs = 10_000;
    private const int BatchFlushIntervalMs = 30_000;
    private const int BatchMaxSize = 50;
    private const int RecoveryWindowMinutes = 5;

    public event EventHandler<EngineState>? OnEngineStateChanged;
    public event EventHandler<bool>? IdleStateChanged;
    public event EventHandler<bool>? SleepStateChanged;
    public event EventHandler<bool>? LockStateChanged;

    public EngineState State { get; private set; } = EngineState.Stopped;
    public bool IsRunning => _isRunning;
    public bool IsPaused => _isPaused;
    public bool IsIdle => _isIdle;
    public bool IsSleeping => _isSleeping;
    public bool IsLocked => _isLocked;
    public IReadOnlyList<PauseSegment> PauseHistory => _pauseHistory.AsReadOnly();

    public ActivityEngine(
        IActivityTracker tracker,
        IIdleDetector idleDetector,
        ISleepDetector sleepDetector,
        ILockScreenDetector lockScreenDetector,
        IActivityRepository repository,
        IActivityCategorizer? categorizer,
        CrashRecoveryService crashRecovery,
        IOperationLogRepository? operationLogRepository = null)
    {
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
        _idleDetector = idleDetector ?? throw new ArgumentNullException(nameof(idleDetector));
        _sleepDetector = sleepDetector ?? throw new ArgumentNullException(nameof(sleepDetector));
        _lockScreenDetector = lockScreenDetector ?? throw new ArgumentNullException(nameof(lockScreenDetector));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _categorizer = categorizer;
        _crashRecovery = crashRecovery ?? throw new ArgumentNullException(nameof(crashRecovery));

        _windowSwitchLogger = operationLogRepository != null
            ? new WindowSwitchLogger(_tracker, operationLogRepository)
            : null!; // null 时跳过窗口切换日志
    }

    public async Task StartAsync()
    {
        ThrowIfDisposed();
        if (_isRunning) return;
        _engineCts = new CancellationTokenSource();
        var token = _engineCts.Token;
        SetState(EngineState.Starting);
        try
        {
            await PerformCrashRecoveryAsync();
            bool wasPaused = await _crashRecovery.WasPausedOnCrashAsync();

            Debug.WriteLine("[Engine] Startup delay 10s...");
            try { await Task.Delay(StartupDelayMs, token); }
            catch (OperationCanceledException) { return; }

            _tracker.OnActivityChanged += OnTrackerActivityChanged;
            _idleDetector.OnIdleStateChanged += OnIdleStateChanged;
            _sleepDetector.OnSleepStateChanged += OnSleepStateChanged;
            _lockScreenDetector.OnLockStateChanged += OnLockScreenStateChanged;

            _idleDetector.Start();
            _sleepDetector.Start();
            _lockScreenDetector.Start();

            if (wasPaused)
            {
                _isRunning = true; _isPaused = true; _pauseStartTime = DateTime.Now;
                SetState(EngineState.Paused);
                Debug.WriteLine("[Engine] Started in paused state (crash recovery)");
            }
            else
            {
                _tracker.Start();
            }

            _batchTimer = new Timer(BatchFlushIntervalMs) { AutoReset = true };
            _batchTimer.Elapsed += OnBatchTimerElapsed;
            _batchTimer.Start();

            // W1-M3: 启动窗口切换日志记录器
            if (_windowSwitchLogger != null) { _windowSwitchLogger.Start(); }

            if (!wasPaused) { _isPaused = false; SetState(EngineState.Running); Debug.WriteLine("[Engine] Started"); }
        }
        catch (Exception ex) { Debug.WriteLine(string.Format("[Engine] Start error: {0}", ex.Message)); SetState(EngineState.Stopped); }
    }

    public async Task StopAsync()
    {
        if (!_isRunning) return;
        SetState(EngineState.Stopping);
        try
        {
            _batchTimer?.Stop(); _batchTimer?.Dispose(); _batchTimer = null;
            _engineCts?.Cancel();

            _tracker.OnActivityChanged -= OnTrackerActivityChanged;
            _idleDetector.OnIdleStateChanged -= OnIdleStateChanged;
            _sleepDetector.OnSleepStateChanged -= OnSleepStateChanged;
            _lockScreenDetector.OnLockStateChanged -= OnLockScreenStateChanged;

            if (_isPaused && _pauseStartTime.HasValue)
            {
                _pauseHistory.Add(new PauseSegment { StartTime = _pauseStartTime.Value, EndTime = DateTime.Now });
                _pauseStartTime = null;
            }

            // W1-M3: 停止窗口切换日志记录器
            if (_windowSwitchLogger != null) { await _windowSwitchLogger.StopAsync(); }
            _tracker.Stop(); _idleDetector.Stop(); _sleepDetector.Stop(); _lockScreenDetector.Stop();
            FinalizeCurrentEvent();
            await FlushBatchAsync();
            await _crashRecovery.MarkGracefulExitAsync(_currentEvent?.EndTime ?? _currentEvent?.StartTime);
            _isRunning = false; _isPaused = false;
            SetState(EngineState.Stopped);
            Debug.WriteLine("[Engine] Stopped");
        }
        catch (Exception ex) { Debug.WriteLine(string.Format("[Engine] Stop error: {0}", ex.Message)); SetState(EngineState.Stopped); }
    }

    public async Task PauseAsync()
    {
        ThrowIfDisposed();
        if (!_isRunning || _isPaused) return;
        _isPaused = true; _pauseStartTime = DateTime.Now;
        try
        {
            FinalizeCurrentEvent(); await FlushBatchAsync(); _tracker.Pause();
            await _crashRecovery.MarkPausedAsync();
            SetState(EngineState.Paused); Debug.WriteLine("[Engine] Paused");
        }
        catch (Exception ex) { Debug.WriteLine(string.Format("[Engine] Pause error: {0}", ex.Message)); }
    }

    public async Task ResumeAsync()
    {
        ThrowIfDisposed();
        if (!_isRunning || !_isPaused) return;
        _isPaused = false;
        try
        {
            _tracker.Resume();
            if (_pauseStartTime.HasValue)
            {
                _pauseHistory.Add(new PauseSegment { StartTime = _pauseStartTime.Value, EndTime = DateTime.Now });
                _pauseStartTime = null;
            }
            await _crashRecovery.ClearPauseMarkerAsync();
            CaptureCurrentWindow();
            SetState(EngineState.Running); Debug.WriteLine("[Engine] Resumed");
        }
        catch (Exception ex) { Debug.WriteLine(string.Format("[Engine] Resume error: {0}", ex.Message)); }
        await Task.CompletedTask;
    }

    public ActivityEvent? GetCurrentEvent() => _currentEvent;
    public async Task FlushAsync() => await FlushBatchAsync();

    private async Task PerformCrashRecoveryAsync()
    {
        try
        {
            var result = await _crashRecovery.CheckAsync();
            if (!result.WasCrashed) return;
            ActivityEvent? lastEvent = null;
            try { lastEvent = (await _repository.GetTodayEventsAsync()).LastOrDefault(); } catch { }
            DateTime lastEndTime = lastEvent?.EndTime ?? lastEvent?.StartTime ?? DateTime.Now.AddMinutes(-RecoveryWindowMinutes);
            if (DateTime.Now - lastEndTime <= TimeSpan.FromMinutes(RecoveryWindowMinutes))
            {
                var recovered = _crashRecovery.BuildRecoveryEvents(lastEndTime, DateTime.Now.AddMinutes(-RecoveryWindowMinutes));
                if (recovered.Count > 0) { await _repository.InsertBatchAsync(recovered); Debug.WriteLine(string.Format("[Engine] Crash recovery: {0} events inserted", recovered.Count)); }
            }
            await _crashRecovery.ClearMarkerAsync();
        }
        catch (Exception ex) { Debug.WriteLine(string.Format("[Engine] Crash recovery error: {0}", ex.Message)); }
    }

    private void OnTrackerActivityChanged(object? sender, ActivityEvent newEvent)
    {
        if (_disposed || _isPaused) return;
        try
        {
            lock (_batchLock)
            {
                if (_currentEvent != null)
                {
                    _currentEvent.EndTime = newEvent.StartTime;
                    _currentEvent.DurationMs = (long)(newEvent.StartTime - _currentEvent.StartTime).TotalMilliseconds;
                    _pendingBatch.Add(_currentEvent);
                }
                _currentEvent = newEvent;
                if (_categorizer != null) { try { var (cat, tag) = _categorizer.Classify(newEvent); _currentEvent.Category = cat; _currentEvent.WorkTag = tag; } catch { } }
                if (_pendingBatch.Count >= BatchMaxSize) _ = FlushBatchAsync();
            }
        }
        catch (Exception ex) { Debug.WriteLine(string.Format("[Engine] Tracker event error: {0}", ex.Message)); }
    }

    private void OnIdleStateChanged(object? sender, bool isIdle)
    {
        if (_disposed || _isPaused) return;
        _isIdle = isIdle;
        try { IdleStateChanged?.Invoke(this, isIdle); } catch (Exception ex) { Debug.WriteLine(string.Format("[Engine] Idle state forward error: {0}", ex.Message)); }
        try { if (isIdle) EnterIdle(); else ExitIdle(); } catch (Exception ex) { Debug.WriteLine(string.Format("[Engine] Idle event error: {0}", ex.Message)); }
    }

    private void OnSleepStateChanged(object? sender, bool isSleeping)
    {
        if (_disposed || _isPaused) return;
        _isSleeping = isSleeping;
        try { SleepStateChanged?.Invoke(this, isSleeping); } catch (Exception ex) { Debug.WriteLine(string.Format("[Engine] Sleep state forward error: {0}", ex.Message)); }
        try { if (isSleeping) EnterSleep(); else ExitSleep(); } catch (Exception ex) { Debug.WriteLine(string.Format("[Engine] Sleep event error: {0}", ex.Message)); }
    }

    private void OnLockScreenStateChanged(object? sender, bool isLocked)
    {
        if (_disposed || _isPaused) return;
        _isLocked = isLocked;
        try { LockStateChanged?.Invoke(this, isLocked); } catch (Exception ex) { Debug.WriteLine(string.Format("[Engine] Lock screen state forward error: {0}", ex.Message)); }
        try { if (isLocked) EnterLock(); else ExitLock(); } catch (Exception ex) { Debug.WriteLine(string.Format("[Engine] Lock screen event error: {0}", ex.Message)); }
    }

    private void EnterIdle()
    {
        if (_currentEvent == null || _currentEvent.Category == Category.Idle) return;
        var now = DateTime.Now;
        lock (_batchLock)
        {
            _lastActiveEvent = _currentEvent;
            _currentEvent.EndTime = now; _currentEvent.DurationMs = (long)(now - _currentEvent.StartTime).TotalMilliseconds;
            _pendingBatch.Add(_currentEvent);
            _currentEvent = new ActivityEvent { StartTime = now, Category = Category.Idle, WorkTag = WorkTag.Unknown, Detail = "Idle" };
        }
        if (_tracker is WindowTracker wt) wt.SetIdleMode(true);
        Debug.WriteLine("[Engine] Idle started");
    }

    private void ExitIdle()
    {
        if (_currentEvent == null || _currentEvent.Category != Category.Idle) return;
        var now = DateTime.Now;
        lock (_batchLock)
        {
            _currentEvent.EndTime = now; _currentEvent.DurationMs = (long)(now - _currentEvent.StartTime).TotalMilliseconds;
            _pendingBatch.Add(_currentEvent);
            if (_lastActiveEvent != null)
            {
                _currentEvent = new ActivityEvent { StartTime = now, WindowTitle = _lastActiveEvent.WindowTitle, ProcessName = _lastActiveEvent.ProcessName, ProcessPath = _lastActiveEvent.ProcessPath, ProcessId = _lastActiveEvent.ProcessId, Category = _lastActiveEvent.Category, WorkTag = _lastActiveEvent.WorkTag, Detail = _lastActiveEvent.Detail, Domain = _lastActiveEvent.Domain, Project = _lastActiveEvent.Project, IsContinued = true, RawWindowTitle = _lastActiveEvent.RawWindowTitle, RawProcessPath = _lastActiveEvent.RawProcessPath };
                _lastActiveEvent = null;
            }
            else { CaptureCurrentWindow(); }
        }
        if (_tracker is WindowTracker wt) wt.SetIdleMode(false);
        Debug.WriteLine("[Engine] Idle ended, is_continued=true");
    }

    private void EnterSleep()
    {
        var now = DateTime.Now;
        lock (_batchLock)
        {
            if (_currentEvent != null) { _currentEvent.EndTime = now; _currentEvent.DurationMs = (long)(now - _currentEvent.StartTime).TotalMilliseconds; _pendingBatch.Add(_currentEvent); }
            _currentEvent = new ActivityEvent { StartTime = now, Category = Category.Sleep, WorkTag = WorkTag.Unknown, Detail = "Sleep" };
        }
        Debug.WriteLine("[Engine] Sleep started");
    }

    private void ExitSleep()
    {
        if (_currentEvent == null || _currentEvent.Category != Category.Sleep) return;
        var now = DateTime.Now;
        lock (_batchLock) { _currentEvent.EndTime = now; _currentEvent.DurationMs = (long)(now - _currentEvent.StartTime).TotalMilliseconds; _pendingBatch.Add(_currentEvent); CaptureCurrentWindow(); }
        Debug.WriteLine("[Engine] Sleep ended, resuming tracking");
    }

    private void EnterLock()
    {
        var now = DateTime.Now;
        lock (_batchLock)
        {
            if (_currentEvent != null) { _currentEvent.EndTime = now; _currentEvent.DurationMs = (long)(now - _currentEvent.StartTime).TotalMilliseconds; _pendingBatch.Add(_currentEvent); }
            _currentEvent = new ActivityEvent { StartTime = now, Category = Category.Locked, WorkTag = WorkTag.Unknown, Detail = "LockScreen" };
        }
        Debug.WriteLine("[Engine] Lock started");
    }

    private void ExitLock()
    {
        if (_currentEvent == null || _currentEvent.Category != Category.Locked) return;
        var now = DateTime.Now;
        lock (_batchLock) { _currentEvent.EndTime = now; _currentEvent.DurationMs = (long)(now - _currentEvent.StartTime).TotalMilliseconds; _pendingBatch.Add(_currentEvent); CaptureCurrentWindow(); }
        Debug.WriteLine("[Engine] Lock ended, resuming tracking");
    }

    private void FinalizeCurrentEvent()
    {
        if (_currentEvent == null) return;
        lock (_batchLock) { _currentEvent.EndTime = DateTime.Now; _currentEvent.DurationMs = (long)(_currentEvent.EndTime.Value - _currentEvent.StartTime).TotalMilliseconds; _pendingBatch.Add(_currentEvent); _currentEvent = null; }
    }

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
            string? processName = "unknown"; string? processPath = null;
            try { using var proc = System.Diagnostics.Process.GetProcessById((int)processId); processName = proc.ProcessName + ".exe"; processPath = proc.MainModule?.FileName; } catch { }
            _currentEvent = new ActivityEvent { StartTime = DateTime.Now, WindowTitle = title, ProcessName = processName, ProcessPath = processPath, ProcessId = (int)processId, Category = Category.App, WorkTag = WorkTag.Unknown, RawWindowTitle = title, RawProcessPath = processPath };
        }
        catch (Exception ex) { Debug.WriteLine(string.Format("[Engine] CaptureWindow error: {0}", ex.Message)); }
    }

    private void OnBatchTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (_disposed || _isPaused) return;
        try
        {
            try { Thread.CurrentThread.Priority = ThreadPriority.BelowNormal; } catch { }
            if (_currentEvent != null) _currentEvent.DurationMs = (long)(DateTime.Now - _currentEvent.StartTime).TotalMilliseconds;
            _ = FlushBatchAsync();
        }
        catch (Exception ex) { Debug.WriteLine(string.Format("[Engine] Batch timer error: {0}", ex.Message)); }
    }

    private async Task FlushBatchAsync()
    {
        List<ActivityEvent> batch;
        lock (_batchLock) { if (_pendingBatch.Count == 0) return; batch = new List<ActivityEvent>(_pendingBatch); _pendingBatch.Clear(); }
        try { await _repository.InsertBatchAsync(batch); _lastFlushTime = DateTime.Now; Debug.WriteLine(string.Format("[Engine] Flushed {0} events", batch.Count)); }
        catch (Exception ex) { Debug.WriteLine(string.Format("[Engine] Flush error: {0}", ex.Message)); lock (_batchLock) { _pendingBatch.AddRange(batch); } }
    }

    private void SetState(EngineState newState)
    {
        if (State == newState) return;
        State = newState;
        try { OnEngineStateChanged?.Invoke(this, newState); } catch (Exception ex) { Debug.WriteLine(string.Format("[Engine] State change handler error: {0}", ex.Message)); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _batchTimer?.Stop(); _batchTimer?.Dispose(); _batchTimer = null;
        _tracker.OnActivityChanged -= OnTrackerActivityChanged;
        _idleDetector.OnIdleStateChanged -= OnIdleStateChanged;
        _sleepDetector.OnSleepStateChanged -= OnSleepStateChanged;
        _lockScreenDetector.OnLockStateChanged -= OnLockScreenStateChanged;
        (_tracker as IDisposable)?.Dispose();
        (_idleDetector as IDisposable)?.Dispose();
        (_sleepDetector as IDisposable)?.Dispose();
        (_lockScreenDetector as IDisposable)?.Dispose();
        _windowSwitchLogger?.Dispose();
        _crashRecovery.Dispose();
        _engineCts?.Cancel(); _engineCts?.Dispose(); _engineCts = null;
        OnEngineStateChanged = null;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ActivityEngine));
    }
}

public enum EngineState { Stopped = 0, Starting, Running, Paused, Stopping }

public class PauseSegment
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public long DurationMs => (long)(EndTime - StartTime).TotalMilliseconds;
    public string FormattedRange { get { return string.Format("{0:HH:mm}-{1:HH:mm}", StartTime, EndTime); } }
}
