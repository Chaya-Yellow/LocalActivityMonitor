using ActivityMonitor.Core.Interfaces;
using ActivityMonitor.Core.Models;

namespace ActivityMonitor.TrayApp.Mock;

/// <summary>
/// Mock 实现 —— 操作日志仓储。
/// 返回模拟的窗口切换日志数据，用于 UI 预览和开发。
/// 后期替换为真实的 <see cref="ActivityMonitor.Data.Repositories.OperationLogRepository"/>。
/// </summary>
public class MockOperationLogRepository : IOperationLogRepository
{
    /// <summary>模拟基准日期。</summary>
    private readonly DateTime _baseDate;

    public MockOperationLogRepository() : this(DateTime.Today) { }

    public MockOperationLogRepository(DateTime baseDate)
    {
        _baseDate = baseDate;
    }

    // 模拟进程名列表
    private static readonly string[] ProcessNames =
    {
        "code.exe", "chrome.exe", "msedge.exe", "notepad++.exe",
        "explorer.exe", "slack.exe", "devenv.exe", "WindowsTerminal.exe",
        "ms-teams.exe", "firefox.exe", "wechat.exe", "outlook.exe"
    };

    // 模拟窗口标题前缀
    private static readonly string[] WindowTitles =
    {
        "ActivityMonitor - Visual Studio Code",
        "Stack Overflow - Where Developers Learn — Google Chrome",
        "GitHub - ActivityMonitor/ActivityMonitor — Google Chrome",
        "Azure DevOps - Build Pipeline — Microsoft Edge",
        "new 1 - Notepad++",
        "设计文档_v2.md - Notepad++",
        "Slack - #general",
        "Microsoft Teams - 团队会议",
        "Outlook - 收件箱",
        "Windows Terminal - PowerShell",
        "Program.cs - Visual Studio 2022",
        "DashboardWindow.xaml - Visual Studio Code",
        "微信 - 工作群",
        "Settings - Windows",
        "Firefox - MDN Web Docs",
        "PR #42 code review - GitHub — Chrome",
    };

    // 模拟类别
    private static readonly string[] Categories = { "app", "web", "file", "app", "web", "app", "file" };

    // ── 内部内存存储，支持 Update/Delete Mock ──
    private List<OperationLog>? _cachedLogs;
    private DateTime _cachedDate;

    public Task<OperationLog> InsertAsync(OperationLog log)
    {
        log.Id = Random.Shared.NextInt64(1000, 9999);
        return Task.FromResult(log);
    }

    public Task InsertBatchAsync(IEnumerable<OperationLog> logs)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> UpdateAsync(OperationLog log)
    {
        // 在缓存列表中查找并更新
        var existing = _cachedLogs?.FirstOrDefault(l => l.Id == log.Id);
        if (existing != null)
        {
            if (log.WindowTitle != null)
                existing.WindowTitle = log.WindowTitle;
            if (log.Detail != null)
                existing.Detail = log.Detail;
        }
        return Task.FromResult(existing != null);
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(long id)
    {
        var removed = _cachedLogs?.RemoveAll(l => l.Id == id) ?? 0;
        return Task.FromResult(removed > 0);
    }

    /// <summary>
    /// 生成指定日期的模拟操作日志（42 条），时间分布在 08:00 到 18:00 之间。
    /// 内部缓存一份副本，供 UpdateAsync / DeleteAsync 修改。
    /// </summary>
    public Task<List<OperationLog>> GetOperationLogsAsync(DateTime date)
    {
        // 使用缓存（相同日期返回已修改过的列表）
        if (_cachedLogs != null && _cachedDate.Date == date.Date)
            return Task.FromResult(new List<OperationLog>(_cachedLogs));

        var logs = new List<OperationLog>();
        var startOfDay = date.Date;
        var rng = new Random(_baseDate.DayOfYear + date.Day * 7); // 可重现的随机

        // 生成 42 条日志，时间在 08:00 ~ 18:00 之间随机分布
        var baseMinutes = 8 * 60;          // 08:00
        var maxMinutes = 18 * 60 - baseMinutes; // 10 小时 = 600 分钟

        // 生成随机递增的时间戳
        var timePoints = new HashSet<int>();
        while (timePoints.Count < 42)
        {
            timePoints.Add(rng.Next(0, maxMinutes));
        }
        var sortedMinutes = timePoints.OrderBy(m => m).ToList();

        for (var i = 0; i < sortedMinutes.Count; i++)
        {
            var minutes = sortedMinutes[i];
            var timestamp = startOfDay.AddMinutes(baseMinutes + minutes)
                                      .AddSeconds(rng.Next(0, 60));

            var processIdx = rng.Next(ProcessNames.Length);
            var titleIdx = rng.Next(WindowTitles.Length);
            var catIdx = rng.Next(Categories.Length);

            logs.Add(new OperationLog
            {
                Id = i + 1,
                Timestamp = timestamp,
                WindowTitle = WindowTitles[titleIdx],
                ProcessName = ProcessNames[processIdx],
                ProcessId = rng.Next(1000, 30000),
                ProcessPath = $@"C:\Program Files\{ProcessNames[processIdx].Replace(".exe", "")}\{ProcessNames[processIdx]}",
                Category = Categories[catIdx],
                Detail = i % 5 == 0 ? $"详细说明 #{i + 1}" : null
            });
        }

        // 缓存结果供后续 Update/Delete
        _cachedLogs = new List<OperationLog>(logs);
        _cachedDate = date.Date;

        return Task.FromResult(logs);
    }
}
