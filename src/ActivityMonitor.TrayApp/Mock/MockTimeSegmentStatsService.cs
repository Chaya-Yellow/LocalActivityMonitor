using ActivityMonitor.Core.Interfaces;
using ActivityMonitor.Core.Models;

namespace ActivityMonitor.TrayApp.Mock;

/// <summary>
/// Mock 实现 —— 半小时时段聚合统计服务。
/// 生成今日 48 个半小时时段的模拟软件使用分布数据，
/// 涵盖多种软件和网页浏览内容，用于前端 UI 开发。
/// </summary>
public class MockTimeSegmentStatsService : ITimeSegmentStatsService
{
    /// <summary>模拟基准日期。</summary>
    private static DateTime Today => DateTime.Today;

    /// <summary>用于生成稳定模拟数据的随机种子。</summary>
    private static readonly Random Rng = new(42);

    /// <summary>类别颜色字典（用于环形图着色）。</summary>
    private static readonly Dictionary<string, string> CategoryColorMap = new()
    {
        [Category.Web] = "#0078D4",   // 蓝
        [Category.File] = "#107C10",  // 绿
        [Category.App] = "#FF8C00",   // 橙
        [Category.Idle] = "#767676",  // 灰
        [Category.Sleep] = "#4B0082", // 紫
    };

    public Task<List<TimeSegmentStats>> GetTimeSegmentStatsAsync(DateTime date)
    {
        // 为指定日期生成 48 个时段的数据
        var segments = new List<TimeSegmentStats>();
        var baseDate = date.Date;

        for (var hour = 0; hour < 24; hour++)
        {
            for (var minute = 0; minute < 60; minute += 30)
            {
                var segmentStart = baseDate + new TimeSpan(hour, minute, 0);
                segments.Add(GenerateSegment(segmentStart));
            }
        }

        return Task.FromResult(segments);
    }

    /// <summary>
    /// 为单个半小时时段生成模拟的软件使用分布。
    /// 不同时段有不同的活动模式：工作时间活跃、午休空闲、早晚低活跃。
    /// </summary>
    private static TimeSegmentStats GenerateSegment(DateTime segmentStart)
    {
        var hour = segmentStart.Hour;
        var minute = segmentStart.Minute;

        // 根据时段决定活跃系数：工作时间 (09:00-12:00, 13:30-18:00) 高活跃
        var isWorkTime = (hour >= 9 && hour < 12) || (hour >= 13 && hour < 18) ||
                         (hour == 12 && minute >= 30) || (hour == 18 && minute == 0);
        var isLunchTime = hour == 12 && minute == 0; // 12:00-12:29 午餐
        var isMorning = hour >= 5 && hour < 9;
        var isEvening = hour >= 18 && hour < 22;
        var isNight = hour >= 22 || hour < 5;

        // 各时段的软件数量和活跃秒数
        var (softwareCount, totalActiveSeconds) = (isWorkTime, isLunchTime, isMorning, isEvening, isNight) switch
        {
            // 工作时间：3-5 个软件，1500-1750 秒活跃（约 25-29 分钟）
            (true, false, _, _, _) => (Rng.Next(3, 6), Rng.Next(1500, 1750)),
            // 午休 (12:00-12:29)：1-2 个软件，低活跃
            (_, true, _, _, _) => (Rng.Next(1, 3), Rng.Next(30, 300)),
            // 早晨：1-3 个软件，低活跃
            (_, _, true, _, _) => (Rng.Next(1, 3), Rng.Next(60, 600)),
            // 晚上：1-3 个软件，中等活跃
            (_, _, _, true, _) => (Rng.Next(1, 4), Rng.Next(120, 900)),
            // 深夜：0-1 个软件，几乎没有
            (_, _, _, _, true) => (0, 0),
            _ => (0, 0)
        };

        var softwareList = new List<StatsItem>();
        var remainingSeconds = totalActiveSeconds;

        if (softwareCount > 0)
        {
            // 为每个软件分配时长，主软件占大头
            var names = GetSoftwareNames(hour, minute, softwareCount);

            // 确保总时长不超过 totalActiveSeconds
            for (var i = 0; i < names.Count; i++)
            {
                var isMain = i == 0;
                var durationSeconds = isMain
                    ? (int)(totalActiveSeconds * Rng.Next(45, 65) / 100.0)
                    : totalActiveSeconds / (names.Count * 2);

                durationSeconds = Math.Min(durationSeconds, remainingSeconds);
                if (durationSeconds < 60)
                    durationSeconds = 60;
                if (i == names.Count - 1)
                    durationSeconds = Math.Max(60, remainingSeconds);

                var durationMs = durationSeconds * 1000L;

                // 活动 < 1 分钟不列出（前端已过滤，但这里直接生成 >= 1 分钟的数据）
                softwareList.Add(new StatsItem
                {
                    Name = names[i].name,
                    DurationMs = durationMs,
                    Percentage = totalActiveSeconds > 0
                        ? (double)durationMs / (totalActiveSeconds * 1000) * 100
                        : 0,
                    Detail = names[i].detail,
                });

                remainingSeconds -= durationSeconds;
                if (remainingSeconds < 60) break;
            }

            // 按占比降序排列
            softwareList = softwareList.OrderByDescending(s => s.Percentage).ToList();

            // 重新计算占比（归一化到 100%）
            var totalMs = softwareList.Sum(s => s.DurationMs);
            foreach (var item in softwareList)
            {
                item.Percentage = totalMs > 0
                    ? (double)item.DurationMs / totalMs * 100
                    : 0;
            }
        }

        return new TimeSegmentStats
        {
            SegmentStart = segmentStart,
            TotalDurationMs = softwareList.Sum(s => s.DurationMs),
            SoftwareList = softwareList,
        };
    }

