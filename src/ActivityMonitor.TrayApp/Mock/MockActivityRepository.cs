using ActivityMonitor.Core.Interfaces;
using ActivityMonitor.Core.Models;

namespace ActivityMonitor.TrayApp.Mock;

/// <summary>
/// Mock 实现 —— 活动事件仓储。
/// 提供模拟的活动事件数据以供 UI 开发，涵盖各种类别和场景。
/// </summary>
public class MockActivityRepository : IActivityRepository
{
    /// <summary>模拟事件 ID 自增计数器。</summary>
    private static long _nextId = 100;

    /// <summary>当天日期（模拟基准）。</summary>
    private static DateTime Today => DateTime.Today;

    /// <summary>
    /// 生成今天从 09:00 到 17:30 的模拟活动事件列表。
    /// </summary>
    private List<ActivityEvent> GenerateTodayEvents()
    {
        var t = Today;

        return new List<ActivityEvent>
        {
            new()
            {
                Id = _nextId++,
                StartTime = t + new TimeSpan(9, 0, 0),
                EndTime = t + new TimeSpan(9, 35, 0),
                DurationMs = 2_100_000,
                Category = Category.File,
                WorkTag = WorkTag.Work,
                SubCategory = "editor",
                ProcessName = "code.exe",
                ProcessPath = @"C:\Program Files\Microsoft VS Code\Code.exe",
                ProcessId = 1234,
                WindowTitle = "Program.cs - ActivityMonitor - Visual Studio Code",
                Project = "ActivityMonitor",
                Detail = @"C:\src\ActivityMonitor\Program.cs",
            },
            new()
            {
                Id = _nextId++,
                StartTime = t + new TimeSpan(9, 35, 0),
                EndTime = t + new TimeSpan(9, 50, 0),
                DurationMs = 900_000,
                Category = Category.Web,
                WorkTag = WorkTag.Work,
                SubCategory = "browser",
                ProcessName = "chrome.exe",
                ProcessPath = @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                ProcessId = 5678,
                WindowTitle = "如何在 WPF 中使用 CommunityToolkit.Mvvm - Google Chrome",
                Domain = "stackoverflow.com",
                Detail = "如何在 WPF 中使用 CommunityToolkit.Mvvm",
            },
            new()
            {
                Id = _nextId++,
                StartTime = t + new TimeSpan(9, 50, 0),
                EndTime = t + new TimeSpan(10, 45, 0),
                DurationMs = 3_300_000,
                Category = Category.File,
                WorkTag = WorkTag.Work,
                SubCategory = "editor",
                ProcessName = "code.exe",
                ProcessPath = @"C:\Program Files\Microsoft VS Code\Code.exe",
                ProcessId = 1234,
                WindowTitle = "DashboardViewModel.cs - ActivityMonitor - Visual Studio Code",
                Project = "ActivityMonitor",
                Detail = @"C:\src\ActivityMonitor\DashboardViewModel.cs",
            },
            new()
            {
                Id = _nextId++,
                StartTime = t + new TimeSpan(10, 45, 0),
                EndTime = t + new TimeSpan(11, 0, 0),
                DurationMs = 900_000,
                Category = Category.Web,
                WorkTag = WorkTag.Break,
                SubCategory = "browser",
                ProcessName = "chrome.exe",
                ProcessPath = @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                ProcessId = 5678,
                WindowTitle = "B站 - Google Chrome",
                Domain = "bilibili.com",
                Detail = "休息时间",
            },
            new()
            {
                Id = _nextId++,
                StartTime = t + new TimeSpan(11, 0, 0),
                EndTime = t + new TimeSpan(11, 30, 0),
                DurationMs = 1_800_000,
                Category = Category.App,
                WorkTag = WorkTag.Work,
                SubCategory = "remote",
                ProcessName = "msedge.exe",
                ProcessPath = @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
                ProcessId = 9012,
                WindowTitle = "Azure DevOps - Microsoft Edge",
                Domain = "dev.azure.com",
                Project = "web-app",
                Detail = "查看构建流水线",
            },
            new()
            {
                Id = _nextId++,
                StartTime = t + new TimeSpan(11, 30, 0),
                EndTime = t + new TimeSpan(12, 0, 0),
                DurationMs = 1_800_000,
                Category = Category.App,
                WorkTag = WorkTag.Work,
                SubCategory = "terminal",
                ProcessName = "WindowsTerminal.exe",
                ProcessPath = @"C:\Program Files\WindowsApps\Microsoft.WindowsTerminal_*\WindowsTerminal.exe",
                ProcessId = 3456,
                WindowTitle = "Windows PowerShell - C:\\src\\ActivityMonitor",
                Project = "ActivityMonitor",
                Detail = "dotnet build & git status",
            },
            new()
            {
                Id = _nextId++,
                StartTime = t + new TimeSpan(12, 0, 0),
                EndTime = t + new TimeSpan(13, 15, 0),
                DurationMs = 4_500_000,
                Category = Category.Idle,
                WorkTag = WorkTag.Break,
                SubCategory = null,
                ProcessName = "",
                WindowTitle = "",
                EditedDesc = "午休",
            },
            new()
            {
                Id = _nextId++,
                StartTime = t + new TimeSpan(13, 15, 0),
                EndTime = t + new TimeSpan(13, 45, 0),
                DurationMs = 1_800_000,
                Category = Category.File,
                WorkTag = WorkTag.Work,
                SubCategory = "editor",
                ProcessName = "notepad++.exe",
                ProcessPath = @"C:\Program Files\Notepad++\notepad++.exe",
                ProcessId = 7890,
                WindowTitle = "设计文档.md - Notepad++",
                Detail = @"C:\docs\design\设计文档.md",
                Project = "docs",
            },
            new()
            {
                Id = _nextId++,
                StartTime = t + new TimeSpan(13, 45, 0),
                EndTime = t + new TimeSpan(14, 30, 0),
                DurationMs = 2_700_000,
                Category = Category.Web,
                WorkTag = WorkTag.Work,
                SubCategory = "browser",
                ProcessName = "chrome.exe",
                ProcessPath = @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                ProcessId = 5678,
                WindowTitle = "PR #42 - ActivityMonitor - GitHub",
                Domain = "github.com",
                Detail = "代码审查 PR #42",
                Project = "ActivityMonitor",
            },
            new()
            {
                Id = _nextId++,
                StartTime = t + new TimeSpan(14, 30, 0),
                EndTime = t + new TimeSpan(16, 0, 0),
                DurationMs = 5_400_000,
                Category = Category.File,
                WorkTag = WorkTag.Work,
                SubCategory = "editor",
                ProcessName = "code.exe",
                ProcessPath = @"C:\Program Files\Microsoft VS Code\Code.exe",
                ProcessId = 1234,
                WindowTitle = "TimelineControl.xaml - ActivityMonitor - Visual Studio Code",
                Project = "ActivityMonitor",
                Detail = @"C:\src\ActivityMonitor\TimelineControl.xaml",
            },
            new()
            {
                Id = _nextId++,
                StartTime = t + new TimeSpan(16, 0, 0),
                EndTime = t + new TimeSpan(16, 30, 0),
                DurationMs = 1_800_000,
                Category = Category.App,
                WorkTag = WorkTag.Work,
                SubCategory = "terminal",
                ProcessName = "WindowsTerminal.exe",
                ProcessPath = @"C:\Program Files\WindowsApps\Microsoft.WindowsTerminal_*\WindowsTerminal.exe",
                ProcessId = 3456,
                WindowTitle = "管理员: Windows PowerShell - C:\\src\\ActivityMonitor",
                Project = "ActivityMonitor",
                Detail = "git push & dotnet test",
            },
            new()
            {
                Id = _nextId++,
                StartTime = t + new TimeSpan(16, 30, 0),
                EndTime = t + new TimeSpan(17, 0, 0),
                DurationMs = 1_800_000,
                Category = Category.Web,
                WorkTag = WorkTag.Personal,
                SubCategory = "browser",
                ProcessName = "chrome.exe",
                ProcessPath = @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                ProcessId = 5678,
                WindowTitle = "购物网站 - Google Chrome",
                Domain = "taobao.com",
                Detail = "个人事务",
            },
            new()
            {
                Id = _nextId++,
                StartTime = t + new TimeSpan(17, 0, 0),
                EndTime = t + new TimeSpan(17, 30, 0),
                DurationMs = 1_800_000,
                Category = Category.App,
                WorkTag = WorkTag.Work,
                SubCategory = null,
                ProcessName = "Slack.exe",
                ProcessPath = @"C:\Users\user\AppData\Local\slack\Slack.exe",
                ProcessId = 1111,
                WindowTitle = "#project-general - Slack",
                Project = "ActivityMonitor",
                Detail = "团队沟通",
            },
        };
    }

