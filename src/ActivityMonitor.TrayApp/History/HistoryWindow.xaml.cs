using System.Windows;
using System.Windows.Controls;

namespace ActivityMonitor.TrayApp.History;

/// <summary>
/// 历史浏览窗口。
/// 支持按日期查看以往的活动记录，日历选择日期，多日期范围统计。
/// 近 3 天显示完整时间线，3 天以前显示统计概览。
/// </summary>
public partial class HistoryWindow : Window
{
    public HistoryViewModel ViewModel { get; }

    public HistoryWindow()
    {
        InitializeComponent();
        ViewModel = new HistoryViewModel();
        DataContext = ViewModel;

        // DatePicker 日期变化时自动加载数据
        DatePickerControl.SelectedDateChanged += OnDatePickerChanged;
    }

    /// <summary>
    /// DatePicker 选择日期变化时触发加载。
    /// </summary>
    private void OnDatePickerChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is DateTime date)
        {
            // 直接调用 LoadDataCommand（自动处理 IsLoading 屏蔽和异步）
            if (ViewModel.LoadDataCommand.CanExecute(null))
                ViewModel.LoadDataCommand.Execute(null);
        }
    }
}
