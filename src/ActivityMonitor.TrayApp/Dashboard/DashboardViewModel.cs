using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Threading;
using ActivityMonitor.Core.Interfaces;
using ActivityMonitor.Core.Models;
using ActivityMonitor.TrayApp.Mock;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ActivityMonitor.TrayApp.Dashboard;

/// <summary>统计列表排序列。</summary>
public enum StatsSortColumn { Name, Duration }

/// <summary>排序方向。</summary>
public enum StatsSortDirection { Ascending, Descending }

/// <summary>
/// Dashboard 主面板 ViewModel。
/// 管理当日时间线、实时统计、监控状态切换和窗口导航。
/// 使用 Mock 数据先行开发，后期替换为真实注入的服务。
/// </summary>
public partial class DashboardViewModel : ObservableObject
{
    // ──────────────── 服务字段 ────────────────
    private readonly ITodayStatsService _statsService;
    private readonly IActivityRepository _repository;
    private readonly IReportExporter _exporter;
    private readonly ITimeSegmentStatsService _timeSegmentService;

    // ──────────────── 定时刷新 ────────────────
    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _refreshTextTimer;

    /// <summary>星期几中文名称。</summary>
    private static readonly string[] WeekDays = { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };

    // ──────────────── 降频控制 ────────────────
    private bool _isReducedFrequency;
    private DateTime _lastRefreshTimestamp = DateTime.Now;
    private const int NormalIntervalMs = 1_000;      // 正常 1 秒
    private const int ReducedIntervalMs = 7_000;     // 降频 7 秒
    private const int ConsecutiveFastThreshold = 30; // 连续 30 次快速刷新后恢复 1 秒
    private int _consecutiveFastRefreshes;

    // ──────────────── 自动刷新暂停 ────────────────
    private bool _isAutoRefreshPaused;
    private DateTime _lastInteractionTime = DateTime.MinValue;
    private const int AutoRefreshResumeDelayMs = 3_000;

    // ──────────────── 误报跟踪 ────────────────
    private readonly HashSet<long> _falsePositiveIds = new();

    /// <summary>类别代码 → 显示名称映射。</summary>
    private static readonly Dictionary<string, string> CategoryDisplayNames = new()
    {
        [Category.Web] = "web (网页)",
        [Category.File] = "file (编辑)",
        [Category.App] = "app (应用)",
        [Category.Idle] = "idle (空闲)",
        [Category.Sleep] = "sleep (睡眠)",
    };

    // ──────────────── 排序状态 ────────────────

    /// <summary>统计列表当前排序列。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NameSortGlyph))]
    [NotifyPropertyChangedFor(nameof(DurationSortGlyph))]
    private StatsSortColumn _sortColumn = StatsSortColumn.Duration;

    /// <summary>统计列表当前排序方向。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NameSortGlyph))]
    [NotifyPropertyChangedFor(nameof(DurationSortGlyph))]
    private StatsSortDirection _sortDirection = StatsSortDirection.Descending;

    /// <summary>名称列排序箭头标识（▲/▼/空）。</summary>
    public string NameSortGlyph =>
        SortColumn == StatsSortColumn.Name
            ? SortDirection == StatsSortDirection.Ascending ? " ▲" : " ▼"
            : "";

    /// <summary>时长列排序箭头标识（▲/▼/空）。</summary>
    public string DurationSortGlyph =>
        SortColumn == StatsSortColumn.Duration
            ? SortDirection == StatsSortDirection.Descending ? " ▲" : " ▼"
            : "";

    // ──────────────── 可观察属性 ────────────────

    /// <summary>今日概览数据（总时长/工作/空闲等）。</summary>
    [ObservableProperty]
    private TodayOverview? _todayOverview;

    /// <summary>上次刷新时间的文本（如"刚刚"、"3秒前"）。</summary>
    [ObservableProperty]
    private string _lastRefreshText = "刚刚";

