using System.ComponentModel;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;
using ActivityMonitor.TrayApp.Dashboard;
using ActivityMonitor.TrayApp.TrayIcon;

namespace ActivityMonitor.TrayApp;

/// <summary>
/// WPF 应用程序入口。
/// 管理托盘图标生命周期和各窗口的显示/隐藏。
/// </summary>
public partial class App : Application
{
    private TrayIconManager? _trayIcon;
    private DashboardWindow? _dashboardWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 确保 Windows Forms 组件能正确接收消息循环
        WindowsFormsSynchronizationContext.AutoInstall = true;

        // 初始化系统托盘图标
        _trayIcon = new TrayIconManager();
        _trayIcon.OpenDashboardRequested += OnOpenDashboard;
        _trayIcon.ExitRequested += OnExit;
        _trayIcon.ToggleMonitoringRequested += OnToggleMonitoring;

        // 首次启动自动打开 Dashboard
        OnOpenDashboard();
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
                // 用户点击关闭 → 隐藏窗口而非退出程序
                _dashboardWindow.Hide();
                args.Cancel = true;
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
    /// 暂停/恢复监控（委托给当前 Dashboard 的 ViewModel）。
    /// </summary>
    private void OnToggleMonitoring()
    {
        _dashboardWindow?.ViewModel?.ToggleMonitoringCommand.Execute(null);
    }

    /// <summary>
    /// 完全退出程序。
    /// </summary>
    private void OnExit()
    {
        _trayIcon?.Dispose();
        Current.Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
