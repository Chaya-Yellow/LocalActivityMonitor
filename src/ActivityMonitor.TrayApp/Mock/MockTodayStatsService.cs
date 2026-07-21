using ActivityMonitor.Core.Interfaces;

namespace ActivityMonitor.TrayApp.Mock;

/// <summary>
/// Mock 实现 —— 当日实时统计服务。
/// 提供模拟的实时统计数据以供 UI 开发，后端完成后替换为真实注入。
/// </summary>
public class MockTodayStatsService : ITodayStatsService
{
    /// <summary>模拟基准日期：使用当天。</summary>
    private static DateTime Today => DateTime.Today;

    public Task<List<StatsItem>> GetByAppAsync()
    {
        var items = new List<StatsItem>
        {
            new() { Name = "code.exe", DurationMs = 9_000_000, Percentage = 37.5 },
            new() { Name = "chrome.exe", DurationMs = 6_600_000, Percentage = 27.5 },
            new() { Name = "msedge.exe", DurationMs = 2_400_000, Percentage = 10.0 },
            new() { Name = "notepad++.exe", DurationMs = 1_800_000, Percentage = 7.5 },
            new() { Name = "explorer.exe", DurationMs = 1_200_000, Percentage = 5.0 },
            new() { Name = "slack.exe", DurationMs = 900_000, Percentage = 3.8 },
            new() { Name = "WeChat.exe", DurationMs = 600_000, Percentage = 2.5 },
            new() { Name = "其他", DurationMs = 1_500_000, Percentage = 6.2 },
        };
        return Task.FromResult(items);
    }

    public Task<List<StatsItem>> GetByProjectAsync()
    {
        var items = new List<StatsItem>
        {
            new() { Name = "ActivityMonitor", DurationMs = 7_200_000, Percentage = 30.0 },
            new() { Name = "web-app", DurationMs = 4_800_000, Percentage = 20.0 },
            new() { Name = "docs", DurationMs = 1_800_000, Percentage = 7.5 },
            new() { Name = "design-assets", DurationMs = 1_200_000, Percentage = 5.0 },
            new() { Name = "其他", DurationMs = 3_000_000, Percentage = 12.5 },
        };
        return Task.FromResult(items);
    }

    public Task<List<StatsItem>> GetByDomainAsync()
    {
        var items = new List<StatsItem>
        {
            new() { Name = "github.com", DurationMs = 2_400_000, Percentage = 10.0 },
            new() { Name = "stackoverflow.com", DurationMs = 1_800_000, Percentage = 7.5 },
            new() { Name = "docs.microsoft.com", DurationMs = 1_200_000, Percentage = 5.0 },
            new() { Name = "bilibili.com", DurationMs = 600_000, Percentage = 2.5 },
            new() { Name = "其他", DurationMs = 600_000, Percentage = 2.5 },
        };
        return Task.FromResult(items);
    }

    public Task<List<StatsItem>> GetByCategoryAsync()
    {
        var items = new List<StatsItem>
        {
            new() { Name = "file (编辑)", DurationMs = 10_800_000, Percentage = 45.0 },
            new() { Name = "web (网页)", DurationMs = 6_600_000, Percentage = 27.5 },
            new() { Name = "app (应用)", DurationMs = 3_600_000, Percentage = 15.0 },
        };
        return Task.FromResult(items);
    }

    public Task<List<StatsItem>> GetByWorkTagAsync()
    {
        var items = new List<StatsItem>
        {
            new() { Name = "work (工作)", DurationMs = 16_800_000, Percentage = 70.0 },
            new() { Name = "break (休息)", DurationMs = 2_400_000, Percentage = 10.0 },
            new() { Name = "personal (个人)", DurationMs = 1_800_000, Percentage = 7.5 },
        };
        return Task.FromResult(items);
    }

    public Task<TodayOverview> GetOverviewAsync()
    {
        var overview = new TodayOverview
        {
            TotalActiveMs = 21_000_000,     // 约 5h 50m
            TotalIdleMs = 1_200_000,        // 约 20m
            TotalSleepMs = 28_800_000,      // 约 8h
            WorkMs = 16_800_000,            // 约 4h 40m
            NonWorkMs = 4_200_000,          // 约 1h 10m
            EventCount = 24,
        };
        return Task.FromResult(overview);
    }
}
