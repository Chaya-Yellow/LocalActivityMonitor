using System.Collections.ObjectModel;
using ActivityMonitor.Core.Interfaces;
using ActivityMonitor.Core.Models;
using ActivityMonitor.TrayApp.Mock;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ActivityMonitor.TrayApp.History;

/// <summary>
/// 历史浏览 ViewModel。
/// 支持按日期查看历史活动记录、日历选择、多日期范围统计。
/// 近 3 天显示完整时间线，3 天以前显示统计概览。
/// </summary>
public partial class HistoryViewModel : ObservableObject
{
    private readonly IActivityRepository _repository;
    private readonly ITodayStatsService _statsService;

    // ──────────────── 可观察属性 ────────────────

    /// <summary>当前选中的日期。</summary>
    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Today;

    /// <summary>范围查询的起始日期（用于多日期选择）。</summary>
    [ObservableProperty]
    private DateTime _rangeStart = DateTime.Today;

    /// <summary>范围查询的结束日期（用于多日期选择）。</summary>
    [ObservableProperty]
    private DateTime _rangeEnd = DateTime.Today;

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

    /// <summary>是否显示时间线视图（近 3 天）还是统计概览视图。</summary>
    [ObservableProperty]
    private bool _isTimelineView = true;

    /// <summary>是否有数据可显示。</summary>
    [ObservableProperty]
    private bool _hasData = true;

    /// <summary>空数据提示文本。</summary>
    [ObservableProperty]
    private string _emptyMessage = string.Empty;

    /// <summary>是否启用范围选择模式。</summary>
    [ObservableProperty]
    private bool _isRangeMode;

    /// <summary>范围统计的总时长文本。</summary>
    [ObservableProperty]
    private string _rangeTotalText = string.Empty;

    /// <summary>范围统计的天数。</summary>
    [ObservableProperty]
    private string _rangeDaysText = string.Empty;

