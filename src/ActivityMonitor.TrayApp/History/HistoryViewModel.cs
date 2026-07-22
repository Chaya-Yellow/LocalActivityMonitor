using System.Collections.ObjectModel;
using WpfColor = System.Windows.Media.Color;
using WpfBrush = System.Windows.Media.SolidColorBrush;
using ActivityMonitor.Core.Interfaces;
using ActivityMonitor.Core.Models;
using ActivityMonitor.TrayApp.History.Controls;
using ActivityMonitor.TrayApp.Mock;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ActivityMonitor.TrayApp.History;

/// <summary>
/// 视图模式枚举：日/周/月。
/// </summary>
public enum HistoryViewMode
{
    Day,
    Week,
    Month
}

/// <summary>
/// 历史浏览 ViewModel。
/// 支持日/周/月三种视图模式，日视图保留原有功能，
/// 周视图调用 IWeeklyReportExporter 展示周报，
/// 月视图展示月度聚合数据。
/// </summary>
public partial class HistoryViewModel : ObservableObject
{
    private readonly IActivityRepository _repository;
    private readonly ITodayStatsService _statsService;
    private readonly IDailyStatsService _dailyStatsService;
    private readonly IWeeklyReportExporter _weeklyExporter;

    // ──────────────── 柱状图颜色数组 ────────────────
    /// <summary>为柱状图项目分配颜色的色板（12 色，时长降序循环使用）。</summary>
    private static readonly WpfColor[] BarColors =
    [
        WpfColor.FromRgb(0x00, 0x78, 0xD4),   // 蓝色 Primary
        WpfColor.FromRgb(0x10, 0x7C, 0x10),   // 绿色 Success
        WpfColor.FromRgb(0xFF, 0x8C, 0x00),   // 橙色 Warning
        WpfColor.FromRgb(0x5C, 0x2D, 0x91),   // 紫色
        WpfColor.FromRgb(0x00, 0x82, 0x72),   // 青色
        WpfColor.FromRgb(0xD1, 0x34, 0x38),   // 红色 Danger
        WpfColor.FromRgb(0x2B, 0x57, 0x9A),   // 深蓝
        WpfColor.FromRgb(0x49, 0x82, 0x05),   // 橄榄绿
        WpfColor.FromRgb(0xCA, 0x50, 0x10),   // 深橙
        WpfColor.FromRgb(0x87, 0x64, 0xB8),   // 浅紫
        WpfColor.FromRgb(0x03, 0x83, 0x87),   // 深青
        WpfColor.FromRgb(0x00, 0x55, 0xA4),   // 藏蓝
    ];

    // ──────────────── 可观察属性 ────────────────

    /// <summary>当前视图模式（日/周/月）。</summary>
    [ObservableProperty]
    private HistoryViewMode _selectedViewMode = HistoryViewMode.Day;

    /// <summary>当前选中的日期（日视图）。</summary>
    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Today;

    /// <summary>当前选中的周参考日期（周视图，周的任意一天）。</summary>
    [ObservableProperty]
    private DateTime _selectedWeekDate = DateTime.Today;

    /// <summary>当前选中的月参考日期（月视图，月的任意一天）。</summary>
    [ObservableProperty]
    private DateTime _selectedMonthDate = DateTime.Today;

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

    // ──────────────── 柱状图数据 ────────────────

    /// <summary>日视图软件柱状图数据。</summary>
    [ObservableProperty]
    private ObservableCollection<BarChartItem> _dayBarItems = new();

    /// <summary>周视图软件柱状图数据。</summary>
    [ObservableProperty]
    private ObservableCollection<BarChartItem> _weekBarItems = new();

    /// <summary>月视图软件柱状图数据。</summary>
    [ObservableProperty]
    private ObservableCollection<BarChartItem> _monthBarItems = new();

    // ──────────────── 数据表格 ────────────────

    /// <summary>日视图软件表格数据。</summary>
    [ObservableProperty]
    private ObservableCollection<SoftwareTableItem> _dayTableItems = new();

