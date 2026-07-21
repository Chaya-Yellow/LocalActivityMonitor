using System.Windows;

namespace ActivityMonitor.TrayApp.Dashboard;

/// <summary>
/// Dashboard 主面板窗口。
/// 展示当日时间线、实时统计、操作入口。
/// 关闭窗口仅隐藏而非退出 —— 托盘图标在后台继续运行。
/// </summary>
public partial class DashboardWindow : Window
{
    /// <summary>关联的 ViewModel 实例。</summary>
    public DashboardViewModel ViewModel { get; }

    public DashboardWindow()
    {
        InitializeComponent();

        // 创建 ViewModel 并设为 DataContext
        ViewModel = new DashboardViewModel();
        DataContext = ViewModel;

        // 窗口关闭时清理资源
        this.Closed += (_, _) => ViewModel.Cleanup();
    }
}
