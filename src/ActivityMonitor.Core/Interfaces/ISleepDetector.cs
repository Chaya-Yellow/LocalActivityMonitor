namespace ActivityMonitor.Core.Interfaces;

/// <summary>
/// 睡眠/休眠检测器接口。
/// 监听系统电源事件（<c>WM_POWERBROADCAST</c>），
/// 在系统进入睡眠/休眠时通知调用方暂停，唤醒时通知恢复。
/// </summary>
public interface ISleepDetector
{
    /// <summary>
    /// 睡眠状态变更事件。参数表示是否处于睡眠状态。
    /// </summary>
    event EventHandler<bool>? OnSleepStateChanged;

    /// <summary>
    /// 当前是否处于睡眠/休眠状态。
    /// </summary>
    bool IsSleeping { get; }

    /// <summary>
    /// 开始监听系统电源事件。
    /// </summary>
    void Start();

    /// <summary>
    /// 停止监听并释放资源。
    /// </summary>
    void Stop();
}
