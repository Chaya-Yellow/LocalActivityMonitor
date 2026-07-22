using System.Text;
using System.Timers;
using ActivityMonitor.Core.Interfaces;
using ActivityMonitor.Core.Models;
using ActivityMonitor.Core.Win32;
using Debug = System.Diagnostics.Debug;
using Process = System.Diagnostics.Process;
using Timer = System.Timers.Timer;

namespace ActivityMonitor.Core.Tracking;

/// <summary>
/// 通用前台窗口追踪器。
/// 每 2 秒轮询 <c>GetForegroundWindow()</c>，检测窗口/进程变更并触发事件。
/// <list type="bullet">
///   <item>所有 exe 无差别追踪，不设白名单。</item>
///   <item>内部 Timer 而非 while(true)，线程优先级 BelowNormal。</item>
///   <item>空闲状态下自动降频到 10 秒轮询。</item>
///   <item>所有异常均被捕获，永不崩溃。</item>
/// </list>
/// </summary>
public sealed class WindowTracker : IActivityTracker, IDisposable
{
    private Timer? _timer;
    private IntPtr _currentHandle;
    private uint _currentProcessId;
    private string? _currentProcessName;
    private string? _currentWindowTitle;
    private DateTime _currentEventStart;
    private int _pollIntervalMs = 2000;
    private bool _isRunning;
    private bool _isPaused;
    private bool _disposed;

    /// <summary>窗口标题缓冲区最大字符数（含 null 终止符）。</summary>
    private const int WindowTextMaxLength = 1024;

    /// <summary>空闲降频时的轮询间隔（毫秒）。</summary>
    private const int IdlePollIntervalMs = 10000;

    /// <summary>用于 GetWindowText 的可复用缓冲区。</summary>
    private readonly StringBuilder _titleBuffer = new(WindowTextMaxLength);

    // ──────────────────────────────────────────────
    // IActivityTracker 事件与属性
    // ──────────────────────────────────────────────

    /// <inheritdoc />
    public event EventHandler<ActivityEvent>? OnActivityChanged;

    /// <inheritdoc />
    public bool IsRunning => _isRunning;

    /// <inheritdoc />
    public bool IsPaused => _isPaused;

    /// <inheritdoc />
    public int PollIntervalMs
    {
        get => _pollIntervalMs;
        set
        {
            _pollIntervalMs = Math.Max(500, value); // 最低 500ms 防止高频
            if (_timer is { Enabled: true })
                _timer.Interval = _pollIntervalMs;
        }
    }

    // ──────────────────────────────────────────────
    // IActivityTracker 生命周期
    // ──────────────────────────────────────────────

    /// <inheritdoc />
    public void Start()
    {
        ThrowIfDisposed();

        if (_isRunning) return;

        _timer = new Timer(_pollIntervalMs)
        {
            AutoReset = true,
            // 让 Timer 在线程池上触发回调，避免阻塞调用方
        };

        // 将 Timer 回调运行的线程设为 BelowNormal 优先级
        _timer.Elapsed += OnTimerElapsed;

        _timer.Start();
        _isRunning = true;
        _isPaused = false;

        // 立即执行一次采样，快速获取当前窗口
        try { CaptureForeground(); }
        catch { /* 首次采样失败可接受，下次 tick 重试 */ }
    }

    /// <inheritdoc />
    public void Stop()
    {
        if (!_isRunning) return;

        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
        _isRunning = false;
        _isPaused = false;
        _currentHandle = IntPtr.Zero;
        _currentProcessId = 0;
        _currentProcessName = null;
        _currentWindowTitle = null;
    }

    /// <inheritdoc />
    public void Pause()
    {
        ThrowIfDisposed();
        if (!_isRunning) return;
        _isPaused = true;
    }

    /// <inheritdoc />
    public void Resume()
    {
        ThrowIfDisposed();
        if (!_isRunning) return;
        _isPaused = false;

        // 恢复后立即采样一次，保证窗口状态准确
        try { CaptureForeground(); }
        catch { }
    }

    // ──────────────────────────────────────────────
    // 空闲模式控制
    // ──────────────────────────────────────────────

