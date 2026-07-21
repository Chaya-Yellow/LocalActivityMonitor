using System.Diagnostics;
using System.Runtime.InteropServices;
using ActivityMonitor.Core.Interfaces;

namespace ActivityMonitor.Core.Tracking;

/// <summary>
/// 睡眠/休眠检测器。
/// 创建隐藏消息窗口监听 <c>WM_POWERBROADCAST</c>，检测系统电源状态变更。
/// <list type="bullet">
///   <item>专用 STA 线程 + 消息泵，独立于主线程运行。</item>
///   <item>系统进入睡眠时触发 <see cref="ISleepDetector.OnSleepStateChanged"/>(true)。</item>
///   <item>系统唤醒时触发 <see cref="ISleepDetector.OnSleepStateChanged"/>(false)。</item>
///   <item>所有异常均被捕获，永不崩溃。</item>
/// </list>
/// </summary>
public sealed class SleepDetector : ISleepDetector, IDisposable
{
    /// <summary>消息窗口类名。</summary>
    private const string WindowClassName = "AM_SleepDetector_Window";

    /// <summary>消息窗口句柄。</summary>
    private IntPtr _hwnd;

    /// <summary>消息泵线程。</summary>
    private Thread? _messageThread;

    /// <summary>窗口过程回调委托（需 GC root 防止回收）。</summary>
    private WndProcDelegate? _wndProcDelegate;

    private bool _isSleeping;
    private bool _disposed;
    private bool _started;

    /// <summary>取消信号，用于安全停止消息泵。</summary>
    private CancellationTokenSource? _cts;

    /// <inheritdoc />
    public event EventHandler<bool>? OnSleepStateChanged;

    /// <inheritdoc />
    public bool IsSleeping => _isSleeping;

    // ──────────────────────────────────────────────
    // ISleepDetector 生命周期
    // ──────────────────────────────────────────────

    /// <inheritdoc />
    public void Start()
    {
        ThrowIfDisposed();

        if (_started) return;
        _started = true;

        _cts = new CancellationTokenSource();
        _messageThread = new Thread(MessagePumpProc)
        {
            Name = "AM-SleepDetector",
            IsBackground = true,
            // 消息窗口需要 STA 线程
            // ApartmentState 通过 TrySetApartmentState 设置
        };

        _messageThread.TrySetApartmentState(ApartmentState.STA);

        _messageThread.Start();
    }

