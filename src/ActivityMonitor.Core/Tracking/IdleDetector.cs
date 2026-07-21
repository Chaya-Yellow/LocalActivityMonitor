using System.Diagnostics;
using System.Timers;
using ActivityMonitor.Core.Interfaces;
using ActivityMonitor.Core.Win32;
using Timer = System.Timers.Timer;

namespace ActivityMonitor.Core.Tracking;

/// <summary>
/// 空闲检测器。
/// 通过 <c>GetLastInputInfo</c> 轮询判断用户空闲状态，
/// 超过配置的空闲阈值后触发 <see cref="IIdleDetector.OnIdleStateChanged"/> 事件。
/// <list type="bullet">
///   <item>默认空闲阈值：15 分钟（900,000 毫秒）。</item>
///   <item>使用 Timer 而非 while(true)。</item>
///   <item>内部异常全部捕获，永不崩溃。</item>
/// </list>
/// </summary>
public sealed class IdleDetector : IIdleDetector, IDisposable
{
    private Timer? _timer;
    private bool _isIdle;
    private bool _disposed;

    /// <summary>默认轮询间隔（毫秒）。</summary>
    private const int DefaultPollIntervalMs = 2000;

    /// <summary>默认空闲阈值（毫秒）：15 分钟。</summary>
    private const long DefaultIdleThresholdMs = 15 * 60 * 1000;

    /// <summary>
    /// 空闲状态变更事件。参数表示是否处于空闲状态。
    /// </summary>
    public event EventHandler<bool>? OnIdleStateChanged;

    /// <inheritdoc />
    public bool IsIdle => _isIdle;

    /// <inheritdoc />
    public long IdleThresholdMs { get; set; } = DefaultIdleThresholdMs;

    /// <inheritdoc />
    public long IdleSinceMs
    {
        get
        {
            try
            {
                var lastInput = GetLastInputTick();
                if (lastInput == 0) return 0;

                uint now = (uint)Environment.TickCount;
                // 处理 TickCount 回绕（约 49.7 天发生一次）
                long diff = now >= lastInput ? now - lastInput : uint.MaxValue - lastInput + now;
                return diff;
            }
            catch
            {
                return 0;
            }
        }
    }

    // ──────────────────────────────────────────────
    // IIdleDetector 生命周期
    // ──────────────────────────────────────────────

    /// <inheritdoc />
    public void Start()
    {
        ThrowIfDisposed();
        if (_timer != null) return;

        _timer = new Timer(DefaultPollIntervalMs)
        {
            AutoReset = true,
        };

        _timer.Elapsed += OnTimerElapsed;
        _timer.Start();

        // 初始采样
        try { CheckIdleState(); }
        catch { }
    }

    /// <inheritdoc />
    public void Stop()
    {
        if (_timer == null) return;

        _timer.Stop();
        _timer.Dispose();
        _timer = null;
        _isIdle = false;
    }

    // ──────────────────────────────────────────────
    // 核心逻辑
    // ──────────────────────────────────────────────

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (_disposed) return;

        try
        {
            // 设置线程优先级
            try { Thread.CurrentThread.Priority = ThreadPriority.BelowNormal; }
            catch { }

            CheckIdleState();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[IdleDetector] Poll error: {ex.Message}");
        }
    }

    /// <summary>
    /// 检查当前空闲状态并触发变更事件。
    /// </summary>
    private void CheckIdleState()
    {
        long idleMs = IdleSinceMs;
        bool nowIdle = idleMs >= IdleThresholdMs;

        if (nowIdle != _isIdle)
        {
            _isIdle = nowIdle;
            // 在非 Timer 线程上安全触发事件
            OnIdleStateChanged?.Invoke(this, nowIdle);
        }
    }

    // ──────────────────────────────────────────────
    // 辅助方法
    // ──────────────────────────────────────────────

    /// <summary>
    /// 调用 <c>GetLastInputInfo</c> 获取上次输入的系统 TickCount。
    /// </summary>
    private static uint GetLastInputTick()
    {
        var plii = new NativeMethods.LASTINPUTINFO
        {
            cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.LASTINPUTINFO>()
        };

        if (NativeMethods.GetLastInputInfo(ref plii))
            return plii.dwTime;

        return 0;
    }

    /// <summary>
    /// 手动触发一次空闲状态检查（供 ActivityEngine 按需调用）。
    /// </summary>
    public void CheckNow()
    {
        if (_disposed) return;
        CheckIdleState();
    }

    // ──────────────────────────────────────────────
    // IDisposable
    // ──────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        OnIdleStateChanged = null;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(IdleDetector));
    }
}
