using System.Collections.ObjectModel;
using System.Windows.Threading;
using ActivityMonitor.Core.Interfaces;
using ActivityMonitor.Core.Models;
using ActivityMonitor.TrayApp.Mock;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ActivityMonitor.TrayApp.Dashboard;

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

    public DashboardViewModel()
    {
        // 使用 Mock 实现先行开发，后期替换为 DI 注入
        _statsService = new MockTodayStatsService();
        _repository = new MockActivityRepository();
        _exporter = new MockReportExporter();

        // 设置日期标签
        var now = DateTime.Now;
        CurrentDateLabel = $"{now:yyyy年M月d日} {WeekDays[(int)now.DayOfWeek]}";

        // 初始化定时器：每 1 秒刷新一次（W0-M2: 从 30 秒改为 1 秒）
        _refreshTimer = new DispatcherTimer(DispatcherPriority.Normal)
        {
            Interval = TimeSpan.FromMilliseconds(NormalIntervalMs),
        };
        _refreshTimer.Tick += async (_, _) => await RefreshDataAsync();

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
    /// 主数据刷新方法：重新加载当日概览、时间线和所有聚合统计。
    /// </summary>
    [RelayCommand]
    private async Task RefreshDataAsync()
    {
        if (IsLoading) return;
        IsLoading = true;

        try
        {
            // 并行加载概览和时间线
            var overviewTask = _statsService.GetOverviewAsync();
            var eventsTask = _repository.GetTodayEventsAsync();
            var appTask = _statsService.GetByAppAsync();
            var projectTask = _statsService.GetByProjectAsync();
            var domainTask = _statsService.GetByDomainAsync();
            var categoryTask = _statsService.GetByCategoryAsync();

            await Task.WhenAll(overviewTask, eventsTask, appTask, projectTask, domainTask, categoryTask);

            // 更新 UI（DispatcherTimer 已在 UI 线程上运行）
            TodayOverview = overviewTask.Result;
            TodayEvents = new ObservableCollection<ActivityEvent>(eventsTask.Result);
            AppStats = new ObservableCollection<StatsItem>(appTask.Result);
            ProjectStats = new ObservableCollection<StatsItem>(projectTask.Result);
            DomainStats = new ObservableCollection<StatsItem>(domainTask.Result);
            CategoryStats = new ObservableCollection<StatsItem>(categoryTask.Result);
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
}
