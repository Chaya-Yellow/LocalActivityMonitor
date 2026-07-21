using ActivityMonitor.Core.Models;

namespace ActivityMonitor.Core.Interfaces;

/// <summary>
/// 活动分类器接口。
/// 根据进程名、窗口标题等规则自动判断活动类别和工作/非工作标记。
/// </summary>
public interface IActivityCategorizer
{
    /// <summary>
    /// 对给定的活动事件进行分类，返回类别和工作标记。
    /// </summary>
    /// <param name="activity">待分类的活动事件（须包含 ProcessName、WindowTitle 等基础信息）。</param>
    /// <returns>元组：(category, workTag)。</returns>
    (string category, string workTag) Classify(ActivityEvent activity);
}
