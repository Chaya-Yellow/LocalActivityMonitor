using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using Microsoft.Win32;
using Application = System.Windows.Application;
using ActivityMonitor.TrayApp.Dashboard;
using ActivityMonitor.TrayApp.TrayIcon;

namespace ActivityMonitor.TrayApp;

/// <summary>
/// WPF 应用程序入口。
/// 管理托盘图标生命周期和各窗口的显示/隐藏。
/// <list type="bullet">
///   <item>F13.1 — 命名 Mutex 禁止多开，双击 exe 激活已有实例。</item>
///   <item>F13.4 — 系统关机/注销时自动退出，不弹窗。</item>
/// </list>
/// </summary>
public partial class App : Application
{
    private static Mutex? _instanceMutex;
    private const string MutexName = "Local\\ActivityMonitor_SingleInstance";

    private TrayIconManager? _trayIcon;
    private DashboardWindow? _dashboardWindow;

    // ──────────────────────────────────────────────
    // Win32 P/Invoke for window activation
    // ──────────────────────────────────────────────

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private const int SW_RESTORE = 9;
    private const int SW_SHOW = 5;

    // ──────────────────────────────────────────────
    // Application 生命周期
    // ──────────────────────────────────────────────

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ── F13.1: 单实例检查 ──
        if (!EnsureSingleInstance())
        {
            Current.Shutdown();
            return;
        }

        // 确保 Windows Forms 组件能正确接收消息循环
        WindowsFormsSynchronizationContext.AutoInstall = true;

        // 初始化系统托盘图标
        _trayIcon = new TrayIconManager();
        _trayIcon.OpenDashboardRequested += OnOpenDashboard;
        _trayIcon.ExitRequested += OnExit;
        _trayIcon.ToggleMonitoringRequested += OnToggleMonitoring;
        _trayIcon.ExportReportRequested += OnExportReport;

        // ── F13.4: 监听系统关机/注销 ──
        SystemEvents.SessionEnding += OnSystemSessionEnding;

