namespace ActivityMonitor.Core.Interfaces;

/// <summary>
/// 空闲检测器接口。
/// 通过 <c>GetLastInputInfo</c> 检测用户最后一次键盘/鼠标输入，
/// 超过配置的空闲阈值后触发空闲状态变更事件。
/// </summary>
public interface IIdleDetector
{
    /// <summary>
    /// 空闲状态变更事件。参数表示是否处于空闲状态。
    /// </summary>
    event EventHandler<bool>? OnIdleStateChanged;

    /// <summary>
    /// 当前是否处于空闲状态。
    /// </summary>
    bool IsIdle { get; }

    /// <summary>
    /// 空闲阈值（毫秒）。默认 900000（15 分钟）。
    /// </summary>
    long IdleThresholdMs { get; set; }

    /// <summary>
    /// 距离上次用户输入已过去的毫秒数。
    /// </summary>
    long IdleSinceMs { get; }

    /// <summary>
    /// 开始检测空闲状态。
    /// </summary>
    void Start();

    /// <summary>
    /// 停止检测并释放资源。
    /// </summary>
    void Stop();
}
