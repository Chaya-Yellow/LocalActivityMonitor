using System.Windows;
using ActivityMonitor.Core.Models;

namespace ActivityMonitor.TrayApp.ViewDetails;

/// <summary>
/// 来源追溯详情弹窗。
/// 显示活动记录的原始窗口标题、进程路径、进程名、PID 等原始数据，
/// 让用户核实数据来源（F2.6 / W0-M6）。
/// </summary>
public partial class ViewDetailsWindow : Window
{
    public ViewDetailsWindow(ActivityEvent evt)
    {
        InitializeComponent();
        DataContext = evt;

        // 仅在有关联描述时显示详情节
        if (!string.IsNullOrEmpty(evt.Detail))
            DetailSection.Visibility = Visibility.Visible;
    }

    /// <summary>关闭按钮事件。</summary>
    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
