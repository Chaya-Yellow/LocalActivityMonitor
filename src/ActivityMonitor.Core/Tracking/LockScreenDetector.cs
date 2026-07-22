using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Timers;
using ActivityMonitor.Core.Interfaces;
using ActivityMonitor.Core.Win32;
using Timer = System.Timers.Timer;

namespace ActivityMonitor.Core.Tracking;

public sealed class LockScreenDetector : ILockScreenDetector, IDisposable
{
    private const string WindowClassName = "AM_LockScreenDetector_Window";
    private const int DefaultLockThresholdMs = 30_000;

    private IntPtr _hwnd;
    private Thread? _messageThread;
    private WndProcDelegate? _wndProcDelegate;
    private Timer? _thresholdTimer;
    private DateTime? _lockStartTime;

    private bool _isLocked;
    private bool _disposed;
    private bool _started;
    private CancellationTokenSource? _cts;

    public event EventHandler<bool>? OnLockStateChanged;
    public bool IsLocked => _isLocked;
    public long LockThresholdMs { get; set; } = DefaultLockThresholdMs;

    public void Start()
    {
        ThrowIfDisposed();
        if (_started) return;
        _started = true;
        _cts = new CancellationTokenSource();
        _messageThread = new Thread(MessagePumpProc) { Name = "AM-LockScreenDetector", IsBackground = true };
        _messageThread.TrySetApartmentState(ApartmentState.STA);
        _messageThread.Start();
    }

    public void Stop()
    {
        if (!_started) return;
        _started = false;
        StopThresholdTimer();
        _cts?.Cancel();
        if (_hwnd != IntPtr.Zero) User32.PostMessage(_hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        if (_messageThread?.IsAlive == true) try { _messageThread.Join(2000); } catch { }
        Cleanup();
    }

    private void MessagePumpProc()
    {
        try
        {
            _wndProcDelegate = WndProc;
            var wc = new WNDCLASS { lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate), hInstance = Kernel32.GetModuleHandle(null), lpszClassName = WindowClassName };
            ushort classAtom = User32.RegisterClass(ref wc);
            if (classAtom == 0) { Debug.WriteLine("[LockScreenDetector] RegisterClass failed"); return; }
            _hwnd = User32.CreateWindowEx(0, WindowClassName, "AM_LockScreenDetector", 0, 0, 0, 0, 0, HWND_MESSAGE, IntPtr.Zero, wc.hInstance, IntPtr.Zero);
            if (_hwnd == IntPtr.Zero) { Debug.WriteLine("[LockScreenDetector] CreateWindowEx failed"); return; }
            if (!NativeMethods.WTSRegisterSessionNotification(_hwnd, NativeMethods.NOTIFY_FOR_THIS_SESSION))
            { Debug.WriteLine("[LockScreenDetector] WTSRegisterSessionNotification failed"); return; }
            while (User32.GetMessage(out MSG msg, IntPtr.Zero, 0, 0))
            {
                User32.TranslateMessage(ref msg);
                User32.DispatchMessage(ref msg);
                if (_cts?.IsCancellationRequested == true) break;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Debug.WriteLine(string.Format("[LockScreenDetector] Thread error: {0}", ex.Message)); }
        finally { Cleanup(); }
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (msg == WM_WTSSESSION_CHANGE)
            {
                int sessionEvent = (int)wParam;
                if (sessionEvent == NativeMethods.WTS_SESSION_LOCK) OnSessionLock();
                else if (sessionEvent == NativeMethods.WTS_SESSION_UNLOCK) OnSessionUnlock();
                return (IntPtr)1;
            }
            if (msg == WM_CLOSE) User32.DestroyWindow(hWnd);
        }
        catch { }
        return User32.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private void OnSessionLock()
    {
        _lockStartTime = DateTime.Now;
        if (_isLocked) return;
        StopThresholdTimer();
        _thresholdTimer = new Timer(LockThresholdMs) { AutoReset = false };
        _thresholdTimer.Elapsed += OnThresholdElapsed;
        _thresholdTimer.Start();
    }

    private void OnSessionUnlock()
    {
        StopThresholdTimer();
        if (!_isLocked) { _lockStartTime = null; return; }
        _lockStartTime = null;
        SetLockState(false);
    }

    private void OnThresholdElapsed(object? sender, ElapsedEventArgs e)
    {
        if (_disposed) return;
        try { if (!_isLocked) SetLockState(true); } catch { }
    }

    private void SetLockState(bool locked)
    {
        if (_isLocked == locked) return;
        _isLocked = locked;
        try { OnLockStateChanged?.Invoke(this, locked); } catch { }
    }

    private void StopThresholdTimer()
    {
        if (_thresholdTimer == null) return;
        try { _thresholdTimer.Stop(); _thresholdTimer.Dispose(); } catch { }
        _thresholdTimer = null;
    }

    private void Cleanup()
    {
        if (_hwnd != IntPtr.Zero)
        {
            try { NativeMethods.WTSUnRegisterSessionNotification(_hwnd); } catch { }
            try { User32.DestroyWindow(_hwnd); } catch { }
            _hwnd = IntPtr.Zero;
        }
        _wndProcDelegate = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        OnLockStateChanged = null;
        _cts?.Dispose();
        _cts = null;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(LockScreenDetector));
    }

    private static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);
    private const uint WM_WTSSESSION_CHANGE = 0x02B1;
    private const uint WM_CLOSE = 0x0010;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam; public IntPtr lParam; public uint time; public int ptX; public int ptY; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASS
    {
        public uint style; public IntPtr lpfnWndProc; public int cbClsExtra; public int cbWndExtra;
        public IntPtr hInstance; public IntPtr hIcon; public IntPtr hCursor; public IntPtr hbrBackground;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszClassName;
    }

    private static partial class User32
    {
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
        public static extern ushort RegisterClass(ref WNDCLASS lpWndClass);
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
        public static extern IntPtr CreateWindowEx(uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);
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
