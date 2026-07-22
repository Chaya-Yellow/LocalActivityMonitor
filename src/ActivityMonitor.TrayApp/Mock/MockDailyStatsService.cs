using ActivityMonitor.Core.Interfaces;
using ActivityMonitor.Core.Models;

namespace ActivityMonitor.TrayApp.Mock;

/// <summary>
/// Mock 实现 —— 日统计视图服务。
/// 提供模拟的按日查询活动明细和按软件聚合统计的数据，供 UI 开发使用。
/// </summary>
public class MockDailyStatsService : IDailyStatsService
{
    private static DateTime Today => DateTime.Today;
    private static readonly Random _rng = new(42);

    public Task<List<ActivityEvent>> GetDetailByDateAsync(DateTime date)
    {
        // 返回该日期的模拟事件（复用 MockActivityRepository 的生成逻辑）
        var events = GenerateEventsForDate(date);
        return Task.FromResult(events);
    }

    public Task<List<DailySoftwareStats>> GetSoftwareStatsByDateAsync(DateTime date)
    {
        var events = GenerateEventsForDate(date);
        var totalActive = events
            .Where(e => e.Category != Category.Idle && e.Category != Category.Sleep)
            .Sum(e => e.DurationMs);

        var stats = events
            .Where(e => !string.IsNullOrEmpty(e.ProcessName) && e.Category != Category.Idle && e.Category != Category.Sleep)
            .GroupBy(e => e.ProcessName!)
            .Select(g => new DailySoftwareStats
            {
                Name = g.Key,
                DurationMs = g.Sum(e => e.DurationMs),
                RecordCount = g.Count(),
                Percentage = totalActive > 0
                    ? Math.Round((double)g.Sum(e => e.DurationMs) / totalActive * 100, 1)
                    : 0
            })
            .OrderByDescending(s => s.DurationMs)
            .ToList();

        return Task.FromResult(stats);
    }

    /// <summary>
    /// 为指定日期生成模拟活动事件。
    /// 当天生成完整数据集，过去日期生成带随机变化的子集，未来日期返回空。
    /// </summary>
    private static List<ActivityEvent> GenerateEventsForDate(DateTime date)
    {
        var diffDays = (Today - date.Date).Days;
        if (diffDays < 0) return new List<ActivityEvent>(); // 未来日期无数据

        var baseTime = date.Date;
        long nextId = diffDays * 1000 + 100;

        // 基础数据模板
        var templates = new List<(TimeSpan start, TimeSpan end, string category, string process, string title, string project)>
        {
            (new(9,0,0),  new(9,35,0),  Category.File, "code.exe",        "Program.cs - ActivityMonitor - Visual Studio Code", "ActivityMonitor"),
            (new(9,35,0), new(9,50,0),  Category.Web,  "chrome.exe",       "如何在 WPF 中使用 CommunityToolkit.Mvvm - Google Chrome", null!),
            (new(9,50,0), new(10,45,0), Category.File, "code.exe",         "DashboardViewModel.cs - ActivityMonitor - VS Code", "ActivityMonitor"),
            (new(10,45,0),new(11,0,0),  Category.Web,  "chrome.exe",       "B站 - Google Chrome", null!),
            (new(11,0,0), new(11,30,0), Category.App,  "msedge.exe",       "Azure DevOps - Microsoft Edge", "web-app"),
            (new(11,30,0),new(12,0,0),  Category.App,  "WindowsTerminal.exe","Windows PowerShell - C:\\src\\ActivityMonitor", "ActivityMonitor"),
            (new(12,0,0), new(13,15,0), Category.Idle, "",                 "", ""),
            (new(13,15,0),new(13,45,0), Category.File, "notepad++.exe",    "设计文档.md - Notepad++", "docs"),
            (new(13,45,0),new(14,30,0), Category.Web,  "chrome.exe",       "PR #42 - ActivityMonitor - GitHub", "ActivityMonitor"),
            (new(14,30,0),new(16,0,0),  Category.File, "code.exe",         "TimelineControl.xaml - ActivityMonitor - VS Code", "ActivityMonitor"),
            (new(16,0,0), new(16,30,0), Category.App,  "WindowsTerminal.exe","管理员: Windows PowerShell", "ActivityMonitor"),
            (new(16,30,0),new(17,0,0),  Category.Web,  "chrome.exe",       "购物网站 - Google Chrome", null!),
            (new(17,0,0), new(17,30,0), Category.App,  "Slack.exe",        "#project-general - Slack", "ActivityMonitor"),
        };

        var events = new List<ActivityEvent>();
        // 过去日期：随机跳过一些事件来模拟不同的工作日
        foreach (var t in templates)
        {
            if (diffDays > 0 && _rng.NextDouble() < 0.3) continue; // 30% 概率跳过

            var durationMs = (baseTime + t.end - (baseTime + t.start)).TotalMilliseconds;
            events.Add(new ActivityEvent
            {
                Id = nextId++,
                StartTime = baseTime + t.start,
                EndTime = baseTime + t.end,
                DurationMs = (long)durationMs,
                Category = t.category,
                WorkTag = t.category == Category.Idle ? WorkTag.Break : WorkTag.Work,
                ProcessName = string.IsNullOrEmpty(t.process) ? null : t.process,
                WindowTitle = t.title,
                Project = string.IsNullOrEmpty(t.project) ? null : t.project,
            });
        }

        return events;
    }
}
