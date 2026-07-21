using ActivityMonitor.Core.Interfaces;
using ActivityMonitor.Core.Models;

namespace ActivityMonitor.Core.Classification;

/// <summary>
/// 当日实时统计服务。
/// 实现 <see cref="ITodayStatsService"/>，查询当天 activity_events
/// 按 app / project / domain / category / work_tag 聚合时长和占比。
/// </summary>
public class TodayStatsService : ITodayStatsService
{
    private readonly IActivityRepository _repository;

    public TodayStatsService(IActivityRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <inheritdoc />
    public async Task<List<StatsItem>> GetByAppAsync()
    {
        var events = await GetTodayEventsAsync();
        return Aggregate(events, e => e.ProcessName ?? "unknown");
    }

    /// <inheritdoc />
    public async Task<List<StatsItem>> GetByProjectAsync()
    {
        var events = await GetTodayEventsAsync();
        return Aggregate(events, e =>
        {
            // 项目为空的归为 "未归类"
            if (string.IsNullOrWhiteSpace(e.Project))
            {
                return null;
            }
            return e.Project;
        });
    }

    /// <inheritdoc />
    public async Task<List<StatsItem>> GetByDomainAsync()
    {
        var events = await GetTodayEventsAsync();
        return Aggregate(events, e =>
        {
            // 只统计 web 类别且有域名的
            if (e.Category != Models.Category.Web || string.IsNullOrWhiteSpace(e.Domain))
            {
                return null;
            }
            return e.Domain;
        });
    }

    /// <inheritdoc />
    public async Task<List<StatsItem>> GetByCategoryAsync()
    {
        var events = await GetTodayEventsAsync();
        return Aggregate(events, e => e.Category ?? Models.Category.App);
    }

    /// <inheritdoc />
    public async Task<List<StatsItem>> GetByWorkTagAsync()
    {
        var events = await GetTodayEventsAsync();
        return Aggregate(events, e => e.WorkTag ?? Models.WorkTag.Unknown);
    }

    /// <inheritdoc />
    public async Task<TodayOverview> GetOverviewAsync()
    {
        var events = await GetTodayEventsAsync();

        var overview = new TodayOverview
        {
            EventCount = events.Count,
        };

        foreach (var e in events)
        {
            var cat = e.Category ?? Models.Category.App;
            var wt = e.WorkTag ?? Models.WorkTag.Unknown;

            // 按类别累计
            switch (cat)
            {
                case Models.Category.Idle:
                    overview.TotalIdleMs += e.DurationMs;
                    break;
                case Models.Category.Sleep:
                    overview.TotalSleepMs += e.DurationMs;
                    break;
                default:
                    overview.TotalActiveMs += e.DurationMs;
                    break;
            }

            // 按工作标记累计
            if (wt == Models.WorkTag.Work)
            {
                overview.WorkMs += e.DurationMs;
            }
            else if (wt == Models.WorkTag.Personal || wt == Models.WorkTag.Break)
            {
                overview.NonWorkMs += e.DurationMs;
            }
        }

        return overview;
    }

    /// <summary>
    /// 获取当天的所有活动事件（缓存，避免多次查询）。
    /// </summary>
    private List<ActivityEvent>? _cachedEvents;
    private async Task<List<ActivityEvent>> GetTodayEventsAsync()
    {
        if (_cachedEvents is null)
        {
            _cachedEvents = await _repository.GetTodayEventsAsync();
        }

        return _cachedEvents;
    }

    /// <summary>
    /// 按指定键选择器分组聚合，计算时长和占比。
    /// </summary>
    private static List<StatsItem> Aggregate(
        List<ActivityEvent> events,
        Func<ActivityEvent, string?> keySelector)
    {
        if (events.Count == 0)
        {
            return new List<StatsItem>(0);
        }

        // 按 key 分组求和
        var groups = new Dictionary<string, long>();

        foreach (var e in events)
        {
            var key = keySelector(e);
            if (key is null)
            {
                continue;
            }

            // 空闲和睡眠不计入活跃统计（但会出现在 GetByCategory 中）
            // 不过为了全面，我们还是包含所有类别
            if (!groups.TryGetValue(key, out var current))
            {
                current = 0;
            }

            groups[key] = current + e.DurationMs;
        }

        if (groups.Count == 0)
        {
            return new List<StatsItem>(0);
        }

        // 计算总时长（用于占比）
        var totalMs = groups.Values.Sum();
        if (totalMs <= 0)
        {
            return new List<StatsItem>(0);
        }

        // 构建结果列表，按时长降序排列
        var result = groups
            .Select(g => new StatsItem
            {
                Name = g.Key,
                DurationMs = g.Value,
                Percentage = Math.Round((double)g.Value / totalMs * 100, 1),
            })
            .OrderByDescending(s => s.DurationMs)
            .ToList();

        return result;
    }
}
