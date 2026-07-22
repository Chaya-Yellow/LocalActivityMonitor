using System.Windows;
using System.Windows.Controls;

namespace ActivityMonitor.TrayApp.History;

/// <summary>
/// 历史浏览窗口。
/// 支持日/周/月三种视图模式，日视图保留原有日期查看功能，
/// 周视图展示周报 Markdown，月视图展示月度聚合数据。
/// 所有视图均包含自定义柱状图显示各软件时长分布。
/// </summary>
public partial class HistoryWindow : Window
{
    public HistoryViewModel ViewModel { get; }

    public HistoryWindow()
    {
        InitializeComponent();
        ViewModel = new HistoryViewModel();
        DataContext = ViewModel;

        // DatePicker 日期变化时自动加载日视图数据
        DatePickerControl.SelectedDateChanged += OnDatePickerChanged;

        // 窗口加载完成后初始化默认视图（日视图）
        Loaded += OnWindowLoaded;
    }

    /// <summary>
    /// 窗口加载完成后加载默认日视图数据。
    /// </summary>
    private async void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        // SwitchToDayView 负责设置 SelectedViewMode = Day 并加载日数据
        await ViewModel.SwitchToDayViewCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// DatePicker 选择日期变化时触发加载（仅在日视图模式下）。
    /// 重新加载当日数据以反映选中日期的变化。
    /// </summary>
    private async void OnDatePickerChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count <= 0) return;
        if (ViewModel.SelectedViewMode != HistoryViewMode.Day) return;

        // 日期已通过 TwoWay 绑定更新到 ViewModel.SelectedDate，
        // 这里触发重新加载（使用 SwitchToDayView 刷新日数据）
        await ViewModel.SwitchToDayViewCommand.ExecuteAsync(null);
    }
}
