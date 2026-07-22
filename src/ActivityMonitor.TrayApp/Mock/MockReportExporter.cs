using System.IO;
using ActivityMonitor.Core.Interfaces;

namespace ActivityMonitor.TrayApp.Mock;

/// <summary>
/// Mock 实现 —— 日报导出器。
/// 生成模拟的 Markdown 日报内容，供 UI 预览和编辑使用。
/// </summary>
public class MockReportExporter : IReportExporter
{
    /// <summary>标记是否要模拟导出失败（用于测试错误状态）。</summary>
    public bool SimulateFailure { get; set; }

    public Task<string> ExportDailyAsync(DateTime date)
    {
        if (SimulateFailure)
            throw new InvalidOperationException("模拟导出失败：文件被占用。");

        // 星期几的中文名称
        string[] weekDays = { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };
        var dayOfWeek = weekDays[(int)date.DayOfWeek];
        var dateStr = date.ToString("yyyy-MM-dd");

        var report = $@"# 工作日报 - {dateStr} ({dayOfWeek})

## 📊 今日概览
- 工作时长：5h 50m
- 空闲/休息：1h 15m
	- 监控暂停：15m（09:30-09:45）
- 睡眠/锁屏：8h 00m
- 工作占比：80% · 非工作占比：20%

## ⏱ 时间线

### 上午
- **09:00 - 09:35** VS Code · ActivityMonitor · 编辑 Program.cs
- **09:35 - 09:50** Chrome · stackoverflow.com · 查 WPF MVVM 用法
- **09:50 - 10:45** VS Code · ActivityMonitor · 编辑 DashboardViewModel.cs
- **10:45 - 11:00** Chrome · bilibili.com · 休息
- **11:00 - 11:30** Edge · dev.azure.com · 查看构建流水线
- **11:30 - 12:00** 终端 · ActivityMonitor · dotnet build & git

### 下午
- **12:00 - 13:15** 午休
- **13:15 - 13:45** Notepad++ · docs · 编写设计文档
- **13:45 - 14:30** Chrome · github.com · 代码审查 PR #42
- **14:30 - 16:00** VS Code · ActivityMonitor · 编写 TimelineControl.xaml
- **16:00 - 16:30** 终端 · ActivityMonitor · git push & dotnet test
- **16:30 - 17:00** Chrome · 个人事务
- **17:00 - 17:30** Slack · 团队沟通

## 📁 项目分布
| 项目 | 时长 | 占比 |
|------|------|------|
| ActivityMonitor | 3h 20m | 54% |
| docs | 30m | 8% |
| web-app | 30m | 8% |

## 📈 应用分布
| 应用 | 时长 | 占比 |
|------|------|------|
| VS Code | 2h 45m | 44% |
| Chrome | 1h 25m | 23% |
| 终端 | 1h 00m | 16% |
| Notepad++ | 30m | 8% |
| Edge | 30m | 8% |
| Slack | 30m | 8% |

## 🌐 网页分类
- stackoverflow.com · 15m
- bilibili.com · 15m
- dev.azure.com · 30m
- github.com · 45m
- taobao.com · 30m

## 📝 手动补充
> 暂无补充内容

## 📌 今日总结
>
";
        return Task.FromResult(report);
    }

    public async Task<string> ExportDailyToFileAsync(DateTime date, string? filePath = null)
    {
        var markdown = await ExportDailyAsync(date);

        // 使用默认路径：桌面/ActivityMonitor 日报_yyyy-MM-dd.md
        filePath ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            $"ActivityMonitor 日报_{date:yyyy-MM-dd}.md");

        await File.WriteAllTextAsync(filePath, markdown);
        return filePath;
    }
}
