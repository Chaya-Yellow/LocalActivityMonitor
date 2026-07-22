using ActivityMonitor.Core.Models;
using Debug = System.Diagnostics.Debug;

namespace ActivityMonitor.Core.Tracking;

/// <summary>
/// 崩溃恢复服务。
/// 通过退出标记文件检测程序是否非正常退出，并在 5 分钟窗口内自动补录缺失记录。
/// <list type="bullet">
///   <item>正常退出时写入标记文件，启动时检测标记是否存在。</item>
///   <item>若标记缺失（异常退出）且距上次记录末端 &lt; 5 分钟 → 生成补录记录。</item>
///   <item>所有异常均被捕获，永不崩溃。</item>
/// </list>
/// </summary>
public sealed class CrashRecoveryService : IDisposable
{
    /// <summary>退出标记文件名。</summary>
    private const string ExitMarkerFileName = "exit_marker.txt";

    /// <summary>暂停标记文件名（W0-M3: 暂停态崩溃恢复）。</summary>
    private const string PauseMarkerFileName = "pause_marker.txt";

    /// <summary>崩溃恢复时间窗口（毫秒）：5 分钟。</summary>
    private static readonly TimeSpan RecoveryWindow = TimeSpan.FromMinutes(5);

    /// <summary>默认起始时间（当无法获取上次记录末端时）。</summary>
    private static readonly DateTime FallbackStart = DateTime.Now;

    private readonly string _markerFilePath;
    private readonly string _pauseMarkerFilePath;

    /// <summary>
    /// 初始化崩溃恢复服务。
    /// </summary>
    /// <param name="basePath">
    /// 标记文件存放目录。通常为 <c>%LOCALAPPDATA%\ActivityMonitor</c>。
    /// 传入 null 时使用默认路径。
    /// </param>
    public CrashRecoveryService(string? basePath = null)
    {
        string dir = basePath
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ActivityMonitor");

        _markerFilePath = Path.Combine(dir, ExitMarkerFileName);
        _pauseMarkerFilePath = Path.Combine(dir, PauseMarkerFileName);
    }

    /// <summary>
    /// 崩溃恢复结果。
    /// </summary>
    public sealed record RecoveryResult
    {
        /// <summary>是否检测到崩溃并进行了恢复。</summary>
        public bool WasCrashed { get; init; }

        /// <summary>补录的活动事件列表（引擎的当前事件结束时间到启动时刻的缺失段落）。</summary>
        public List<ActivityEvent> RecoveredEvents { get; init; } = new();

        /// <summary>上次记录的末端时间（可用于判断从何处开始新记录）。</summary>
        public DateTime? LastEventEndTime { get; init; }

        /// <summary>崩溃发生时间（标记文件最后写入时间）。</summary>
        public DateTime? CrashTime { get; init; }

        /// <summary>距离崩溃过去了多久。</summary>
        public TimeSpan? ElapsedSinceCrash { get; init; }
    }

    // ──────────────────────────────────────────────
    // 主逻辑
    // ──────────────────────────────────────────────