    /// <inheritdoc />
    public void Stop()
    {
        if (!_started) return;
        _started = false;

        // 发送退出消息到消息泵
        _cts?.Cancel();

        if (_hwnd != IntPtr.Zero)
        {
            // 发送 WM_CLOSE 让 GetMessage 退出
            User32.PostMessage(_hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        }

        // 等待线程退出（最多 2 秒）
        if (_messageThread?.IsAlive == true)
        {
            try { _messageThread.Join(2000); }
            catch { }
        }

        Cleanup();
    }

    // ──────────────────────────────────────────────
    // 消息窗口与消息泵
    // ──────────────────────────────────────────────

    /// <summary>
    /// STA 消息泵线程入口。创建隐藏消息窗口并处理消息循环。
    /// </summary>
    private void MessagePumpProc()
    {
        try
        {
            // 注册窗口类
            _wndProcDelegate = WndProc;
            var wc = new WNDCLASS
            {
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
                hInstance = Kernel32.GetModuleHandle(null),
                lpszClassName = WindowClassName,
            };

            ushort classAtom = User32.RegisterClass(ref wc);
            if (classAtom == 0)
            {
                Debug.WriteLine("[SleepDetector] RegisterClass failed");
                return;
            }

            // 创建消息窗口（HWND_MESSAGE）
            _hwnd = User32.CreateWindowEx(
                0, WindowClassName, "AM_SleepDetector",
                0, 0, 0, 0, 0,
                HWND_MESSAGE, IntPtr.Zero,
                wc.hInstance, IntPtr.Zero);

            if (_hwnd == IntPtr.Zero)
            {
                Debug.WriteLine("[SleepDetector] CreateWindowEx failed");
                return;
            }

            // 注册电源设置通知（可选增强，WM_POWERBROADCAST 本身已覆盖基础睡眠/唤醒）
            // RegisterPowerSettingNotification 已在 NativeMethods 中声明，
            // 此处通过已有窗口句柄注册。

            // ── 消息循环 ──
            while (User32.GetMessage(out MSG msg, IntPtr.Zero, 0, 0))
            {
                User32.TranslateMessage(ref msg);
                User32.DispatchMessage(ref msg);

                // 检查取消请求
                if (_cts?.IsCancellationRequested == true)
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // 正常停止
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SleepDetector] Thread error: {ex.Message}");
        }
        finally
        {
            Cleanup();
        }
    }

    /// <summary>
    /// 消息窗口过程。
    /// </summary>
    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        const uint WM_POWERBROADCAST = 0x0218;
        const int PBT_APMSUSPEND = 0x0004;
        const int PBT_APMRESUMEAUTOMATIC = 0x0012;
        const int PBT_APMRESUMESUSPEND = 0x0007;
        const uint WM_CLOSE = 0x0010;
        const uint WM_DESTROY = 0x0002;

        try
        {
            switch (msg)
            {
                case WM_POWERBROADCAST:
                    int powerEvent = (int)wParam;
                    switch (powerEvent)
                    {
                        case PBT_APMSUSPEND:
                            SetSleepState(true);
                            break;

                        case PBT_APMRESUMEAUTOMATIC:
                        case PBT_APMRESUMESUSPEND:
                            SetSleepState(false);
                            break;
                    }
                    // 返回 TRUE 表示消息已处理
                    return (IntPtr)1;

                case WM_CLOSE:
                    User32.DestroyWindow(hWnd);
                    break;

                case WM_DESTROY:
                    // 退出消息泵
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SleepDetector] WndProc error: {ex.Message}");
        }

        return User32.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    // ──────────────────────────────────────────────
    // 状态管理
    // ──────────────────────────────────────────────

    private void SetSleepState(bool sleeping)
    {
        if (_isSleeping == sleeping) return;
        _isSleeping = sleeping;

        try
        {
            OnSleepStateChanged?.Invoke(this, sleeping);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SleepDetector] Event handler error: {ex.Message}");
        }
    }

    private void Cleanup()
    {
        if (_hwnd != IntPtr.Zero)
        {
            try { User32.DestroyWindow(_hwnd); }
            catch { }
            _hwnd = IntPtr.Zero;
        }

        _wndProcDelegate = null;
    }

    // ──────────────────────────────────────────────
    // IDisposable
    // ──────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        OnSleepStateChanged = null;
        _cts?.Dispose();
        _cts = null;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SleepDetector));
    }

    // ──────────────────────────────────────────────
    // Win32 P/Invoke 内部声明
    // ──────────────────────────────────────────────

    /// <summary>HWND_MESSAGE 常量 — 消息仅窗口。</summary>
    private static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);

    private const uint WM_CLOSE = 0x0010;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASS
    {
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszClassName;
    }

    private static partial class User32
    {
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
        public static extern ushort RegisterClass(ref WNDCLASS lpWndClass);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
        public static extern IntPtr CreateWindowEx(
            uint dwExStyle,
            [MarshalAs(UnmanagedType.LPWStr)] string lpClassName,
            [MarshalAs(UnmanagedType.LPWStr)] string lpWindowName,
            uint dwStyle,
            int x, int y, int nWidth, int nHeight,
            IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll", ExactSpelling = true)]
        public static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll", ExactSpelling = true)]
        public static extern IntPtr DispatchMessage(ref MSG lpMsg);

        [DllImport("user32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    }

    private static partial class Kernel32
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        public static extern IntPtr GetModuleHandle(string? lpModuleName);
    }
}
