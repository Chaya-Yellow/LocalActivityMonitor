using System.Runtime.InteropServices;
using System.Text;

namespace ActivityMonitor.Core.Win32;

/// <summary>
/// Win32 API P/Invoke 声明。
/// 所有声明标注 <see cref="DllImportAttribute"/> 并设置 <c>ExactSpelling = true</c> 以提升性能。
/// </summary>
internal static partial class NativeMethods
{
    // ──────────────────────────────────────────────
    // user32.dll — 窗口与输入管理
    // ──────────────────────────────────────────────

    /// <summary>
    /// user32.dll — 获取当前前台窗口的句柄。
    /// <see href="https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getforegroundwindow"/>
    /// </summary>
    /// <returns>前台窗口句柄；若没有前台窗口返回 <see cref="System.IntPtr.Zero"/>。</returns>
    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern IntPtr GetForegroundWindow();

    /// <summary>
    /// user32.dll — 获取指定窗口所属进程的 ID。
    /// <see href="https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowthreadprocessid"/>
    /// </summary>
    /// <param name="hWnd">窗口句柄。</param>
    /// <param name="processId">输出参数，接收进程 ID。</param>
    /// <returns>创建窗口的线程 ID。</returns>
    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    /// <summary>
    /// user32.dll — 获取指定窗口的标题文本。
    /// <see href="https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowtextw"/>
    /// </summary>
    /// <param name="hWnd">窗口句柄。</param>
    /// <param name="lpString">接收标题文本的缓冲区。</param>
    /// <param name="nMaxCount">缓冲区最大字符数（含 null 终止符）。</param>
    /// <returns>实际复制的字符数（含 null 终止符）；若窗口无标题返回 0。</returns>
    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    internal static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    /// <summary>
    /// user32.dll — 获取用户上次输入信息（键盘/鼠标）。
    /// <see href="https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getlastinputinfo"/>
    /// </summary>
    /// <param name="plii">接收 <see cref="LASTINPUTINFO"/> 结构体。</param>
    /// <returns>成功返回非零值；失败返回 0。</returns>
    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    /// <summary>
    /// user32.dll — 注册接收指定电源设置事件的通知。
    /// <see href="https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerpowersettingnotification"/>
    /// </summary>
    /// <param name="hRecipient">接收通知的窗口句柄或服务状态句柄。</param>
    /// <param name="PowerSettingGuid">标识电源设置事件的 GUID。</param>
    /// <param name="Flags">标志：<c>DEVICE_NOTIFY_WINDOW_HANDLE (0x00000000)</c> 或 <c>DEVICE_NOTIFY_SERVICE_HANDLE (0x00000001)</c>。</param>
    /// <returns>注册句柄；失败返回 <see cref="System.IntPtr.Zero"/>。</returns>
    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern IntPtr RegisterPowerSettingNotification(IntPtr hRecipient, ref Guid PowerSettingGuid, uint Flags);

    /// <summary>
    /// user32.dll — 取消电源设置事件通知注册。
    /// <see href="https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-unregisterpowersettingnotification"/>
    /// </summary>
    /// <param name="handle">由 <see cref="RegisterPowerSettingNotification"/> 返回的句柄。</param>
    /// <returns>成功返回 true；失败返回 false。</returns>
    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterPowerSettingNotification(IntPtr handle);

    // ──────────────────────────────────────────────
    // powrprof.dll — 电源管理
    // ──────────────────────────────────────────────