    /// <summary>周视图软件表格数据。</summary>
    [ObservableProperty]
    private ObservableCollection<SoftwareTableItem> _weekTableItems = new();

    /// <summary>月视图软件表格数据。</summary>
    [ObservableProperty]
    private ObservableCollection<SoftwareTableItem> _monthTableItems = new();

    // ──────────────── 选中联动 ────────────────

    /// <summary>当前在柱状图或表格中选中的软件名。用于双向高亮联动。</summary>
    [ObservableProperty]
    private string _selectedSoftwareName = string.Empty;

    // ──────────────── 总记录数（汇总行用） ────────────────

    /// <summary>日视图总记录数。</summary>
    [ObservableProperty]
    private int _dayTotalRecordCount;

    /// <summary>周视图总记录数。</summary>
    [ObservableProperty]
    private int _weekTotalRecordCount;

    /// <summary>月视图总记录数。</summary>
    [ObservableProperty]
    private int _monthTotalRecordCount;

    // ──────────────── 周视图 ────────────────

    /// <summary>周报 Markdown 文本。</summary>
    [ObservableProperty]
    private string _weeklyReportText = string.Empty;

    /// <summary>周标签（如 "2026年第30周 — 7/20 ~ 7/26"）。</summary>
    [ObservableProperty]
    private string _weekLabel = string.Empty;

    /// <summary>周视图总活跃时长。</summary>
    [ObservableProperty]
    private string _weekTotalText = "0h 0m";

    // ──────────────── 月视图 ────────────────

    /// <summary>月标签（如 "2026年7月"）。</summary>
    [ObservableProperty]
    private string _monthLabel = string.Empty;

    /// <summary>月视图总活跃时长。</summary>
    [ObservableProperty]
    private string _monthTotalText = "0h 0m";

    /// <summary>月视图活跃天数。</summary>
    [ObservableProperty]
    private string _monthActiveDaysText = "0 天";

    public HistoryViewModel()
    {
        _repository = new MockActivityRepository();
        _statsService = new MockTodayStatsService();
        _dailyStatsService = new MockDailyStatsService();
        _weeklyExporter = new MockWeeklyReportExporter();

        UpdateDateLabel();
        UpdateWeekLabel();
        UpdateMonthLabel();
    }

    // ──────────────── 标签更新 ────────────────

    /// <summary>更新日视图日期标签。</summary>
    private void UpdateDateLabel()
    {
        string[] weekDays = ["周日", "周一", "周二", "周三", "周四", "周五", "周六"];
        DateLabel = $"{SelectedDate:yyyy年M月d日} {weekDays[(int)SelectedDate.DayOfWeek]}";
    }

    /// <summary>更新周视图标签。</summary>
    private void UpdateWeekLabel()
    {
        var (monday, sunday) = GetWeekRange(SelectedWeekDate);
        // 计算是第几周（ISO week number or simple）
        var weekNum = GetWeekNumber(monday);
        WeekLabel = $"第{weekNum}周 — {monday:M/d} ~ {sunday:M/d}";
    }

    /// <summary>更新月视图标签。</summary>
    private void UpdateMonthLabel()
    {
        MonthLabel = SelectedMonthDate.ToString("yyyy年M月");
    }

    // ──────────────── 视图切换 ────────────────

    /// <summary>切换到日视图。</summary>
    [RelayCommand]
    private async Task SwitchToDayViewAsync()
    {
        SelectedViewMode = HistoryViewMode.Day;
        IsRangeMode = false;
        SelectedSoftwareName = string.Empty;
        UpdateDateLabel();
        await LoadDayDataAsync();
    }

    /// <summary>切换到周视图。</summary>
    [RelayCommand]
    private async Task SwitchToWeekViewAsync()
    {
        SelectedViewMode = HistoryViewMode.Week;
        IsRangeMode = false;
        SelectedSoftwareName = string.Empty;
        UpdateWeekLabel();
        await LoadWeekDataAsync();
    }

