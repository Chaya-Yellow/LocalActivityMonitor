namespace ActivityMonitor.Core.Classification;

/// <summary>
/// 匹配置信度常量，标记解析结果的准确程度。
/// </summary>
public static class Confidence
{
    /// <summary>完全匹配：可直接从标题中提取域名，解析结果完整可靠。</summary>
    public const string Exact = "exact";

    /// <summary>模糊匹配：仅能解析页面标题，无法提取域名，信息不完整。</summary>
    public const string Fuzzy = "fuzzy";
}