    public Task<ActivityEvent> InsertAsync(ActivityEvent @event)
    {
        @event.Id = _nextId++;
        return Task.FromResult(@event);
    }

    public Task InsertBatchAsync(IEnumerable<ActivityEvent> events)
    {
        // Mock: 模拟批量写入成功
        return Task.CompletedTask;
    }

    public Task<List<ActivityEvent>> GetTodayEventsAsync()
    {
        return Task.FromResult(GenerateTodayEvents());
    }

    public Task<List<ActivityEvent>> GetByDateAsync(DateTime date)
    {
        // 如果查询当天，返回模拟数据；否则返回空列表
        return Task.FromResult(date.Date == Today ? GenerateTodayEvents() : new List<ActivityEvent>());
    }

    public Task<List<ActivityEvent>> GetByDateRangeAsync(DateTime start, DateTime end)
    {
        // 模拟范围查询，返回当天的数据
        if (start.Date <= Today && end.Date >= Today)
            return Task.FromResult(GenerateTodayEvents());
        return Task.FromResult(new List<ActivityEvent>());
    }

    public Task UpdateAsync(ActivityEvent @event)
    {
        // Mock: 模拟更新成功
        return Task.CompletedTask;
    }

    public Task DeleteAsync(long id)
    {
        // Mock: 模拟删除成功
        return Task.CompletedTask;
    }

    public Task<DailyStats> GetDailyStatsAsync(DateTime date)
    {
        var today = GenerateTodayEvents();
        var stats = new DailyStats
        {
            TotalActiveMs = today.Where(e => e.Category != Category.Idle && e.Category != Category.Sleep)
                                 .Sum(e => e.DurationMs),
            TotalIdleMs = today.Where(e => e.Category == Category.Idle).Sum(e => e.DurationMs),
            TotalSleepMs = today.Where(e => e.Category == Category.Sleep).Sum(e => e.DurationMs),
            EventCount = today.Count,
        };
        return Task.FromResult(stats);
    }

    public Task<ActivityEvent?> GetByIdAsync(long id)
    {
        var events = GenerateTodayEvents();
        return Task.FromResult(events.FirstOrDefault(e => e.Id == id));
    }
}
