using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using ActivityMonitor.Core.Interfaces;
using ActivityMonitor.Core.Models;
using ActivityMonitor.TrayApp.Mock;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ActivityMonitor.TrayApp.ReportEditor;

/// <summary>
/// 操作日志在 ListView 中展示用的 UI 模型。
/// 继承 ObservableObject 以支持窗口标题编辑和编辑模式切换。
/// </summary>
public partial class OperationLogItem : ObservableObject
{
    /// <summary>自增主键。</summary>
    public long Id { get; init; }

    /// <summary>格式化时间戳（HH:mm:ss）。</summary>
    public string TimeFormatted { get; init; } = string.Empty;

    /// <summary>窗口标题（可编辑）。</summary>
    [ObservableProperty]
    private string _windowTitle = string.Empty;

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

    /// <summary>是否处于编辑模式。</summary>
    [ObservableProperty]
    private bool _isEditing;

    /// <summary>编辑前的原始标题（用于取消时恢复）。</summary>
    internal string _titleBeforeEdit = string.Empty;

    /// <summary>对应的原始 OperationLog（用于 UpdateAsync 时保留其他字段）。</summary>
    internal OperationLog _originalLog = null!;
}

/// <summary>
/// 操作日志预览对话框 ViewModel。
/// 从 IOperationLogRepository 加载日志并转换为 UI 展示模型。
/// 支持逐条编辑/删除以及将编辑后的日志嵌入日报并导出。
/// </summary>
public partial class OperationLogPreviewViewModel : ObservableObject
{
    private readonly IOperationLogRepository _repository;
    private readonly IReportExporter _exporter;

    // ──────────────── 可观察属性 ────────────────

    /// <summary>标题字符串，如"2026年7月22日 周三 的操作日志（共 42 条）"。</summary>
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

    /// <summary>导出状态提示信息。</summary>
    [ObservableProperty]
    private string _exportStatus = string.Empty;

    /// <summary>是否导出成功（用于状态栏样式）。</summary>
    [ObservableProperty]
    private bool _isExportSuccess;

    /// <summary>是否正在执行导出操作。</summary>
    [ObservableProperty]
    private bool _isExporting;

    public OperationLogPreviewViewModel()
        : this(new MockOperationLogRepository(), new MockReportExporter()) { }

    public OperationLogPreviewViewModel(IOperationLogRepository repository)
        : this(repository, new MockReportExporter()) { }

