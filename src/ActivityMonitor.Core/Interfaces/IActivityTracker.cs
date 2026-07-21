using ActivityMonitor.Core.Models;

namespace ActivityMonitor.Core.Interfaces;

/// <summary>
/// 前台活动追踪器接口。
/// 负责以固定间隔轮询当前前台窗口，检测活动变更并触发事件。
/// </summary>
public interface IActivityTracker
{
    /// <summary>
    /// 当前活动事件发生变更时触发。
    /// 参数为新的活动事件对象，调用方负责持久化。
    /// </summary>
    event EventHandler<ActivityEvent>? OnActivityChanged;

    /// <summary>
    /// 启动追踪。开始以固定间隔轮询前台窗口。
    /// </summary>
    void Start();

    /// <summary>
    /// 停止追踪。释放所有资源并停止轮询。
    /// </summary>
    void Stop();

    /// <summary>
    /// 暂停追踪。保留当前状态但不进行窗口检测。
    /// </summary>
    void Pause();

    /// <summary>
    /// 恢复追踪。从暂停状态继续轮询。
    /// </summary>
    void Resume();

    /// <summary>
    /// 当前是否正在运行。
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// 当前是否已暂停。
    /// </summary>
    bool IsPaused { get; }

    /// <summary>
    /// 轮询间隔（毫秒）。默认 2000。
    /// </summary>
    int PollIntervalMs { get; set; }
}