    /// <summary>切换到月视图。</summary>
    [RelayCommand]
    private async Task SwitchToMonthViewAsync()
    {
        SelectedViewMode = HistoryViewMode.Month;
        IsRangeMode = false;
        SelectedSoftwareName = string.Empty;
        UpdateMonthLabel();
        await LoadMonthDataAsync();
    }

    // ──────────────── 日视图数据加载 ────────────────

    /// <summary>判断日期是否为近 3 天（含今天）。</summary>
    private static bool IsRecent3Days(DateTime date)
    {
        var diff = (DateTime.Today - date.Date).Days;
        return diff >= 0 && diff <= 2;
    }

    /// <summary>加载日视图数据（事件列表 + 统计 + 柱状图）。</summary>
    private async Task LoadDayDataAsync()
    {
        if (IsLoading) return;
        IsLoading = true;

        try
        {
            UpdateDateLabel();
            IsTimelineView = IsRecent3Days(SelectedDate);

            var events = await _repository.GetByDateAsync(SelectedDate);

            if (events.Count == 0)
            {
                HasData = false;
                EmptyMessage = IsTimelineView
                    ? "当天无活动记录"
                    : "当天无活动记录 — 可能是休息日或未启动监控";
                DateEvents = new ObservableCollection<ActivityEvent>();
                TotalActiveText = "0h 0m";
                EventCountText = "0 条事件";
                AppStats = new ObservableCollection<StatsItem>();
                DayBarItems = new ObservableCollection<BarChartItem>();
                return;
            }

            HasData = true;
            DateEvents = new ObservableCollection<ActivityEvent>(events);

            var totalMs = events
                .Where(e => e.Category != Category.Idle && e.Category != Category.Sleep)
                .Sum(e => e.DurationMs);
            var ts = TimeSpan.FromMilliseconds(totalMs);
            TotalActiveText = ts.TotalHours >= 1
                ? $"{(int)ts.TotalHours}h {ts.Minutes}m"
                : $"{ts.Minutes}m";
            EventCountText = $"{events.Count} 条事件";

            // 加载软件柱状图数据
            await LoadDayBarChartAsync();

            if (!IsTimelineView)
            {
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
            System.Diagnostics.Debug.WriteLine($"[HistoryVM] 日视图加载失败: {ex.Message}");
            HasData = false;
            EmptyMessage = $"加载失败：{ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>加载日视图柱状图数据（同时构建表格数据和总记录数）。</summary>
    private async Task LoadDayBarChartAsync()
    {
        try
        {
            var softwareStats = await _dailyStatsService.GetSoftwareStatsByDateAsync(SelectedDate);
            DayBarItems = BuildBarChartItems(softwareStats);
            DayTableItems = BuildTableItems(softwareStats);
            DayTotalRecordCount = softwareStats.Sum(s => s.RecordCount);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HistoryVM] 日视图柱状图加载失败: {ex.Message}");
            DayBarItems = new ObservableCollection<BarChartItem>();
            DayTableItems = new ObservableCollection<SoftwareTableItem>();
            DayTotalRecordCount = 0;
        }
    }

    // ──────────────── 周视图数据加载 ────────────────

    /// <summary>加载周视图数据（周报 + 周柱状图）。</summary>
    private async Task LoadWeekDataAsync()
    {
        if (IsLoading) return;
        IsLoading = true;

        try
        {
            UpdateWeekLabel();

            // 并行加载周报 Markdown 和周软件统计
            var reportTask = _weeklyExporter.ExportWeeklyAsync(SelectedWeekDate);
            var barChartTask = LoadWeekBarChartAsync();

            await Task.WhenAll(reportTask, barChartTask);

            WeeklyReportText = reportTask.Result;
            HasData = !string.IsNullOrEmpty(WeeklyReportText);

            // 计算周总时长
            var totalMs = WeekBarItems.Sum(b => b.DurationMs);
            var ts = TimeSpan.FromMilliseconds(totalMs);
            WeekTotalText = ts.TotalHours >= 1
                ? $"{(int)ts.TotalHours}h {ts.Minutes}m"
                : $"{ts.Minutes}m";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HistoryVM] 周视图加载失败: {ex.Message}");
            WeeklyReportText = $"# 加载失败\n\n{ex.Message}";
            HasData = false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>加载周视图柱状图数据（遍历周内每天聚合）。</summary>
    private async Task LoadWeekBarChartAsync()
    {
        try
        {
            var (monday, sunday) = GetWeekRange(SelectedWeekDate);
            var allStats = new Dictionary<string, DailySoftwareStats>(StringComparer.OrdinalIgnoreCase);

            // 遍历周一到周日
            for (var d = monday; d <= sunday; d = d.AddDays(1))
            {
                try
                {
                    var dayStats = await _dailyStatsService.GetSoftwareStatsByDateAsync(d);
                    foreach (var stat in dayStats)
                    {
                        if (allStats.TryGetValue(stat.Name, out var existing))
                        {
                            existing.DurationMs += stat.DurationMs;
                            existing.RecordCount += stat.RecordCount;
                        }
                        else
                        {
                            allStats[stat.Name] = new DailySoftwareStats
                            {
                                Name = stat.Name,
                                DurationMs = stat.DurationMs,
                                RecordCount = stat.RecordCount,
                            };
                        }
                    }
                }
                catch
                {
                    // 某些天没有数据，跳过
                }
            }

            var mergedList = allStats.Values.OrderByDescending(s => s.DurationMs).ToList();

            // 计算百分比
            var totalMs = mergedList.Sum(s => s.DurationMs);
            foreach (var s in mergedList)
            {
                s.Percentage = totalMs > 0
                    ? Math.Round((double)s.DurationMs / totalMs * 100, 1)
                    : 0;
            }

            WeekBarItems = BuildBarChartItems(mergedList);
            WeekTableItems = BuildTableItems(mergedList);
            WeekTotalRecordCount = mergedList.Sum(s => s.RecordCount);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HistoryVM] 周视图柱状图加载失败: {ex.Message}");
            WeekBarItems = new ObservableCollection<BarChartItem>();
            WeekTableItems = new ObservableCollection<SoftwareTableItem>();
            WeekTotalRecordCount = 0;
        }
    }

    // ──────────────── 月视图数据加载 ────────────────

    /// <summary>加载月视图数据（月度聚合统计 + 柱状图）。</summary>
    private async Task LoadMonthDataAsync()
    {
        if (IsLoading) return;
        IsLoading = true;

        try
        {
            UpdateMonthLabel();

            var year = SelectedMonthDate.Year;
            var month = SelectedMonthDate.Month;
            var daysInMonth = DateTime.DaysInMonth(year, month);

            var allStats = new Dictionary<string, DailySoftwareStats>(StringComparer.OrdinalIgnoreCase);
            var activeDays = 0;
            long totalMonthMs = 0;

            // 遍历当月每一天聚合
            for (int day = 1; day <= daysInMonth; day++)
            {
                var date = new DateTime(year, month, day);
                if (date > DateTime.Today) break; // 不查未来日期

                try
                {
                    var dayStats = await _dailyStatsService.GetSoftwareStatsByDateAsync(date);
                    if (dayStats.Count > 0)
                    {
                        activeDays++;
                        foreach (var stat in dayStats)
                        {
                            totalMonthMs += stat.DurationMs;
                            if (allStats.TryGetValue(stat.Name, out var existing))
                            {
                                existing.DurationMs += stat.DurationMs;
                                existing.RecordCount += stat.RecordCount;
                            }
                            else
                            {
                                allStats[stat.Name] = new DailySoftwareStats
                                {
                                    Name = stat.Name,
                                    DurationMs = stat.DurationMs,
                                    RecordCount = stat.RecordCount,
                                };
                            }
                        }
                    }
                }
                catch
                {
                    // 跳过无数据的日期
                }
            }

            var mergedList = allStats.Values.OrderByDescending(s => s.DurationMs).ToList();

            // 计算百分比
            var totalMs = mergedList.Sum(s => s.DurationMs);
            foreach (var s in mergedList)
            {
                s.Percentage = totalMs > 0
                    ? Math.Round((double)s.DurationMs / totalMs * 100, 1)
                    : 0;
            }

            MonthBarItems = BuildBarChartItems(mergedList);
            MonthTableItems = BuildTableItems(mergedList);
            MonthTotalRecordCount = mergedList.Sum(s => s.RecordCount);

            var ts = TimeSpan.FromMilliseconds(totalMonthMs);
            MonthTotalText = ts.TotalHours >= 1
                ? $"{(int)ts.TotalHours}h {ts.Minutes}m"
                : $"{ts.Minutes}m";
            MonthActiveDaysText = $"{activeDays} 天";

            HasData = mergedList.Count > 0;
            if (!HasData)
            {
                EmptyMessage = "当月无活动记录";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HistoryVM] 月视图加载失败: {ex.Message}");
            HasData = false;
            EmptyMessage = $"加载失败：{ex.Message}";
            MonthBarItems = new ObservableCollection<BarChartItem>();
            MonthTableItems = new ObservableCollection<SoftwareTableItem>();
            MonthTotalRecordCount = 0;
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ──────────────── 范围统计 ────────────────

    [RelayCommand]
    private async Task LoadRangeStatsAsync()
    {
        if (IsLoading) return;
        if (RangeEnd < RangeStart)
        {
            var temp = RangeEnd;
            RangeEnd = RangeStart;
            RangeStart = temp;
        }

        IsLoading = true;
        try
        {
            var days = (RangeEnd - RangeStart).Days + 1;
            RangeDaysText = $"{days} 天";

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

            string[] weekDays = ["周日", "周一", "周二", "周三", "周四", "周五", "周六"];
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

    // ──────────────── 日导航 ────────────────

    [RelayCommand]
    private async Task PreviousDayAsync()
    {
        if (IsRangeMode || SelectedViewMode != HistoryViewMode.Day) return;
        SelectedDate = SelectedDate.AddDays(-1);
        RangeStart = SelectedDate;
        RangeEnd = SelectedDate;
        await LoadDayDataAsync();
    }

    [RelayCommand]
    private async Task NextDayAsync()
    {
        if (IsRangeMode || SelectedViewMode != HistoryViewMode.Day) return;
        if (SelectedDate >= DateTime.Today) return;
        SelectedDate = SelectedDate.AddDays(1);
        RangeStart = SelectedDate;
        RangeEnd = SelectedDate;
        await LoadDayDataAsync();
    }

    [RelayCommand]
    private async Task GoToTodayAsync()
    {
        IsRangeMode = false;
        SelectedDate = DateTime.Today;
        RangeStart = SelectedDate;
        RangeEnd = SelectedDate;
        if (SelectedViewMode == HistoryViewMode.Day)
            await LoadDayDataAsync();
    }

    [RelayCommand]
    private async Task OnDateSelectedAsync()
    {
        IsRangeMode = false;
        if (SelectedViewMode == HistoryViewMode.Day)
            await LoadDayDataAsync();
    }

    // ──────────────── 周导航 ────────────────

    [RelayCommand]
    private async Task PreviousWeekAsync()
    {
        SelectedWeekDate = SelectedWeekDate.AddDays(-7);
        await LoadWeekDataAsync();
    }

    [RelayCommand]
    private async Task NextWeekAsync()
    {
        SelectedWeekDate = SelectedWeekDate.AddDays(7);
        await LoadWeekDataAsync();
    }

    [RelayCommand]
    private async Task GoToCurrentWeekAsync()
    {
        SelectedWeekDate = DateTime.Today;
        await LoadWeekDataAsync();
    }

    // ──────────────── 月导航 ────────────────

    [RelayCommand]
    private async Task PreviousMonthAsync()
    {
        SelectedMonthDate = SelectedMonthDate.AddMonths(-1);
        await LoadMonthDataAsync();
    }

    [RelayCommand]
    private async Task NextMonthAsync()
    {
        SelectedMonthDate = SelectedMonthDate.AddMonths(1);
        await LoadMonthDataAsync();
    }

    [RelayCommand]
    private async Task GoToCurrentMonthAsync()
    {
        SelectedMonthDate = DateTime.Today;
        await LoadMonthDataAsync();
    }

    // ──────────────── 其他 ────────────────

    [RelayCommand]
    private void ToggleRangeMode()
    {
        IsRangeMode = !IsRangeMode;
        if (!IsRangeMode)
        {
            _ = OnDateSelectedAsync();
        }
    }

    [RelayCommand]
    private void SelectEvent(ActivityEvent? evt)
    {
        if (evt == null) return;
        System.Diagnostics.Debug.WriteLine($"[HistoryVM] 选中事件: Id={evt.Id}, Title={evt.WindowTitle}");
    }

    /// <summary>选中软件（柱状图/表格联动）。传入软件名；重复点击则取消选中。</summary>
    [RelayCommand]
    private void SelectSoftware(string? name)
    {
        var target = name ?? string.Empty;
        SelectedSoftwareName = (SelectedSoftwareName == target) ? string.Empty : target;
    }

    // ──────────────── 辅助方法 ────────────────

    /// <summary>根据日期获取该周的周一和周日。</summary>
    private static (DateTime monday, DateTime sunday) GetWeekRange(DateTime date)
    {
        var diff = (7 + ((int)date.DayOfWeek - 1)) % 7; // DayOfWeek: Sunday=0, Monday=1
        var monday = date.Date.AddDays(-diff);
        var sunday = monday.AddDays(6);
        return (monday, sunday);
    }

    /// <summary>简单周数计算（以 1 月 1 日为第 1 周开始）。</summary>
    private static int GetWeekNumber(DateTime date)
    {
        var jan1 = new DateTime(date.Year, 1, 1);
        return ((date.DayOfYear + (int)jan1.DayOfWeek - 1) / 7) + 1;
    }

    /// <summary>根据 DailySoftwareStats 列表构建柱状图项。</summary>
    private static ObservableCollection<BarChartItem> BuildBarChartItems(IReadOnlyList<DailySoftwareStats> stats)
    {
        if (stats.Count == 0)
            return new ObservableCollection<BarChartItem>();

        var maxPercentage = stats.Max(s => s.Percentage);
        var items = new List<BarChartItem>();

        for (int i = 0; i < stats.Count; i++)
        {
            var s = stats[i];
            var colorIndex = i % BarColors.Length;

            // 按时长降序从深到浅渐变：越靠前颜色越深
            var baseColor = BarColors[colorIndex];
            var brightnessFactor = 1.0 + (i * 0.05); // 越靠后越亮
            var r = (byte)Math.Min(255, (int)(baseColor.R * brightnessFactor + 30));
            var g = (byte)Math.Min(255, (int)(baseColor.G * brightnessFactor + 30));
            var b = (byte)Math.Min(255, (int)(baseColor.B * brightnessFactor + 30));

            items.Add(new BarChartItem
            {
                Name = s.Name,
                DurationMs = s.DurationMs,
                Percentage = s.Percentage,
                WidthFactor = maxPercentage > 0 ? s.Percentage / maxPercentage : 0,
                BarColor = new WpfBrush(WpfColor.FromRgb(r, g, b)),
            });
        }

        return new ObservableCollection<BarChartItem>(items);
    }

    /// <summary>根据 DailySoftwareStats 列表构建表格项。</summary>
    private static ObservableCollection<SoftwareTableItem> BuildTableItems(IReadOnlyList<DailySoftwareStats> stats)
    {
        if (stats.Count == 0)
            return new ObservableCollection<SoftwareTableItem>();

        var items = stats.Select(s => new SoftwareTableItem
        {
            Name = s.Name,
            DurationMs = s.DurationMs,
            Percentage = s.Percentage,
            RecordCount = s.RecordCount,
        }).ToList();

        return new ObservableCollection<SoftwareTableItem>(items);
    }
}