    /// <summary>
    /// powrprof.dll — 获取系统电源状态信息。
    /// <see href="https://docs.microsoft.com/en-us/windows/win32/api/powrprof/nf-powrprof-getsystempowerstatus"/>
    /// </summary>
    /// <param name="spStatus">接收 <see cref="SYSTEM_POWER_STATUS"/> 结构体。</param>
    /// <returns>成功返回 true；失败返回 false。</returns>
    [DllImport("powrprof.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS spStatus);

    // ──────────────────────────────────────────────
    // psapi.dll — 进程状态 API
    // ──────────────────────────────────────────────

    /// <summary>
    /// psapi.dll — 查询指定进程的工作集信息。
    /// <see href="https://docs.microsoft.com/en-us/windows/win32/api/psapi/nf-psapi-queryworkingset"/>
    /// </summary>
    /// <param name="hProcess">进程句柄。</param>
    /// <param name="pv">接收工作集信息的缓冲区。</param>
    /// <param name="cb">缓冲区大小（字节）。</param>
    /// <returns>成功返回 true；失败返回 false。</returns>
    [DllImport("psapi.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool QueryWorkingSet(IntPtr hProcess, IntPtr pv, int cb);

    // ──────────────────────────────────────────────
    // kernel32.dll — 系统功能
    // ──────────────────────────────────────────────

    /// <summary>
    /// kernel32.dll — 打开现有进程对象。
    /// <see href="https://docs.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-openprocess"/>
    /// </summary>
    /// <param name="dwDesiredAccess">进程访问权限。</param>
    /// <param name="bInheritHandle">是否继承句柄。</param>
    /// <param name="dwProcessId">进程 ID。</param>
    /// <returns>进程句柄；失败返回 null。</returns>
    [DllImport("kernel32.dll", ExactSpelling = true)]
    internal static extern IntPtr OpenProcess(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwProcessId);

    /// <summary>
    /// kernel32.dll — 关闭对象句柄。
    /// <see href="https://docs.microsoft.com/en-us/windows/win32/api/handleapi/nf-handleapi-closehandle"/>
    /// </summary>
    /// <param name="hObject">要关闭的句柄。</param>
    /// <returns>成功返回 true；失败返回 false。</returns>
    [DllImport("kernel32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseHandle(IntPtr hObject);

    // ──────────────────────────────────────────────
    // 结构体定义
    // ──────────────────────────────────────────────

    /// <summary>
    /// <c>GetLastInputInfo</c> 使用的输入信息结构体。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct LASTINPUTINFO
    {
        /// <summary>结构体大小（字节）。</summary>
        public uint cbSize;

        /// <summary>上次用户输入的时间戳（TickCount）。</summary>
        public uint dwTime;
    }

    /// <summary>
    /// <c>GetSystemPowerStatus</c> 使用的电源状态结构体。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct SYSTEM_POWER_STATUS
    {
        /// <summary>AC 电源状态：0=离线, 1=在线, 255=未知。</summary>
        public byte ACLineStatus;

        /// <summary>电池充电状态：0=无电池, 1=放电, 2=充电, 255=未知。</summary>
        public byte BatteryFlag;

        /// <summary>电池剩余电量百分比（0-100）；255=未知。</summary>
        public byte BatteryLifePercent;

        /// <summary>保留。</summary>
        public byte Reserved1;

        /// <summary>电池剩余寿命（秒）；-1=未知。</summary>
        public uint BatteryLifeTime;

        /// <summary>电池满充寿命（秒）；-1=未知。</summary>
        public uint BatteryFullLifeTime;
    }

    // ──────────────────────────────────────────────
    // 常量定义
    // ──────────────────────────────────────────────

    /// <summary>电源事件：系统正在进入睡眠/休眠状态。</summary>
    internal const int PBT_APMSUSPEND = 0x0004;

    /// <summary>电源事件：系统已从睡眠/休眠状态恢复。</summary>
    internal const int PBT_APMRESUMEAUTOMATIC = 0x0012;

    /// <summary>电源事件：系统正在恢复操作（用户可交互）。</summary>
    internal const int PBT_APMRESUMESUSPEND = 0x0007;

    /// <summary>Windows 消息：电源事件广播。</summary>
    internal const int WM_POWERBROADCAST = 0x0218;

    /// <summary>进程权限：查询进程信息。</summary>
    internal const uint PROCESS_QUERY_INFORMATION = 0x0400;

    /// <summary>进程权限：查询进程信息及其工作集。</summary>
    internal const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    /// <summary>电源设置 GUID：显示器关闭事件。</summary>
    internal static readonly Guid GUID_CONSOLE_DISPLAY_STATE = new Guid(0x6FE69556, 0x704A, 0x47A0, 0x8F, 0x24, 0xC2, 0x8D, 0x93, 0x6F, 0xDA, 0x47);

    /// <summary>电源设置 GUID：系统睡眠/唤醒事件。</summary>
    internal static readonly Guid GUID_SYSTEM_AWAYMODE = new Guid(0x98A7F580, 0x01F7, 0x48AA, 0x9C, 0x0F, 0x44, 0x3C, 0x29, 0xA4, 0x76, 0x30);

    // ──────────────────────────────────────────────
    // wtsapi32.dll — 终端服务会话通知
    // ──────────────────────────────────────────────

    /// <summary>
    /// wtsapi32.dll — 注册指定窗口接收会话变更通知（锁屏/解锁/远程连接等）。
    /// <see href="https://docs.microsoft.com/en-us/windows/win32/api/wtsapi32/nf-wtsapi32-wtsregistersessionnotification"/>
    /// </summary>
    /// <param name="hWnd">接收通知的窗口句柄。</param>
    /// <param name="dwFlags">
    ///   <c>NOTIFY_FOR_THIS_SESSION (0)</c>：仅接收当前会话通知；
    ///   <c>NOTIFY_FOR_ALL_SESSIONS (1)</c>：接收所有会话通知。
    /// </param>
    /// <returns>成功返回 true；失败返回 false。</returns>
    [DllImport("wtsapi32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool WTSRegisterSessionNotification(IntPtr hWnd, uint dwFlags);

    /// <summary>
    /// wtsapi32.dll — 取消窗口的会话变更通知。
    /// <see href="https://docs.microsoft.com/en-us/windows/win32/api/wtsapi32/nf-wtsapi32-wtsunregistersessionnotification"/>
    /// </summary>
    /// <param name="hWnd">先前注册的窗口句柄。</param>
    /// <returns>成功返回 true；失败返回 false。</returns>
    [DllImport("wtsapi32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool WTSUnRegisterSessionNotification(IntPtr hWnd);

    /// <summary>Windows 消息：会话状态变更（锁屏/解锁等）。</summary>
    internal const int WM_WTSSESSION_CHANGE = 0x02B1;

    /// <summary>会话事件：工作站已锁定。</summary>
    internal const int WTS_SESSION_LOCK = 0x7;

    /// <summary>会话事件：工作站已解锁。</summary>
    internal const int WTS_SESSION_UNLOCK = 0x8;

    /// <summary>仅接收当前会话通知。</summary>
    internal const uint NOTIFY_FOR_THIS_SESSION = 0;

    /// <summary>接收所有会话通知。</summary>
    internal const uint NOTIFY_FOR_ALL_SESSIONS = 1;
}