    /// <summary>数据是否已更新但用户正在交互（W0-M2 交互降频）。</summary>
    [ObservableProperty]
    private bool _hasPendingUpdate;

    /// <summary>监控引擎是否处于空闲/睡眠状态（用于降频）。</summary>
    [ObservableProperty]
    private bool _isIdleOrSleep;

    /// <summary>按应用程序聚合的统计数据。</summary>
    [ObservableProperty]
    private ObservableCollection<StatsItem> _appStats = new();

    /// <summary>按项目聚合的统计数据。</summary>
    [ObservableProperty]
    private ObservableCollection<StatsItem> _projectStats = new();

    /// <summary>按域名聚合的统计数据。</summary>
    [ObservableProperty]
    private ObservableCollection<StatsItem> _domainStats = new();

    /// <summary>按活动类别聚合的统计数据。</summary>
    [ObservableProperty]
    private ObservableCollection<StatsItem> _categoryStats = new();

    // ──────────────── 全量统计缓存（用于搜索过滤） ────────────────
    private ObservableCollection<StatsItem> _allAppStats = new();
    private ObservableCollection<StatsItem> _allProjectStats = new();
    private ObservableCollection<StatsItem> _allDomainStats = new();
    private ObservableCollection<StatsItem> _allCategoryStats = new();

    /// <summary>当天的活动事件列表（时间线数据源）。</summary>
    [ObservableProperty]
    private ObservableCollection<ActivityEvent> _todayEvents = new();

    /// <summary>监控引擎运行状态。变化时触发 <see cref="MonitoringStateChanged"/> 事件。</summary>
    [ObservableProperty]
    private bool _isMonitoring = true;

    /// <summary>监控状态变化事件（供托盘图标联动）。</summary>
    public event Action<bool>? MonitoringStateChanged;

    /// <summary>当前日期标签，如"2026年7月21日 周一"。</summary>
    [ObservableProperty]
    private string _currentDateLabel = string.Empty;

    /// <summary>今日总时长格式化文本，如"5h 50m"。</summary>
    [ObservableProperty]
    private string _todayTotalHours = "0h 0m";

    /// <summary>工作时长格式化文本。</summary>
    [ObservableProperty]
    private string _workHoursText = "0h 0m";

    /// <summary>空闲时长格式化文本。</summary>
    [ObservableProperty]
    private string _idleHoursText = "0h 0m";

    /// <summary>时段聚合统计（48 个半小时段）。</summary>
    [ObservableProperty]
    private ObservableCollection<TimeSegmentStats> _timeSegmentStats = new();

    /// <summary>事件数文本。</summary>
    [ObservableProperty]
    private string _eventCountText = "0 条";

    /// <summary>数据加载状态。</summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>选中的事件（用于编辑/删除操作）。</summary>
    [ObservableProperty]
    private ActivityEvent? _selectedEvent;

    /// <summary>实时统计面板当前选中的视角：0=应用,1=项目,2=网页,3=类别。</summary>
    [ObservableProperty]
    private int _selectedStatsTab;