        // 首次启动自动打开 Dashboard
        OnOpenDashboard();
    }

    /// <summary>
    /// F13.1: 通过命名 Mutex 确保单实例运行。
    /// 若已有实例在运行，激活其主窗口后返回 false。
    /// </summary>
    private static bool EnsureSingleInstance()
    {
        _instanceMutex = new Mutex(true, MutexName, out bool createdNew);

        if (createdNew)
            return true;

        // ── 已有实例运行 → 激活其主窗口 ──
        try
        {
            int currentPid = Environment.ProcessId;
            string procName = Process.GetCurrentProcess().ProcessName;

            foreach (var proc in Process.GetProcessesByName(procName))
            {
                if (proc.Id == currentPid) continue;

                IntPtr hWnd = proc.MainWindowHandle;
                if (hWnd != IntPtr.Zero)
                {
                    ShowWindow(hWnd, SW_RESTORE);
                    SetForegroundWindow(hWnd);
                    break;
                }
            }
        }
        catch
        {
            // 激活失败不影响退出
        }

        return false;
    }

    /// <summary>
    /// F13.4: 系统关机/注销时自动退出，不弹确认框。
    /// </summary>
    private void OnSystemSessionEnding(object? sender, SessionEndingEventArgs e)
    {
        // 取消事件取消标记，允许正常关机
        e.Cancel = false;

        // 在主线程上执行清理并退出
        Dispatcher.Invoke(() =>
        {
            SystemEvents.SessionEnding -= OnSystemSessionEnding;

            _trayIcon?.Dispose();
            Current.Shutdown();
        });
    }

    /// <summary>
    /// 打开 Dashboard 主面板（单例模式）。
    /// 关闭窗口时只隐藏而非销毁，托盘图标继续运行。
    /// </summary>
    private void OnOpenDashboard()
    {
        if (_dashboardWindow == null)
        {
            _dashboardWindow = new DashboardWindow();
            _dashboardWindow.Closing += (_, args) =>
            {
                // 用户点击关闭 → 弹出确认框
                args.Cancel = true; // 先取消默认行为
                HandleWindowClosing(_dashboardWindow);
            };

            // 监听 ViewModel 的监控状态变化 → 同步托盘图标
            _dashboardWindow.ViewModel.MonitoringStateChanged += isMonitoring =>
            {
                if (_trayIcon != null)
                    _trayIcon.UpdateState(isMonitoring
                        ? TrayIcon.TrayIconManager.TrayState.Running
                        : TrayIcon.TrayIconManager.TrayState.Paused);
            };

            _dashboardWindow.Show();
        }
        else
        {
            _dashboardWindow.WindowState = WindowState.Normal;
            _dashboardWindow.Show();
            _dashboardWindow.Activate();
        }
    }

    /// <summary>
    /// F13.2: 处理所有窗口的关闭事件。
    /// 弹出两种选择的确认框。
    /// </summary>
    private static void HandleWindowClosing(Window? window)
    {
        if (window == null) return;

        var result = System.Windows.MessageBox.Show(
            "加载到后台继续监控，或彻底退出程序？\n\n" +
            "  [是] → 彻底退出程序\n" +
            "  [否] → 加载到后台继续监控",
            "Activity Monitor",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.Yes // 回车默认"是"（彻底退出）
        );

        if (result == MessageBoxResult.Yes)
        {
            // "彻底退出" → 关闭所有窗口并退出
            foreach (Window w in Current.Windows)
            {
                w.Close();
            }
            // 由 OnExit 完成清理
            ((App)Current).OnExitInternal();
        }
        else
        {
            // "加载到后台" → 隐藏窗口
            window.Hide();
        }
    }

    /// <summary>
    /// 暂停/恢复监控（委托给当前 Dashboard 的 ViewModel），并同步托盘图标状态。
    /// </summary>
    private void OnToggleMonitoring()
    {
        _dashboardWindow?.ViewModel?.ToggleMonitoringCommand.Execute(null);
        UpdateTrayIconForMonitoringState();
    }

    /// <summary>
    /// 根据 Dashboard ViewModel 的监控状态更新托盘图标颜色。
    /// 运行中→绿色，已暂停→灰色。
    /// </summary>
    private void UpdateTrayIconForMonitoringState()
    {
        if (_trayIcon == null || _dashboardWindow?.ViewModel == null) return;
        var isMonitoring = _dashboardWindow.ViewModel.IsMonitoring;
        _trayIcon.UpdateState(isMonitoring
            ? TrayIcon.TrayIconManager.TrayState.Running
            : TrayIcon.TrayIconManager.TrayState.Paused);
    }

    /// <summary>
    /// 导出日报（打开预览窗口）。
    /// </summary>
    private void OnExportReport()
    {
        // 确保 Dashboard 已打开，然后调用导出
        OnOpenDashboard();
        _dashboardWindow?.ViewModel?.OpenReportEditorCommand.Execute(null);
    }

    /// <summary>
    /// F13.3: 托盘退出菜单项，弹出确认框。
    /// </summary>
    private void OnExit()
    {
        // 系统正在关机/注销中 → 不弹窗直接退出
        if (_shuttingDown)
        {
            CleanupAndExit();
            return;
        }

        var result = System.Windows.MessageBox.Show(
            "加载到后台继续监控，或彻底退出程序？\n\n" +
            "  [是] → 彻底退出程序\n" +
            "  [否] → 加载到后台继续监控",
            "Activity Monitor",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.Yes
        );

        if (result == MessageBoxResult.Yes)
        {
            OnExitInternal();
        }
        // 否 → 什么也不做，继续后台运行
    }

    /// <summary>
    /// 内部退出逻辑——清理所有资源并关闭程序。
    /// </summary>
    private void OnExitInternal()
    {
        CleanupAndExit();
    }

    private bool _shuttingDown;

    private void CleanupAndExit()
    {
        _shuttingDown = true;
        SystemEvents.SessionEnding -= OnSystemSessionEnding;

        _trayIcon?.Dispose();
        Current.Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.SessionEnding -= OnSystemSessionEnding;

        _instanceMutex?.Dispose();
        _instanceMutex = null;

        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}