    /// <summary>
    /// 根据时段生成软件名称列表，尽早在不同时段使用不同的软件组合，
    /// 使视图展示丰富的软件分布。
    /// </summary>
    private static List<(string name, string? detail)> GetSoftwareNames(int hour, int minute, int count)
    {
        // 用 30 分钟时段索引（0-47）作为种子决定软件组合
        var slotIndex = hour * 2 + (minute / 30);

        // 主软件轮流切换，使视图呈现多样性
        var mainSoftware = (slotIndex % 5) switch
        {
            0 => ("code.exe", @"C:\src\ActivityMonitor\Program.cs"),
            1 => ("chrome.exe", "Google Chrome"),
            2 => ("code.exe", @"C:\src\ActivityMonitor\DashboardViewModel.cs"),
            3 => ("msedge.exe", "Microsoft Edge"),
            4 => ("WindowsTerminal.exe", @"C:\src\ActivityMonitor"),
            _ => ("code.exe", null),
        };

        var secondary = new List<(string name, string? detail)>
        {
            ("chrome.exe", "GitHub Pull Requests"),
            ("Slack.exe", "#project-general"),
            ("notepad++.exe", @"C:\docs\设计文档.md"),
            ("explorer.exe", @"C:\src\ActivityMonitor\"),
            ("WeChat.exe", "工作群消息"),
            ("chrome.exe", "Stack Overflow 搜索"),
            ("Spotify.exe", "专注歌单"),
            ("chrome.exe", "Azure DevOps 构建流水线"),
            ("code.exe", "单元测试"),
            ("chrome.exe", "B站 - 学习视频"),
        };

        // 选择不重复的 secondary 软件
        var selectedSecondary = new List<(string name, string? detail)>();
        var usedIndices = new HashSet<int>();
        for (var i = 0; i < count - 1 && i < secondary.Count; i++)
        {
            var idx = (slotIndex * 7 + i * 13) % secondary.Count;
            if (usedIndices.Add(idx))
                selectedSecondary.Add(secondary[idx]);
        }

        // 组合列表，用主软件和不同 secondary 来填充
        var result = new List<(string name, string? detail)> { mainSoftware };
        result.AddRange(selectedSecondary.Take(count - 1));

        return result;
    }
}