    /// <summary>统计搜索关键词（实时过滤，不区分大小写）。</summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    public DashboardViewModel()
    {
        // 使用 Mock 实现先行开发，后期替换为 DI 注入
        _statsService = new MockTodayStatsService();
        _repository = new MockActivityRepository();
        _exporter = new MockReportExporter();
        _timeSegmentService = new MockTimeSegmentStatsService();

        // 设置日期标签
        var now = DateTime.Now;
        CurrentDateLabel = $"{now:yyyy年M月d日} {WeekDays[(int)now.DayOfWeek]}";

        // 初始化定时器：每 1 秒刷新一次（W0-M2: 从 30 秒改为 1 秒）
        _refreshTimer = new DispatcherTimer(DispatcherPriority.Normal)
        {
            Interval = TimeSpan.FromMilliseconds(NormalIntervalMs),
        };
        _refreshTimer.Tick += async (_, _) =>
        {
            // 用户停止交互超过 3s 后自动恢复刷新
            TryResumeAutoRefresh();
            if (!_isAutoRefreshPaused)
                await RefreshDataAsync();
        };

        // 首次加载数据
        _ = RefreshDataAsync();

        // 启动定时器
        _refreshTimer.Start();

        // 初始化"最后刷新"文本定时器（每秒更新显示）
        _refreshTextTimer = new DispatcherTimer(DispatcherPriority.Normal)
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _refreshTextTimer.Tick += (_, _) =>
        {
            var elapsed = DateTime.Now - _lastRefreshTimestamp;
            if (elapsed.TotalSeconds < 2)
                LastRefreshText = "刚刚";
            else if (elapsed.TotalSeconds < 60)
                LastRefreshText = $"{(int)elapsed.TotalSeconds} 秒前";
            else
                LastRefreshText = $"{(int)elapsed.TotalMinutes} 分钟前";
        };
        _refreshTextTimer.Start();
    }