    /// <summary>
    /// 检查上次退出状态并返回恢复结果。
    /// 应当在引擎启动时调用（延迟初始化后）。
    /// </summary>
    /// <returns>恢复结果。</returns>
    public async Task<RecoveryResult> CheckAsync()
    {
        try
        {
            return await Task.Run(() => CheckInternal());
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CrashRecovery] Check error: {ex.Message}");
            return new RecoveryResult();
        }
    }

    /// <summary>
    /// 标记正常退出。在引擎收到停止信号时调用。
    /// </summary>
    /// <param name="lastEventEndTime">最后一条活动事件的结束时间。null 表示当前时间。</param>
    public async Task MarkGracefulExitAsync(DateTime? lastEventEndTime = null)
    {
        try
        {
            await Task.Run(() =>
            {
                WriteMarker(lastEventEndTime ?? DateTime.Now);
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CrashRecovery] MarkGracefulExit error: {ex.Message}");
        }
    }

    /// <summary>
    /// 清除退出标记文件。在正常启动场景下调用。
    /// </summary>
    public async Task ClearMarkerAsync()
    {
        try
        {
            await Task.Run(() =>
            {
                if (File.Exists(_markerFilePath))
                {
                    File.Delete(_markerFilePath);
                    Debug.WriteLine("[CrashRecovery] Marker cleared");
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CrashRecovery] ClearMarker error: {ex.Message}");
        }
    }

    /// <summary>
    /// 检查上次是否处于暂停状态（W0-M3: 暂停态崩溃恢复）。
    /// 暂定时写入 pause_marker.txt，崩溃后重启读取以恢复暂停态。
    /// </summary>
    /// <returns>如果 pause_marker 存在返回 true 表示上次处于暂停状态。</returns>
    public async Task<bool> WasPausedOnCrashAsync()
    {
        try
        {
            return await Task.Run(() => File.Exists(_pauseMarkerFilePath));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CrashRecovery] WasPausedOnCrash error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 标记暂停状态（由 ActivityEngine.PauseAsync 调用）。
    /// </summary>
    public async Task MarkPausedAsync()
    {
        try
        {
            await Task.Run(() => WritePauseMarker());
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CrashRecovery] MarkPaused error: {ex.Message}");
        }
    }

    /// <summary>
    /// 清除暂停标记（由 ActivityEngine.ResumeAsync 调用）。
    /// </summary>
    public async Task ClearPauseMarkerAsync()
    {
        try
        {
            await Task.Run(() =>
            {
                if (File.Exists(_pauseMarkerFilePath))
                {
                    File.Delete(_pauseMarkerFilePath);
                    Debug.WriteLine("[CrashRecovery] Pause marker cleared");
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CrashRecovery] ClearPauseMarker error: {ex.Message}");
        }
    }

    // ──────────────────────────────────────────────
    // 内部实现
    // ──────────────────────────────────────────────

    private RecoveryResult CheckInternal()
    {
        var now = DateTime.Now;

        // 标记文件存在 → 上次是正常退出
        if (File.Exists(_markerFilePath))
        {
            try
            {
                string content = File.ReadAllText(_markerFilePath).Trim();
                File.Delete(_markerFilePath);
                Debug.WriteLine("[CrashRecovery] Clean exit detected");

                if (DateTime.TryParse(content, out var exitTime))
                {
                    return new RecoveryResult
                    {
                        WasCrashed = false,
                        LastEventEndTime = exitTime,
                        CrashTime = exitTime,
                        ElapsedSinceCrash = now - exitTime,
                    };
                }

                return new RecoveryResult { WasCrashed = false };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CrashRecovery] Marker read error: {ex.Message}");
                // 文件损坏视为崩溃
            }
        }

        // ── 标记文件缺失 → 上次非常退出（崩溃/任务管理器结束/断电） ──
        // 尝试从标记文件所在目录获取写入时间信息（如果旧标记存在但被删除）
        // 但实际上文件已不存在，我们只能从可能的旧目录信息获取
        // 这里返回崩溃状态，让调用方（ActivityEngine）决定补录策略
        Debug.WriteLine("[CrashRecovery] Crash detected (exit marker missing)");

        return new RecoveryResult
        {
            WasCrashed = true,
            LastEventEndTime = null, // 调用方应提供上一事件的信息
            CrashTime = null,
        };
    }

    /// <summary>
    /// 在崩溃场景下构建补录事件。
    /// </summary>
    /// <param name="lastEventEndTime">崩溃前最后一条记录的结束时间。</param>
    /// <param name="recoveryStartTime">补录起始时间（通常为引擎启动时间 - 5分钟窗口）。</param>
    /// <returns>补录事件列表。</returns>
    public List<ActivityEvent> BuildRecoveryEvents(DateTime lastEventEndTime, DateTime recoveryStartTime)
    {
        var events = new List<ActivityEvent>();

        try
        {
            // 如果最后记录时间在恢复窗口内
            if (lastEventEndTime >= recoveryStartTime)
            {
                // 情况 1：最后记录完全在窗口内
                events.Add(new ActivityEvent
                {
                    StartTime = lastEventEndTime,
                    EndTime = DateTime.Now,
                    DurationMs = (long)(DateTime.Now - lastEventEndTime).TotalMilliseconds,
                    Category = Category.App,
                    WorkTag = WorkTag.Unknown,
                    IsCrashRecovered = true,
                    Detail = "[Crash Recovery]",
                });
            }
            else
            {
                // 情况 2：存在缺口（recoveryStartTime 到 lastEventEndTime 之间的缺口）
                // 填充缺口记录
                events.Add(new ActivityEvent
                {
                    StartTime = recoveryStartTime,
                    EndTime = lastEventEndTime,
                    DurationMs = (long)(lastEventEndTime - recoveryStartTime).TotalMilliseconds,
                    Category = Category.Idle,
                    WorkTag = WorkTag.Unknown,
                    IsCrashRecovered = true,
                    Detail = "[Crash Recovery Gap]",
                });

                events.Add(new ActivityEvent
                {
                    StartTime = lastEventEndTime,
                    EndTime = DateTime.Now,
                    DurationMs = (long)(DateTime.Now - lastEventEndTime).TotalMilliseconds,
                    Category = Category.App,
                    WorkTag = WorkTag.Unknown,
                    IsCrashRecovered = true,
                    Detail = "[Crash Recovery]",
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CrashRecovery] BuildRecoveryEvents error: {ex.Message}");
        }

        return events;
    }

    // ──────────────────────────────────────────────
    // 标记文件读写
    // ──────────────────────────────────────────────

    private void WriteMarker(DateTime timestamp)
    {
        try
        {
            string? dir = Path.GetDirectoryName(_markerFilePath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(_markerFilePath, timestamp.ToString("O"));
            Debug.WriteLine($"[CrashRecovery] Exit marker written: {timestamp:O}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CrashRecovery] WriteMarker error: {ex.Message}");
        }
    }

    private void WritePauseMarker()
    {
        try
        {
            string? dir = Path.GetDirectoryName(_pauseMarkerFilePath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(_pauseMarkerFilePath, DateTime.Now.ToString("O"));
            Debug.WriteLine($"[CrashRecovery] Pause marker written");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CrashRecovery] WritePauseMarker error: {ex.Message}");
        }
    }

    // ──────────────────────────────────────────────
    // IDisposable
    // ──────────────────────────────────────────────

    public void Dispose()
    {
        // Nothing to dispose.
    }
}