    /// <summary>范围统计的日均时长文本。</summary>
    [ObservableProperty]
    private string _rangeDailyAvgText = string.Empty;

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
    /// 判断日期是否为近 3 天（含今天）。
    /// </summary>
    private static bool IsRecent3Days(DateTime date)
    {
        var diff = (DateTime.Today - date.Date).Days;
        return diff >= 0 && diff <= 2;
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

            // 判断视图模式：近 3 天显示时间线，否则统计概览
            IsTimelineView = IsRecent3Days(SelectedDate);

            // 加载事件和统计
            var events = await _repository.GetByDateAsync(SelectedDate);

            if (events.Count == 0)
            {
                HasData = false;
                EmptyMessage = IsTimelineView
                    ? "📭 当天无活动记录"
                    : "📭 当天无活动记录 — 可能是休息日或未启动监控";
                DateEvents = new ObservableCollection<ActivityEvent>();
                TotalActiveText = "0h 0m";
                EventCountText = "0 条事件";
                AppStats = new ObservableCollection<StatsItem>();
                return;
            }

            HasData = true;
            DateEvents = new ObservableCollection<ActivityEvent>(events);

            // 计算时长（排除空闲和睡眠）
            var totalMs = events
                .Where(e => e.Category != Category.Idle && e.Category != Category.Sleep)
                .Sum(e => e.DurationMs);
            var ts = TimeSpan.FromMilliseconds(totalMs);
            TotalActiveText = ts.TotalHours >= 1
                ? $"{(int)ts.TotalHours}h {ts.Minutes}m"
                : $"{ts.Minutes}m";
            EventCountText = $"{events.Count} 条事件";

            if (!IsTimelineView)
            {
                // 3 天以前：加载统计概览
                try
                {
                    var stats = await _statsService.GetByAppAsync();
                    AppStats = new ObservableCollection<StatsItem>(stats);
                }
                catch
                {
                    AppStats = new ObservableCollection<StatsItem>();
                }
            }
            else
            {
                AppStats = new ObservableCollection<StatsItem>();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HistoryVM] 加载失败: {ex.Message}");
            HasData = false;
            EmptyMessage = $"加载失败：{ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 加载范围统计数据（多日期选择模式）。
    /// </summary>
    [RelayCommand]
    private async Task LoadRangeStatsAsync()
    {
        if (IsLoading) return;
        if (RangeEnd < RangeStart)
        {
            // 如果结束日期早于开始日期，交换
            var temp = RangeEnd;
            RangeEnd = RangeStart;
            RangeStart = temp;
        }

        IsLoading = true;
        try
        {
            var days = (RangeEnd - RangeStart).Days + 1;
            RangeDaysText = $"{days} 天";

            // 加载范围数据
            var events = await _repository.GetByDateRangeAsync(RangeStart, RangeEnd);

            if (events.Count == 0)
            {
                HasData = false;
                EmptyMessage = "所选时间段内无活动记录";
                DateEvents = new ObservableCollection<ActivityEvent>();
                RangeTotalText = "0h 0m";
                RangeDailyAvgText = "0h 0m";
                AppStats = new ObservableCollection<StatsItem>();
                return;
            }

            HasData = true;

            // 聚合统计
            var totalMs = events
                .Where(e => e.Category != Category.Idle && e.Category != Category.Sleep)
                .Sum(e => e.DurationMs);

            var ts = TimeSpan.FromMilliseconds(totalMs);
            RangeTotalText = ts.TotalHours >= 1
                ? $"{(int)ts.TotalHours}h {ts.Minutes}m"
                : $"{ts.Minutes}m";

            var avgTs = TimeSpan.FromMilliseconds(days > 0 ? totalMs / days : 0);
            RangeDailyAvgText = avgTs.TotalHours >= 1
                ? $"{(int)avgTs.TotalHours}h {avgTs.Minutes}m"
                : $"{avgTs.Minutes}m";

            // 按应用聚合
            var appGroups = events
                .Where(e => !string.IsNullOrEmpty(e.ProcessName))
                .GroupBy(e => e.ProcessName!)
                .Select(g => new StatsItem
                {
                    Name = g.Key,
                    DurationMs = g.Where(e => e.Category != Category.Idle && e.Category != Category.Sleep)
                                  .Sum(e => e.DurationMs),
                    Percentage = totalMs > 0
                        ? Math.Round((double)g.Where(e => e.Category != Category.Idle && e.Category != Category.Sleep)
                                             .Sum(e => e.DurationMs) / totalMs * 100, 1)
                        : 0
                })
                .OrderByDescending(s => s.DurationMs)
                .ToList();

            AppStats = new ObservableCollection<StatsItem>(appGroups);

            // 更新时间标签
            string[] weekDays = { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };
            DateLabel = $"{RangeStart:yyyy/M/d} - {RangeEnd:yyyy/M/d} ({days}天)";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HistoryVM] 范围统计加载失败: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 日期选择变化时重新加载。
    /// </summary>
    [RelayCommand]
    private async Task OnDateSelectedAsync()
    {
        IsRangeMode = false;
        await LoadDataAsync();
    }

    /// <summary>
    /// 切换范围选择模式。
    /// </summary>
    [RelayCommand]
    private void ToggleRangeMode()
    {
        IsRangeMode = !IsRangeMode;
        if (!IsRangeMode)
        {
            // 退出范围模式时重新加载当前日期
            _ = OnDateSelectedAsync();
        }
    }

    /// <summary>
    /// 选择前一天。
    /// </summary>
    [RelayCommand]
    private async Task PreviousDayAsync()
    {
        if (IsRangeMode) return;
        SelectedDate = SelectedDate.AddDays(-1);
        RangeStart = SelectedDate;
        RangeEnd = SelectedDate;
        await LoadDataAsync();
    }

    /// <summary>
    /// 选择后一天（不可超过今天）。
    /// </summary>
    [RelayCommand]
    private async Task NextDayAsync()
    {
        if (IsRangeMode) return;
        if (SelectedDate >= DateTime.Today) return;
        SelectedDate = SelectedDate.AddDays(1);
        RangeStart = SelectedDate;
        RangeEnd = SelectedDate;
        await LoadDataAsync();
    }

    /// <summary>
    /// 跳转到今天。
    /// </summary>
    [RelayCommand]
    private async Task GoToTodayAsync()
    {
        IsRangeMode = false;
        SelectedDate = DateTime.Today;
        RangeStart = SelectedDate;
        RangeEnd = SelectedDate;
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
