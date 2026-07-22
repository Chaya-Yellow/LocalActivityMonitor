using System.IO;
using ActivityMonitor.Core.Interfaces;

namespace ActivityMonitor.TrayApp.Mock;

/// <summary>
/// Mock 实现 —— 周报导出器。
/// 生成模拟的 Markdown 周报内容，供 UI 预览使用。
/// </summary>
public class MockWeeklyReportExporter : IWeeklyReportExporter
{
    /// <summary>标记是否要模拟导出失败（用于测试错误状态）。</summary>
    public bool SimulateFailure { get; set; }

    public Task<string> ExportWeeklyAsync(DateTime dateInWeek)
    {
        if (SimulateFailure)
            throw new InvalidOperationException("模拟导出失败：文件被占用。");

        // 计算该周的周一和周日
        var diff = (7 + ((int)dateInWeek.DayOfWeek - 1)) % 7;
        var monday = dateInWeek.Date.AddDays(-diff);
        var sunday = monday.AddDays(6);

        var report = $@"# 周报 {monday:yyyy-MM-dd} ~ {sunday:yyyy-MM-dd}

## 概览

| 指标 | 本周 | 上周 | 环比 |
|------|------|------|------|
| 总活跃时长 | 32h 15m | 30h 10m | +6.9% |
| 日均活跃 | 4h 36m | 4h 18m | +7.0% |
| 工作占比 | 76% | 72% | +4% |
| 事件总数 | 186 | 172 | +8.1% |

## 每日活跃时长

| 日期 | 活跃时长 | 事件数 | 工作占比 |
|------|----------|--------|----------|
| {monday:MM/dd} (周一) | 7h 20m | 42 | 82% |
| {monday.AddDays(1):MM/dd} (周二) | 6h 45m | 38 | 78% |
| {monday.AddDays(2):MM/dd} (周三) | 7h 00m | 40 | 75% |
| {monday.AddDays(3):MM/dd} (周四) | 5h 30m | 32 | 70% |
| {monday.AddDays(4):MM/dd} (周五) | 5h 40m | 34 | 74% |
| {monday.AddDays(5):MM/dd} (周六) | — | — | —% |
| {monday.AddDays(6):MM/dd} (周日) | — | — | —% |

## 软件使用 Top 5

| 软件 | 本周时长 | 上周时长 | 环比 | 日均 |
|------|----------|----------|------|------|
| code.exe | 14h 30m | 13h 10m | +10.1% | 2h 54m |
| chrome.exe | 6h 20m | 5h 50m | +8.6% | 1h 16m |
| WindowsTerminal.exe | 4h 10m | 4h 30m | -7.4% | 50m |
| msedge.exe | 2h 30m | 2h 10m | +15.4% | 30m |
| Slack.exe | 2h 00m | 2h 20m | -14.3% | 24m |

## 项目分布

| 项目 | 本周时长 | 占比 |
|------|----------|------|
| ActivityMonitor | 18h 30m | 57% |
| web-app | 4h 00m | 12% |
| docs | 2h 30m | 8% |
| design-assets | 1h 00m | 3% |
| 其他 | 6h 15m | 19% |

## 域外分析

- github.com: 3h 15m (环比 +20%)
- dev.azure.com: 1h 45m (环比 -10%)
- stackoverflow.com: 45m (环比 +5%)
- bilibili.com: 30m (环比 -25%)
- taobao.com: 20m (环比 -33%)

## 周末活动

周末无记录活动。

## 备注

> 本周新增了 Dashboard 统计视图功能，code.exe 使用量上升明显。
";
        return Task.FromResult(report);
    }

    public async Task<string> ExportWeeklyToFileAsync(DateTime dateInWeek, string? filePath = null)
    {
        var markdown = await ExportWeeklyAsync(dateInWeek);

        // 计算该周的周一
        var diff = (7 + ((int)dateInWeek.DayOfWeek - 1)) % 7;
        var monday = dateInWeek.Date.AddDays(-diff);

        filePath ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            $"ActivityMonitor 周报_{monday:yyyy-MM-dd}.md");

        await File.WriteAllTextAsync(filePath, markdown);
        return filePath;
    }
}