    public OperationLogPreviewViewModel(IOperationLogRepository repository, IReportExporter exporter)
    {
        _repository = repository;
        _exporter = exporter;
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

    /// <summary>进入编辑模式：记录原始标题，切换到编辑状态。</summary>
    [RelayCommand]
    private void EditItem(OperationLogItem? item)
    {
        if (item == null) return;
        item._titleBeforeEdit = item.WindowTitle;
        item.IsEditing = true;
    }

    /// <summary>保存编辑：将修改后的标题持久化到数据库。</summary>
    [RelayCommand]
    private async Task SaveItemAsync(OperationLogItem? item)
    {
        if (item == null) return;

        try
        {
            // 构造更新对象，保留原始 detail 不被覆盖
            var updateLog = new OperationLog
            {
                Id = item.Id,
                WindowTitle = item.WindowTitle,
            };
            // 如果原始日志有 detail，保留之
            if (!string.IsNullOrEmpty(item._originalLog.Detail))
                updateLog.Detail = item._originalLog.Detail;

            await _repository.UpdateAsync(updateLog);
            // 同步更新缓存的原始日志
            item._originalLog.WindowTitle = item.WindowTitle;
            item.IsEditing = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OperationLogPreviewVM] 保存失败: {ex.Message}");
            System.Windows.MessageBox.Show($"保存失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>取消编辑：恢复原始标题，退出编辑模式。</summary>
    [RelayCommand]
    private void CancelEdit(OperationLogItem? item)
    {
        if (item == null) return;
        item.WindowTitle = item._titleBeforeEdit;
        item.IsEditing = false;
    }

    /// <summary>删除条目：确认后从 UI 列表和数据库中同时移除。</summary>
    [RelayCommand]
    private async Task DeleteItemAsync(OperationLogItem? item)
    {
        if (item == null) return;

        var result = System.Windows.MessageBox.Show(
            $"确定删除此条日志？\n\n{item.TimeFormatted}  {item.WindowTitle}",
            "确认删除",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            var deleted = await _repository.DeleteAsync(item.Id);
            if (deleted)
            {
                Logs.Remove(item);
                UpdateTitle();
                System.Diagnostics.Debug.WriteLine($"[OperationLogPreviewVM] 已删除 ID={item.Id}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OperationLogPreviewVM] 删除失败: {ex.Message}");
            System.Windows.MessageBox.Show($"删除失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 导出日志到日报：将当前编辑后的日志列表嵌入日报 Markdown，写入文件。
    /// </summary>
    [RelayCommand]
    private async Task ExportToFileAsync()
    {
        if (IsExporting) return;
        IsExporting = true;
        IsExportSuccess = false;
        ExportStatus = "正在导出...";

        try
        {
            // 1. 获取基础日报 Markdown（来自 IReportExporter）
            var baseMarkdown = await _exporter.ExportDailyAsync(QueryDate);

            // 2. 从当前编辑后的日志列表生成操作日志 Markdown
            var logSection = GenerateOperationLogSection();

            // 3. 将日志章节嵌入日报：替换已有章节，若无则追加
            var finalMarkdown = EmbedOperationLogSection(baseMarkdown, logSection);

            // 4. 写入文件
            var filePath = GetExportFilePath(QueryDate);
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            await File.WriteAllTextAsync(filePath, finalMarkdown, Encoding.UTF8);

            ExportStatus = $"导出成功：{filePath}";
            IsExportSuccess = true;
            System.Diagnostics.Debug.WriteLine($"[OperationLogPreviewVM] 日报已导出到: {filePath}");
        }
        catch (Exception ex)
        {
            ExportStatus = $"导出失败：{ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[OperationLogPreviewVM] 导出失败: {ex.Message}");
        }
        finally
        {
            IsExporting = false;
        }
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
                IsLast = index == rawLogs.Count - 1,
                _originalLog = log, // 保留原始引用，供保存时回写
            }).ToList();

            Logs = new ObservableCollection<OperationLogItem>(items);

            UpdateTitle();

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

    // ──────────────── 私有方法 ────────────────

    /// <summary>
    /// 根据当前列表数量更新标题。
    /// </summary>
    private void UpdateTitle()
    {
        string[] weekDays = { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };
        var dayOfWeek = weekDays[(int)QueryDate.DayOfWeek];
        Title = $"{QueryDate:yyyy年M月d日} {dayOfWeek} 的操作日志（共 {Logs.Count} 条）";
    }

    /// <summary>
    /// 从当前编辑后的 Logs 集合生成 Markdown 格式的操作日志章节。
    /// </summary>
    private string GenerateOperationLogSection()
    {
        var sb = new StringBuilder(1024);
        sb.AppendLine("## 📋 操作日志");

        if (Logs.Count == 0)
        {
            sb.AppendLine("（无操作日志记录）");
            return sb.ToString();
        }

        foreach (var item in Logs)
        {
            sb.Append("- **");
            sb.Append(item.TimeFormatted);
            sb.Append("** ");

            if (!string.IsNullOrWhiteSpace(item.Category))
            {
                sb.Append('`');
                sb.Append(item.Category);
                sb.Append("` ");
            }

            sb.Append(item.ProcessName);

            if (!string.IsNullOrWhiteSpace(item.WindowTitle))
            {
                sb.Append(" · ");
                sb.Append(item.WindowTitle);
            }

            // 如果有原始 detail，一并输出
            if (!string.IsNullOrWhiteSpace(item._originalLog.Detail))
            {
                sb.AppendLine();
                sb.Append("  └ ");
                sb.Append(item._originalLog.Detail);
            }

            sb.AppendLine();
        }

        sb.AppendLine();
        sb.Append("> 共 ");
        sb.Append(Logs.Count);
        sb.AppendLine(" 条操作记录");
        sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// 将操作日志章节嵌入基础日报 Markdown。
    /// 若基础日报已包含"## 📋 操作日志"章节，则替换之；否则追加到末尾。
    /// </summary>
    private static string EmbedOperationLogSection(string baseMarkdown, string logSection)
    {
        // 匹配 "## 📋 操作日志" 开头直到下一个 "## " 或文件结尾
        // 同时匹配章节尾部可能存在的 "> 共 N 条操作记录" 行
        const string pattern = @"## 📋 操作日志[\s\S]*?(?=\n## |\Z)";

        if (Regex.IsMatch(baseMarkdown, pattern, RegexOptions.ExplicitCapture))
        {
            return Regex.Replace(baseMarkdown, pattern, logSection.TrimEnd(), RegexOptions.ExplicitCapture);
        }

        // 未找到：追加到末尾
        return baseMarkdown.TrimEnd() + "\n\n" + logSection;
    }

    /// <summary>
    /// 获取导出文件的默认路径。
    /// </summary>
    private static string GetExportFilePath(DateTime date)
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var fileName = $"工作日报_{date:yyyy-MM-dd}.md";
        return Path.Combine(documents, "ActivityMonitor", "Reports", fileName);
    }
}
