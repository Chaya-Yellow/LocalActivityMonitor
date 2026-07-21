using System.Collections.ObjectModel;
using ActivityMonitor.Core.Interfaces;
using ActivityMonitor.Core.Models;
using ActivityMonitor.TrayApp.Mock;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ActivityMonitor.TrayApp.History;

/// <summary>
/// 历史浏览 ViewModel。
/// 支持按日期查看历史活动记录、浏览日/周/月聚合数据。
/// 使用 Mock 数据先行开发。
/// </summary>
public partial class HistoryViewModel : ObservableObject
{
    private readonly IActivityRepository _repository;
    private readonly ITodayStatsService _statsService;

    // ──────────────── 可观察属性 ────────────────

    /// <summary>当前选中的日期。</summary>
    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Today;

    /// <summary>选中日期的事件列表。</summary>
    [ObservableProperty]
    private ObservableCollection<ActivityEvent> _dateEvents = new();

    /// <summary>按应用聚合的统计。</summary>
    [ObservableProperty]
    private ObservableCollection<StatsItem> _appStats = new();

    /// <summary>选中日期的总活跃时长文本。</summary>
    [ObservableProperty]
    private string _totalActiveText = "0h 0m";

    /// <summary>选中日期的事件数。</summary>
    [ObservableProperty]
    private string _eventCountText = "0 条";

    /// <summary>当前显示的日期标签。</summary>
    [ObservableProperty]
    private string _dateLabel = string.Empty;

    /// <summary>是否正在加载数据。</summary>
    [ObservableProperty]
    private bool _isLoading;

    public HistoryViewModel()
    {
        _repository = new MockActivityRepository();
        _statsService = new MockTodayStatsService();

        UpdateDateLabel();
        _ = LoadDataAsync();
    }

    /// <summary>
    /// 更新日期标签。
    /// </summary>
    private void UpdateDateLabel()
    {
        string[] weekDays = { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };
        DateLabel = $"{SelectedDate:yyyy年M月d日} {weekDays[(int)SelectedDate.DayOfWeek]}";
    }

    /// <summary>
    /// 加载选中日期的数据。
    /// </summary>
    [RelayCommand]
    private async Task LoadDataAsync()
    {
        if (IsLoading) return;
        IsLoading = true;

        try
        {
            UpdateDateLabel();

            // 加载事件和统计
            var eventsTask = _repository.GetByDateAsync(SelectedDate);
            var statsTask = _statsService.GetByAppAsync(); // Mock 始终返回今天数据

            await Task.WhenAll(eventsTask, statsTask);

            DateEvents = new ObservableCollection<ActivityEvent>(eventsTask.Result);

            // 计算时长
            var totalMs = eventsTask.Result
                .Where(e => e.Category != Category.Idle && e.Category != Category.Sleep)
                .Sum(e => e.DurationMs);
            var ts = TimeSpan.FromMilliseconds(totalMs);
            TotalActiveText = ts.TotalHours >= 1
                ? $"{(int)ts.TotalHours}h {ts.Minutes}m"
                : $"{ts.Minutes}m";
            EventCountText = $"{eventsTask.Result.Count} 条事件";

            AppStats = new ObservableCollection<StatsItem>(statsTask.Result);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HistoryVM] 加载失败: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 选择前一天。
    /// </summary>
    [RelayCommand]
    private async Task PreviousDayAsync()
    {
        SelectedDate = SelectedDate.AddDays(-1);
        await LoadDataAsync();
    }

    /// <summary>
    /// 选择后一天（不可超过今天）。
    /// </summary>
    [RelayCommand]
    private async Task NextDayAsync()
    {
        if (SelectedDate >= DateTime.Today) return;
        SelectedDate = SelectedDate.AddDays(1);
        await LoadDataAsync();
    }

    /// <summary>
    /// 跳转到今天。
    /// </summary>
    [RelayCommand]
    private async Task GoToTodayAsync()
    {
        SelectedDate = DateTime.Today;
        await LoadDataAsync();
    }

    /// <summary>
    /// 选中一条事件（可以在此打开详情编辑）。
    /// </summary>
    [RelayCommand]
    private void SelectEvent(ActivityEvent? evt)
    {
        if (evt == null) return;
        System.Diagnostics.Debug.WriteLine($"[HistoryVM] 选中事件: Id={evt.Id}, Title={evt.WindowTitle}");
    }
}
