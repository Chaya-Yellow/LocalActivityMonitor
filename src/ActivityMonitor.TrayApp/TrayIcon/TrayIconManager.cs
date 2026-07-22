using System.Drawing;
using System.Windows.Forms;

namespace ActivityMonitor.TrayApp.TrayIcon;

/// <summary>
/// 系统托盘图标管理器。
/// 负责托盘图标的状态切换（运行/暂停/空闲）、悬停提示、右键菜单和左键点击事件。
/// </summary>
public sealed class TrayIconManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly ToolStripMenuItem _toggleItem;
    private bool _disposed;

    /// <summary>托盘图标状态枚举。</summary>
    public enum TrayState
    {
        /// <summary>监控运行中（绿色）。</summary>
        Running,
        /// <summary>监控已暂停（橙色）。</summary>
        Paused,
        /// <summary>空闲中（灰色）。</summary>
        Idle
    }

    private TrayState _currentState = TrayState.Running;

    /// <summary>请求打开 Dashboard 面板。</summary>
    public event Action? OpenDashboardRequested;
    /// <summary>请求退出程序。</summary>
    public event Action? ExitRequested;
    /// <summary>请求暂停/恢复监控。</summary>
    public event Action? ToggleMonitoringRequested;

    /// <summary>请求导出日报。</summary>
    public event Action? ExportReportRequested;

    /// <summary>
    /// 初始化托盘图标、悬停提示和右键菜单。
    /// </summary>
    public TrayIconManager()
    {
        _notifyIcon = new NotifyIcon
        {
            Text = "Activity Monitor — 运行中",
            Visible = true,
        };
        // 设置程序化生成的图标
        _notifyIcon.Icon = CreateStateIcon(TrayState.Running);

        // 左键单击 → 打开面板
        _notifyIcon.Click += (s, e) =>
        {
            if (e is MouseEventArgs me && me.Button == MouseButtons.Left)
                OpenDashboardRequested?.Invoke();
        };

        // 构建右键菜单
        _contextMenu = new ContextMenuStrip();

        var openItem = new ToolStripMenuItem("打开面板", null, (_, _) => OpenDashboardRequested?.Invoke());
        _contextMenu.Items.Add(openItem);
        _contextMenu.Items.Add(new ToolStripSeparator());

        _toggleItem = new ToolStripMenuItem("暂停监控", null, (_, _) =>
        {
            ToggleMonitoringRequested?.Invoke();
        });
        _contextMenu.Items.Add(_toggleItem);

        var exportItem = new ToolStripMenuItem("导出日报", null, (_, _) => ExportReportRequested?.Invoke());
        _contextMenu.Items.Add(exportItem);
        _contextMenu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("退出", null, (_, _) => ExitRequested?.Invoke());
        _contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = _contextMenu;
    }

    /// <summary>
    /// 更新托盘图标状态（颜色 + 文字提示同时更新）。
    /// </summary>
    public void UpdateState(TrayState state)
    {
        if (_disposed) return;
        _currentState = state;

        // 更新图标
        var oldIcon = _notifyIcon.Icon;
        _notifyIcon.Icon = CreateStateIcon(state);
        oldIcon?.Dispose();

        // 更新文字
        _notifyIcon.Text = state switch
        {
            TrayState.Running => "Activity Monitor — 运行中",
            TrayState.Paused => "Activity Monitor — 已暂停",
            TrayState.Idle => "Activity Monitor — 空闲中",
            _ => "Activity Monitor",
        };

        // 更新菜单项文字
        _toggleItem.Text = state == TrayState.Paused ? "恢复监控" : "暂停监控";
    }

    /// <summary>
    /// 更新悬停提示文字，可附加今日累计时长等信息。
    /// </summary>
    /// <param name="tooltipText">自定义提示文本。</param>
    public void UpdateTooltip(string tooltipText)
    {
        if (!_disposed && !string.IsNullOrEmpty(tooltipText))
            _notifyIcon.Text = tooltipText;
    }

    /// <summary>
    /// 根据状态绘制 16×16 托盘图标。
    /// 颜色含义：绿色=运行中，橙色=已暂停，灰色=空闲中。
    /// </summary>
    private static Icon CreateStateIcon(TrayState state)
    {
        var bitmap = new Bitmap(16, 16);
        try
        {
            using var g = Graphics.FromImage(bitmap);

            var color = state switch
            {
                TrayState.Running => Color.Green,
                TrayState.Paused => Color.Orange,
                TrayState.Idle => Color.Gray,
                _ => Color.Green,
            };

            using var brush = new SolidBrush(color);
            g.FillEllipse(brush, 1, 1, 14, 14);     // 圆形底色
            using var font = new Font("Segoe UI", 7, FontStyle.Bold);
            g.DrawString("A", font, Brushes.White, 3, 2);  // 字母 A

            var handle = bitmap.GetHicon();
            return Icon.FromHandle(handle);
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_notifyIcon.Icon != null)
        {
            // 销毁由 Icon.FromHandle 创建的图标
            var iconHandle = _notifyIcon.Icon.Handle;
            _notifyIcon.Icon.Dispose();
            NativeMethods.DestroyIcon(iconHandle);
        }

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _contextMenu.Dispose();
    }

    /// <summary>
    /// Win32 API —— 确保 Icon.FromHandle 创建的 GDI 对象被正确释放。
    /// </summary>
    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr hIcon);
    }
}
