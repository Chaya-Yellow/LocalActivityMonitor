using System.Windows;
using ActivityMonitor.Core.Interfaces;
using ActivityMonitor.TrayApp.Mock;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ActivityMonitor.TrayApp.ReportEditor;

/// <summary>
/// 日报编辑器 ViewModel。
/// 提供日报的 Markdown 预览/编辑和导出功能。
/// 使用 Mock 数据先行开发，后期替换为真实注入服务。
/// </summary>
public partial class ReportEditorViewModel : ObservableObject
{
    private readonly IReportExporter _exporter;

    // ──────────────── 可观察属性 ────────────────

    /// <summary>日报对应的日期。</summary>
    [ObservableProperty]
    private DateTime _reportDate = DateTime.Today;

    /// <summary>日报 Markdown 内容（支持用户编辑）。</summary>
    [ObservableProperty]
    private string _reportMarkdown = string.Empty;

    /// <summary>日期标签，如"2026年7月21日 的日报"。</summary>
    [ObservableProperty]
    private string _dateLabel = string.Empty;

    /// <summary>是否正在导出。</summary>
    [ObservableProperty]
    private bool _isExporting;

    /// <summary>导出状态信息。</summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>是否显示导出成功状态。</summary>
    [ObservableProperty]
    private bool _isExportSuccess;

    public ReportEditorViewModel()
    {
        _exporter = new MockReportExporter();

        UpdateDateLabel();
        _ = LoadReportAsync();
    }

    /// <summary>
    /// 更新日期标签。
    /// </summary>
    private void UpdateDateLabel()
    {
        string[] weekDays = { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };
        DateLabel = $"{ReportDate:yyyy年M月d日} {weekDays[(int)ReportDate.DayOfWeek]} 的日报";
    }

    /// <summary>
    /// 加载日报 Markdown 内容。
    /// </summary>
    [RelayCommand]
    private async Task LoadReportAsync()
    {
        try
        {
            StatusMessage = "正在生成日报...";
            IsExportSuccess = false;

            ReportMarkdown = await _exporter.ExportDailyAsync(ReportDate);

            StatusMessage = "日报已生成，可在此编辑后导出。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"生成失败：{ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[ReportEditorVM] 加载日报失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 重新生成日报（丢弃用户编辑）。
    /// </summary>
    [RelayCommand]
    private async Task RegenerateReportAsync()
    {
        await LoadReportAsync();
    }

    /// <summary>
    /// 将日报导出为 Markdown 文件。
    /// </summary>
    [RelayCommand]
    private async Task ExportToFileAsync()
    {
        if (IsExporting) return;
        IsExporting = true;
        IsExportSuccess = false;

        try
        {
            // 先写入当前编辑后的内容
            // 使用 Mock 默认导出路径（桌面）
            var filePath = await _exporter.ExportDailyToFileAsync(ReportDate);

            StatusMessage = $"✅ 导出成功：{filePath}";
            IsExportSuccess = true;

            System.Diagnostics.Debug.WriteLine($"[ReportEditorVM] 日报已导出到: {filePath}");
        }
        catch (Exception ex)
        {
            StatusMessage = $"导出失败：{ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[ReportEditorVM] 导出失败: {ex.Message}");
        }
        finally
        {
            IsExporting = false;
        }
    }

    /// <summary>
    /// 复制日报内容到剪贴板。
    /// </summary>
    [RelayCommand]
    private void CopyToClipboard()
    {
        try
        {
            Clipboard.SetText(ReportMarkdown);
            StatusMessage = "✅ 已复制到剪贴板";
            IsExportSuccess = true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"复制失败：{ex.Message}";
        }
    }

    /// <summary>
    /// 选择前一天。
    /// </summary>
    [RelayCommand]
    private async Task PreviousDayAsync()
    {
        ReportDate = ReportDate.AddDays(-1);
        UpdateDateLabel();
        await LoadReportAsync();
    }

    /// <summary>
    /// 选择后一天。
    /// </summary>
    [RelayCommand]
    private async Task NextDayAsync()
    {
        ReportDate = ReportDate.AddDays(1);
        UpdateDateLabel();
        await LoadReportAsync();
    }
}
