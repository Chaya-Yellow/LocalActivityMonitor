using System.Windows;

namespace ActivityMonitor.TrayApp.History;

/// <summary>
/// 历史浏览窗口。
/// 支持按日期查看以往的活动记录，可浏览任意日期的完整时间线和聚合统计。
/// </summary>
public partial class HistoryWindow : Window
{
    public HistoryViewModel ViewModel { get; }

    public HistoryWindow()
    {
        InitializeComponent();
        ViewModel = new HistoryViewModel();
        DataContext = ViewModel;
    }
}