    /// <summary>
    /// 辅助方法：将毫秒数格式化为可读文本（如 "2h 30m"）。
    /// </summary>
    private static string FormatDuration(long ms)
    {
        var ts = TimeSpan.FromMilliseconds(ms);
        return ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours}h {ts.Minutes}m"
            : $"{ts.Minutes}m";
    }

    /// <summary>
    /// IsMonitoring 变化时触发事件（供托盘、App.xaml.cs 联动）。
    /// </summary>
    partial void OnIsMonitoringChanged(bool value)
    {
        MonitoringStateChanged?.Invoke(value);
    }

    /// <summary>
    /// IsIdleOrSleep 变化时自动调整刷新频率。
    /// </summary>
    partial void OnIsIdleOrSleepChanged(bool value)
    {
        UpdateRefreshInterval();
    }

    /// <summary>
    /// TodayOverview 变化时更新派生的格式化文本。
    /// </summary>
    partial void OnTodayOverviewChanged(TodayOverview? value)
    {
        if (value != null)
        {
            TodayTotalHours = FormatDuration(value.TotalActiveMs);
            WorkHoursText = FormatDuration(value.WorkMs);
            IdleHoursText = FormatDuration(value.TotalIdleMs);
            EventCountText = $"{value.EventCount} 条";
        }
    }

    /// <summary>
    /// 搜索文本变化时实时过滤统计列表。
    /// </summary>
    partial void OnSearchTextChanged(string value)
    {
        ApplySearchFilter();
        ApplySortToStats();
    }

    /// <summary>
    /// 主数据刷新方法：重新加载当日概览、时间线和所有聚合统计。
    /// </summary>
    [RelayCommand]
    private async Task RefreshDataAsync()
    {
        if (IsLoading) return;
        IsLoading = true;

        try
        {
            // 并行加载概览、时间线和时段聚合
            var overviewTask = _statsService.GetOverviewAsync();
            var eventsTask = _repository.GetTodayEventsAsync();
            var segmentTask = _timeSegmentService.GetTimeSegmentStatsAsync(DateTime.Today);

            await Task.WhenAll(overviewTask, eventsTask, segmentTask);

            // 过滤误报记录
            var allEvents = eventsTask.Result;
            var filteredEvents = allEvents.Where(e => !_falsePositiveIds.Contains(e.Id)).ToList();

            // 更新时间线（过滤误报后）
            TodayEvents = new ObservableCollection<ActivityEvent>(filteredEvents);

            // 从过滤后的事件重建统计和概览
            TodayOverview = overviewTask.Result;
            RebuildFromEvents(filteredEvents);

            // 更新时段聚合统计
            TimeSegmentStats = new ObservableCollection<TimeSegmentStats>(segmentTask.Result);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DashboardVM] 刷新失败: {ex.Message}");
        }
        finally
        {
            IsLoading = false;

            // 更新"最后刷新"文本
            _lastRefreshTimestamp = DateTime.Now;
            LastRefreshText = "刚刚";

            // 降频恢复检测：如果连续 N 次刷新都很顺畅（无卡顿），逐步恢复频率
            _consecutiveFastRefreshes++;
            if (_isReducedFrequency && _consecutiveFastRefreshes >= ConsecutiveFastThreshold)
            {
                _isReducedFrequency = false;
                UpdateRefreshInterval();
            }
        }
    }

    /// <summary>
    /// 切换监控引擎的暂停/恢复状态。
    /// </summary>
    [RelayCommand]
    private void ToggleMonitoring()
    {
        IsMonitoring = !IsMonitoring;
        System.Diagnostics.Debug.WriteLine($"[DashboardVM] 监控状态: {(IsMonitoring ? "运行中" : "已暂停")}");
    }

    /// <summary>
    /// 删除选中的活动事件。
    /// </summary>
    [RelayCommand]
    private async Task DeleteEventAsync(ActivityEvent? evt)
    {
        if (evt == null) return;
        try
        {
            await _repository.DeleteAsync(evt.Id);
            _falsePositiveIds.Remove(evt.Id);
            TodayEvents.Remove(evt);
            // 刷新统计
            await RefreshDataAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DashboardVM] 删除失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 将选中的活动事件标记为误报，误报记录将在实时统计中排除显示。
    /// </summary>
    [RelayCommand]
    private async Task MarkAsFalsePositiveAsync(ActivityEvent? evt)
    {
        if (evt == null || _falsePositiveIds.Contains(evt.Id)) return;

        evt.IsFalsePositive = true;
        _falsePositiveIds.Add(evt.Id);

        try
        {
            await _repository.UpdateAsync(evt);
            TodayEvents.Remove(evt);
            // 从当前过滤后的事件集合重建统计
            RebuildFromEvents(TodayEvents.ToList());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DashboardVM] 标记误报失败: {ex.Message}");
        }
    }

    // ──────────────── 排序 ────────────────

    /// <summary>
    /// 按名称排序（A-Z / Z-A 切换）。
    /// </summary>
    [RelayCommand]
    private void SortByName()
    {
        if (SortColumn == StatsSortColumn.Name)
        {
            // 同列切换方向
            SortDirection = SortDirection == StatsSortDirection.Ascending
                ? StatsSortDirection.Descending
                : StatsSortDirection.Ascending;
        }
        else
        {
            SortColumn = StatsSortColumn.Name;
            SortDirection = StatsSortDirection.Ascending;
        }
        ApplySortToStats();
    }

    /// <summary>
    /// 按时长排序（高→低 / 低→高 切换）。
    /// </summary>
    [RelayCommand]
    private void SortByDuration()
    {
        if (SortColumn == StatsSortColumn.Duration)
        {
            SortDirection = SortDirection == StatsSortDirection.Descending
                ? StatsSortDirection.Ascending
                : StatsSortDirection.Descending;
        }
        else
        {
            SortColumn = StatsSortColumn.Duration;
            SortDirection = StatsSortDirection.Descending;
        }
        ApplySortToStats();
    }

    /// <summary>
    /// 将当前排序应用到四个统计集合。
    /// </summary>
    private void ApplySortToStats()
    {
        AppStats = SortCollection(AppStats);
        ProjectStats = SortCollection(ProjectStats);
        DomainStats = SortCollection(DomainStats);
        CategoryStats = SortCollection(CategoryStats);
    }

    /// <summary>
    /// 对单个统计集合按当前排序设置重新排序。
    /// </summary>
    private ObservableCollection<StatsItem> SortCollection(ObservableCollection<StatsItem> source)
    {
        var list = source.ToList();

        if (SortColumn == StatsSortColumn.Name)
        {
            list.Sort(SortDirection == StatsSortDirection.Ascending
                ? (a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal)
                : (a, b) => string.Compare(b.Name, a.Name, StringComparison.Ordinal));
        }
        else
        {
            list.Sort(SortDirection == StatsSortDirection.Descending
                ? (a, b) => b.DurationMs.CompareTo(a.DurationMs)
                : (a, b) => a.DurationMs.CompareTo(b.DurationMs));
        }

        return new ObservableCollection<StatsItem>(list);
    }

    /// <summary>
    /// 编辑选中的活动事件（弹出编辑对话框，当前为占位实现）。
    /// </summary>
    [RelayCommand]
    private void EditEvent(ActivityEvent? evt)
    {
        if (evt == null) return;
        // TODO: 弹出编辑对话框，修改标题/描述/分类/工作标记
        System.Diagnostics.Debug.WriteLine($"[DashboardVM] 编辑事件: Id={evt.Id}, Title={evt.WindowTitle}");
    }

    /// <summary>
    /// 查看活动事件详情（来源追溯 W0-M6）。
    /// 弹窗显示原始窗口标题、完整进程路径等原始数据。
    /// </summary>
    [RelayCommand]
    private void ViewDetails(ActivityEvent? evt)
    {
        if (evt == null) return;
        var detailsWindow = new ViewDetails.ViewDetailsWindow(evt);
        detailsWindow.Owner = System.Windows.Application.Current.Windows
            .OfType<DashboardWindow>().FirstOrDefault();
        detailsWindow.ShowDialog();
    }

    /// <summary>
    /// 查看时段活动详情（W1-M2）。
    /// 弹窗显示半小时时段内的所有软件活动明细。
    /// </summary>
    [RelayCommand]
    private void ViewTimeSegmentDetail(TimeSegmentStats? segment)
    {
        if (segment == null) return;
        var detailWindow = new Controls.TimeSegmentDetailWindow(segment);
        detailWindow.Owner = System.Windows.Application.Current.Windows
            .OfType<DashboardWindow>().FirstOrDefault();
        detailWindow.ShowDialog();
    }

    /// <summary>
    /// 插入线下活动记录（如开会、电话等不在电脑前的工作）。
    /// </summary>
    [RelayCommand]
    private void InsertOfflineActivity()
    {
        // TODO: 弹出插入对话框，填写线下活动的时间段和描述
        // 临时插入一条占位记录
        var offlineEvent = new ActivityEvent
        {
            StartTime = DateTime.Now,
            EndTime = DateTime.Now.AddMinutes(30),
            DurationMs = 1_800_000,
            Category = Category.App,
            WorkTag = WorkTag.Work,
            ProcessName = "(线下活动)",
            WindowTitle = "线下活动",
            EditedDesc = "请描述活动内容",
        };
        TodayEvents.Add(offlineEvent);
        System.Diagnostics.Debug.WriteLine("[DashboardVM] 插入线下活动占位记录");
    }

    /// <summary>
    /// 打开日报编辑器窗口。
    /// </summary>
    [RelayCommand]
    private void OpenReportEditor()
    {
        var editor = new ReportEditor.ReportEditorWindow();
        editor.Owner = System.Windows.Application.Current.Windows
            .OfType<DashboardWindow>().FirstOrDefault();
        editor.ShowDialog();
    }

    /// <summary>
    /// 打开历史浏览窗口。
    /// </summary>
    [RelayCommand]
    private void OpenHistory()
    {
        var history = new History.HistoryWindow();
        history.Owner = System.Windows.Application.Current.Windows
            .OfType<DashboardWindow>().FirstOrDefault();
        history.ShowDialog();
    }

    /// <summary>
    /// 打开设置窗口。
    /// </summary>
    [RelayCommand]
    private void OpenSettings()
    {
        var settings = new Settings.SettingsWindow();
        settings.Owner = System.Windows.Application.Current.Windows
            .OfType<DashboardWindow>().FirstOrDefault();
        settings.ShowDialog();
    }

    /// <summary>
    /// 清理 ViewModel 资源（关闭定时器等）。
    /// </summary>
    public void Cleanup()
    {
        _refreshTimer.Stop();
        _refreshTextTimer.Stop();
    }

    /// <summary>
    /// 根据空闲/睡眠状态和性能状况动态调整刷新频率。
    /// 空闲/睡眠时降频至 7 秒，正常时 1 秒。
    /// 若被手动降频（单次刷新 > 200ms），也使用 7 秒间隔。
    /// </summary>
    private void UpdateRefreshInterval()
    {
        var newInterval = IsIdleOrSleep || _isReducedFrequency
            ? ReducedIntervalMs
            : NormalIntervalMs;

        if (Math.Abs(_refreshTimer.Interval.TotalMilliseconds - newInterval) > 10)
        {
            _refreshTimer.Interval = TimeSpan.FromMilliseconds(newInterval);
            System.Diagnostics.Debug.WriteLine(
                $"[DashboardVM] 刷新频率调整为 {newInterval}ms (空闲/睡眠={IsIdleOrSleep}, 降频={_isReducedFrequency})");
        }
    }

    /// <summary>
    /// 当单次刷新耗时超过 200ms 时调用此方法降低频率。
    /// 由性能监控机制触发（W0-M2/N1.6）。
    /// </summary>
    public void ReduceRefreshRate()
    {
        if (!_isReducedFrequency)
        {
            _isReducedFrequency = true;
            _consecutiveFastRefreshes = 0;
            UpdateRefreshInterval();
        }
    }

    // ──────────────── 自动刷新暂停 ────────────────

    /// <summary>
    /// 通知 VM 用户开始交互（搜索/翻页），暂停 1s 自动刷新。
    /// </summary>
    public void NotifyInteractionStarted()
    {
        _isAutoRefreshPaused = true;
        _lastInteractionTime = DateTime.Now;
        System.Diagnostics.Debug.WriteLine("[DashboardVM] 用户交互，暂停自动刷新");
    }

    /// <summary>
    /// 通知 VM 用户结束交互（失去焦点），记录时间供 3s 后恢复。
    /// </summary>
    public void NotifyInteractionEnded()
    {
        _lastInteractionTime = DateTime.Now;
        System.Diagnostics.Debug.WriteLine("[DashboardVM] 用户结束交互，3s 后恢复自动刷新");
    }

    /// <summary>
    /// 每次定时器 Tick 调用，检查是否已过 3s 恢复期。
    /// </summary>
    private void TryResumeAutoRefresh()
    {
        if (_isAutoRefreshPaused &&
            (DateTime.Now - _lastInteractionTime).TotalMilliseconds >= AutoRefreshResumeDelayMs)
        {
            _isAutoRefreshPaused = false;
            System.Diagnostics.Debug.WriteLine("[DashboardVM] 恢复自动刷新");
        }
    }

    // ──────────────── 误报处理 ────────────────

    /// <summary>
    /// 从过滤后的事件集合重新计算概览和所有维度统计。
    /// 误报记录不会出现在统计中。
    /// </summary>
    private void RebuildFromEvents(List<ActivityEvent> events)
    {
        // 排除空闲/睡眠后计算活跃统计
        var active = events.Where(e => e.Category != Category.Idle
                                        && e.Category != Category.Sleep).ToList();

        var totalMs = active.Sum(e => e.DurationMs);

        // ── 概览（TodayOverview） ──
        TodayOverview = new TodayOverview
        {
            TotalActiveMs = totalMs,
            TotalIdleMs = events.Where(e => e.Category == Category.Idle).Sum(e => e.DurationMs),
            TotalSleepMs = events.Where(e => e.Category == Category.Sleep).Sum(e => e.DurationMs),
            WorkMs = events.Where(e => e.WorkTag == WorkTag.Work).Sum(e => e.DurationMs),
            NonWorkMs = events
                .Where(e => e.WorkTag != WorkTag.Work
                            && e.Category != Category.Idle
                            && e.Category != Category.Sleep)
                .Sum(e => e.DurationMs),
            EventCount = active.Count,
        };

        // ── 按应用（进程名）统计 ──
        _allAppStats = BuildStats(active
            .Where(e => !string.IsNullOrEmpty(e.ProcessName))
            .GroupBy(e => e.ProcessName!), totalMs, "其他");

        // ── 按项目统计（含项目路径） ──
        var projectGroups = active
            .Where(e => !string.IsNullOrEmpty(e.Project))
            .GroupBy(e => e.Project!).ToList();

        _allProjectStats = BuildStats(projectGroups, totalMs);

        // 为每个项目项填充 Detail（项目目录路径）
        foreach (var item in _allProjectStats)
        {
            var group = projectGroups.FirstOrDefault(g => g.Key == item.Name);
            if (group != null)
                item.Detail = ExtractProjectPath(group);
        }

        // ── 按域名统计 ──
        _allDomainStats = BuildStats(active
            .Where(e => !string.IsNullOrEmpty(e.Domain))
            .GroupBy(e => e.Domain!), totalMs);

        // ── 按类别统计 ──
        _allCategoryStats = new ObservableCollection<StatsItem>(
            events
                .GroupBy(e => e.Category)
                .Select(g => new StatsItem
                {
                    Name = CategoryDisplayNames.TryGetValue(g.Key, out var display)
                        ? display
                        : g.Key,
                    DurationMs = g.Sum(e => e.DurationMs),
                    Percentage = totalMs > 0
                        ? (double)g.Sum(e => e.DurationMs) / totalMs * 100
                        : 0,
                })
                .OrderByDescending(s => s.DurationMs)
                .ToList());

        // 从全量缓存按搜索关键词过滤，然后排序
        ApplySearchFilter();
        ApplySortToStats();
    }

    /// <summary>
    /// 从分组数据构建 StatsItem 列表，包含占比计算和"其他"兜底。
    /// </summary>
    private static ObservableCollection<StatsItem> BuildStats(
        IEnumerable<IGrouping<string, ActivityEvent>> groups, long totalMs, string fallbackName = "")
    {
        var items = new List<StatsItem>();
        var accounted = 0L;

        foreach (var g in groups)
        {
            var ms = g.Sum(e => e.DurationMs);
            accounted += ms;
            items.Add(new StatsItem
            {
                Name = g.Key,
                DurationMs = ms,
                Percentage = totalMs > 0 ? (double)ms / totalMs * 100 : 0,
            });
        }

        // 未归类的余量记为"其他"
        var other = totalMs - accounted;
        if (other > 0)
        {
            items.Add(new StatsItem
            {
                Name = string.IsNullOrEmpty(fallbackName) ? "其他" : fallbackName,
                DurationMs = other,
                Percentage = totalMs > 0 ? (double)other / totalMs * 100 : 0,
            });
        }

        return new ObservableCollection<StatsItem>(
            items.OrderByDescending(s => s.DurationMs));
    }

    // ──────────────── 搜索过滤 ────────────────

    /// <summary>
    /// 根据 <see cref="SearchText"/> 从全量缓存中过滤统计列表。
    /// 不区分大小写，按 Name 字段匹配。
    /// </summary>
    private void ApplySearchFilter()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            AppStats = _allAppStats;
            ProjectStats = _allProjectStats;
            DomainStats = _allDomainStats;
            CategoryStats = _allCategoryStats;
        }
        else
        {
            var filter = SearchText.Trim();
            AppStats = FilterCollection(_allAppStats, filter);
            ProjectStats = FilterCollection(_allProjectStats, filter);
            DomainStats = FilterCollection(_allDomainStats, filter);
            CategoryStats = FilterCollection(_allCategoryStats, filter);
        }
    }

    /// <summary>
    /// 对单个集合按名称文本过滤（不区分大小写）。
    /// </summary>
    private static ObservableCollection<StatsItem> FilterCollection(
        ObservableCollection<StatsItem> source, string filter)
    {
        return new ObservableCollection<StatsItem>(
            source.Where(s => s.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)));
    }

    // ──────────────── 项目重命名 ────────────────

    /// <summary>
    /// 从一组事件中提取可读的项目目录路径。
    /// </summary>
    private static string? ExtractProjectPath(IGrouping<string, ActivityEvent> group)
    {
        // 优先从 Detail（通常是文件路径）中提取目录
        foreach (var evt in group)
        {
            if (!string.IsNullOrEmpty(evt.Detail))
            {
                var dir = Path.GetDirectoryName(evt.Detail);
                if (!string.IsNullOrEmpty(dir))
                    return dir;
            }
        }

        // 回退到进程路径所在目录
        foreach (var evt in group)
        {
            if (!string.IsNullOrEmpty(evt.ProcessPath))
            {
                var dir = Path.GetDirectoryName(evt.ProcessPath);
                if (!string.IsNullOrEmpty(dir))
                    return dir;
            }
        }

        return null;
    }

    /// <summary>
    /// 重命名项目：弹出输入对话框，确认后全局重命名并刷新统计。
    /// </summary>
    [RelayCommand]
    private async Task RenameProjectAsync(StatsItem? item)
    {
        if (item == null || string.IsNullOrEmpty(item.Name)) return;

        var newName = ShowInputDialog(
            "重命名项目",
            $"将 \"{item.Name}\" 重命名为：",
            item.Name);

        if (string.IsNullOrWhiteSpace(newName) || newName.Trim() == item.Name)
            return;

        newName = newName.Trim();

        try
        {
            // 全局重命名：更新当前所有事件的项目名
            foreach (var evt in TodayEvents.Where(e => e.Project == item.Name))
            {
                evt.Project = newName;
                await _repository.UpdateAsync(evt);
            }

            System.Diagnostics.Debug.WriteLine(
                $"[DashboardVM] 项目 \"{item.Name}\" → \"{newName}\" 全局重命名完成");

            // 刷新统计
            await RefreshDataAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DashboardVM] 重命名失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 简单输入对话框（WPF Window 内联创建）。
    /// </summary>
    private static string? ShowInputDialog(string title, string prompt, string defaultValue)
    {
        var textBox = new System.Windows.Controls.TextBox
        {
            Text = defaultValue,
            Margin = new System.Windows.Thickness(4),
        };

        var okButton = new System.Windows.Controls.Button
        {
            Content = "确定",
            Width = 80,
            IsDefault = true,
            Margin = new System.Windows.Thickness(0, 0, 8, 0),
        };

        var cancelButton = new System.Windows.Controls.Button
        {
            Content = "取消",
            Width = 80,
            IsCancel = true,
        };

        string? result = null;
        okButton.Click += (_, _) =>
        {
            result = textBox.Text;
            // 关闭所属窗口
            var w = System.Windows.Window.GetWindow(textBox);
            if (w != null)
                w.DialogResult = true;
        };

        var buttonPanel = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new System.Windows.Thickness(0, 8, 0, 0),
        };
        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);

        var stackPanel = new System.Windows.Controls.StackPanel
        {
            Margin = new System.Windows.Thickness(12),
        };
        stackPanel.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = prompt,
            Margin = new System.Windows.Thickness(0, 0, 0, 8),
        });
        stackPanel.Children.Add(textBox);
        stackPanel.Children.Add(buttonPanel);

        var window = new System.Windows.Window
        {
            Title = title,
            Width = 380,
            SizeToContent = System.Windows.SizeToContent.Height,
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
            Owner = System.Windows.Application.Current.Windows
                .OfType<DashboardWindow>().FirstOrDefault(),
            WindowStyle = System.Windows.WindowStyle.ToolWindow,
            ResizeMode = System.Windows.ResizeMode.NoResize,
            Content = stackPanel,
            ShowInTaskbar = false,
        };

        window.ShowDialog();
        return result;
    }
}