    /// <summary>
    /// 设置空闲模式，自动调整轮询频率。
    /// </summary>
    /// <param name="idle">是否处于空闲状态。</param>
    public void SetIdleMode(bool idle)
    {
        if (_timer != null)
            _timer.Interval = idle ? IdlePollIntervalMs : _pollIntervalMs;
    }

    // ──────────────────────────────────────────────
    // 当前窗口快照（供 ActivityEngine 查询）
    // ──────────────────────────────────────────────

    /// <summary>
    /// 获取当前前台窗口信息快照。线程安全。
    /// </summary>
    public (IntPtr Handle, uint ProcessId, string? ProcessName, string? WindowTitle, DateTime StartTime) GetCurrentSnapshot()
    {
        lock (_titleBuffer)
        {
            return (_currentHandle, _currentProcessId, _currentProcessName, _currentWindowTitle, _currentEventStart);
        }
    }

    // ──────────────────────────────────────────────
    // 核心轮询逻辑
    // ──────────────────────────────────────────────

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (_isPaused || _disposed) return;

        // 设置线程优先级为 BelowNormal
        try { Thread.CurrentThread.Priority = ThreadPriority.BelowNormal; }
        catch { /* 非托管线程可能不支持设置优先级 */ }

        try
        {
            CaptureForeground();
        }
        catch (Exception ex)
        {
            // 所有异常在此捕获，永不崩溃
            Debug.WriteLine($"[WindowTracker] Poll error: {ex.Message}");
        }
    }

    /// <summary>
    /// 采样当前前台窗口信息，检测变更并触发事件。
    /// </summary>
    private void CaptureForeground()
    {
        IntPtr handle = NativeMethods.GetForegroundWindow();
        if (handle == IntPtr.Zero) return;

        uint processId;
        uint threadId = NativeMethods.GetWindowThreadProcessId(handle, out processId);
        if (processId == 0) return;

        // 获取窗口标题
        string title;
        lock (_titleBuffer)
        {
            _titleBuffer.Clear();
            int len = NativeMethods.GetWindowText(handle, _titleBuffer, WindowTextMaxLength);
            title = len > 0 ? _titleBuffer.ToString(0, len) : string.Empty;
        }

        // 获取进程信息（延迟加载，仅窗口变化时）
        string? processName = null;
        if (handle != _currentHandle || processId != _currentProcessId)
        {
            try
            {
                using var proc = Process.GetProcessById((int)processId);
                processName = proc.ProcessName;
            }
            catch
            {
                // 进程可能已退出
                processName = "unknown";
            }
        }

        // 与当前记录对比
        if (handle == _currentHandle && processId == _currentProcessId)
        {
            // 同一窗口 → 引擎负责累加时长，此处只更新标题以防变化
            if (title != _currentWindowTitle)
                _currentWindowTitle = title;
            return;
        }

        // ── 窗口已变更 ──
        string? processPath = null;
        try
        {
            using var proc = Process.GetProcessById((int)processId);
            processName ??= proc.ProcessName;
            processPath = proc.MainModule?.FileName;
        }
        catch
        {
            processName ??= "unknown";
        }

        var now = DateTime.Now;

        // 构建新活动事件（StartTime 设为本采样时刻，不在构造函数中做任何耗时操作）
        var newEvent = new ActivityEvent
        {
            StartTime = now,
            WindowTitle = title,
            ProcessName = $"{processName}.exe",
            ProcessPath = processPath,
            ProcessId = (int)processId,
            Category = Category.App,
            WorkTag = WorkTag.Unknown,
            // 原始字段：保留 GetWindowText 和 MainModule.FileName 的原始值
            RawWindowTitle = title,
            RawProcessPath = processPath,
        };

        // 更新当前窗口快照
        _currentHandle = handle;
        _currentProcessId = processId;
        _currentProcessName = processName;
        _currentWindowTitle = title;
        _currentEventStart = now;

        // 通知监听方（ActivityEngine）
        OnActivityChanged?.Invoke(this, newEvent);
    }

    // ──────────────────────────────────────────────
    // IDisposable
    // ──────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        OnActivityChanged = null;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(WindowTracker));
    }
}
