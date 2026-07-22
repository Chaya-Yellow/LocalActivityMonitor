using System.Collections.ObjectModel;
using System.Windows;
using ActivityMonitor.Core.Interfaces;
using ActivityMonitor.TrayApp.Mock;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ActivityMonitor.TrayApp.ReportEditor;

/// <summary>
/// 操作日志在 ListView 中展示用的 UI 模型。
/// 对原始 <see cref="ActivityMonitor.Core.Models.OperationLog"/> 做格式化处理。
/// </summary>
public class OperationLogItem
{
    /// <summary>自增主键。</summary>
    public long Id { get; init; }

    /// <summary>格式化时间戳（HH:mm:ss）。</summary>
    public string TimeFormatted { get; init; } = string.Empty;

    /// <summary>窗口标题。</summary>
    public string WindowTitle { get; init; } = string.Empty;

    /// <summary>进程名。</summary>
    public string ProcessName { get; init; } = string.Empty;

    /// <summary>进程 ID。</summary>
    public int? ProcessId { get; init; }

    /// <summary>活动类别。</summary>
    public string? Category { get; init; }

    /// <summary>活动类别的中文标签（用于显示徽章）。</summary>
    public string CategoryLabel => Category switch
    {
        "app" => "应用",
        "web" => "网页",
        "file" => "文件",
        _ => Category ?? "未知"
    };

    /// <summary>类别徽章颜色。</summary>
    public string CategoryColor => Category switch
    {
        "app" => "#E8F5E9",
        "web" => "#E3F2FD",
        "file" => "#FFF3E0",
        _ => "#F3E5F5"
    };

    /// <summary>是否是第一条记录（用于时间线样式首项 dot 高亮）。</summary>
    public bool IsFirst { get; set; }

    /// <summary>是否是最后一条记录。</summary>
    public bool IsLast { get; set; }
}

/// <summary>
/// 操作日志预览对话框 ViewModel。
/// 从 IOperationLogRepository 加载日志并转换为 UI 展示模型。
/// </summary>
public partial class OperationLogPreviewViewModel : ObservableObject
{
    private readonly IOperationLogRepository _repository;

    // ──────────────── 可观察属性 ────────────────

    /// <summary>标题字符串，如"2026年7月22日 的操作日志（共 42 条）"。</summary>
    [ObservableProperty]
    private string _title = "操作日志预览";

    /// <summary>转换后的日志列表（供 ListView 绑定）。</summary>
    [ObservableProperty]
    private ObservableCollection<OperationLogItem> _logs = new();

    /// <summary>是否正在加载。</summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>是否已完成加载。</summary>
    [ObservableProperty]
    private bool _isLoaded;

    /// <summary>查询日期。</summary>
    [ObservableProperty]
    private DateTime _queryDate = DateTime.Today;

    public OperationLogPreviewViewModel()
        : this(new MockOperationLogRepository()) { }

    public OperationLogPreviewViewModel(IOperationLogRepository repository)
    {
        _repository = repository;
    }

    // ──────────────── 命令 ────────────────

    /// <summary>由 code-behind 注入的关闭窗口委托。</summary>
    public Action? CloseWindowAction { get; set; }

    /// <summary>关闭窗口（通过 CloseWindowAction 委托在 code-behind 中执行实际关闭）。</summary>
    [RelayCommand]
    private void Close()
    {
        CloseWindowAction?.Invoke();
    }

    // ──────────────── 公开方法 ────────────────

    /// <summary>
    /// 异步加载指定日期的操作日志并填充列表。
    /// </summary>
    /// <param name="date">查询日期。</param>
    public async Task LoadLogsAsync(DateTime date)
    {
        QueryDate = date;
        IsLoading = true;
        IsLoaded = false;

        try
        {
            var rawLogs = await _repository.GetOperationLogsAsync(date);

            var items = rawLogs.Select((log, index) => new OperationLogItem
            {
                Id = log.Id,
                TimeFormatted = log.Timestamp.ToString("HH:mm:ss"),
                WindowTitle = log.WindowTitle ?? "(无标题)",
                ProcessName = log.ProcessName ?? "(未知进程)",
                ProcessId = log.ProcessId,
                Category = log.Category,
                IsFirst = index == 0,
                IsLast = index == rawLogs.Count - 1
            }).ToList();

            Logs = new ObservableCollection<OperationLogItem>(items);

            string[] weekDays = { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };
            var dayOfWeek = weekDays[(int)date.DayOfWeek];
            Title = $"{date:yyyy年M月d日} {dayOfWeek} 的操作日志（共 {items.Count} 条）";

            IsLoaded = true;
        }
        catch (Exception ex)
        {
            Logs = new ObservableCollection<OperationLogItem>();
            Title = $"加载失败：{ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[OperationLogPreviewVM] 加载日志失败: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }
}
