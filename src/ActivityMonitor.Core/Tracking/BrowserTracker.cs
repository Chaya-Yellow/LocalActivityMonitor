namespace ActivityMonitor.Core.Tracking;

/// <summary>
/// 浏览器标签页追踪器（Phase 1 — 标题截断解析）。
/// 从浏览器窗口标题中解析页面标题和域名，支持 Chrome/Edge/Firefox。
/// 通过浏览器扩展获取完整 URL 为 Phase 3+ 增强。
/// </summary>
public class BrowserTracker
{
    // 已知浏览器窗口标题后缀及其对应的浏览器类型
    private static readonly (string suffix, BrowserKind kind)[] BrowserSuffixes =
    {
        (" - Google Chrome",  BrowserKind.Chrome),
        (" - Chromium",       BrowserKind.Chrome),
        (" - Microsoft Edge", BrowserKind.Edge),
        (" - Mozilla Firefox", BrowserKind.Firefox),
    };

    // 隐私/无痕窗口标记词
    private static readonly string[] PrivateMarkers =
    {
        "无痕浏览", "无痕模式", "Incognito", "InPrivate", "隐私浏览", "隐私模式",
    };

    /// <summary>
    /// 判断指定进程名是否属于已知浏览器。
    /// </summary>
    public static bool IsBrowser(string processName)
    {
        return DetectBrowserKind(processName) != BrowserKind.Unknown;
    }

    /// <summary>
    /// 解析浏览器窗口标题，返回页面标题和域名等信息。
    /// </summary>
    /// <param name="windowTitle">浏览器窗口标题完整字符串。</param>
    /// <param name="processName">进程名（如 "chrome.exe"、"msedge.exe"）。</param>
    /// <returns>解析结果；若无法解析则返回 null。</returns>
    public BrowserInfo? Parse(string windowTitle, string processName)
    {
        if (string.IsNullOrWhiteSpace(windowTitle))
        {
            return null;
        }

        var kind = DetectBrowserKind(processName);
        if (kind == BrowserKind.Unknown)
        {
            return null;
        }

        var pageTitle = StripBrowserSuffix(windowTitle, kind);
        if (string.IsNullOrWhiteSpace(pageTitle))
        {
            return null;
        }

        var domain = ExtractDomain(pageTitle);
        var isPrivate = PrivateMarkers.Any(m =>
            windowTitle.Contains(m, StringComparison.OrdinalIgnoreCase));

        return new BrowserInfo
        {
            PageTitle = pageTitle,
            Domain = domain,
            IsPrivate = isPrivate,
        };
    }

    /// <summary>
    /// 根据进程名识别浏览器类型。
    /// </summary>
    private static BrowserKind DetectBrowserKind(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return BrowserKind.Unknown;
        }

        var name = processName.AsSpan();

        // Chrome: chrome.exe 等
        if (name.Contains("chrome".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return BrowserKind.Chrome;
        }

        // Edge: msedge.exe, edge.exe 等
        if (name.Contains("msedge".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
            name.Contains("edge".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return BrowserKind.Edge;
        }

        // Firefox: firefox.exe 等
        if (name.Contains("firefox".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return BrowserKind.Firefox;
        }

        return BrowserKind.Unknown;
    }

    /// <summary>
    /// 移除浏览器后缀，提取纯页面标题。
    /// </summary>
    private static string StripBrowserSuffix(string windowTitle, BrowserKind kind)
    {
        foreach (var (suffix, browserKind) in BrowserSuffixes)
        {
            if (browserKind != kind)
            {
                continue;
            }

            if (windowTitle.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return windowTitle[..^suffix.Length].Trim();
            }
        }

        return windowTitle.Trim();
    }

    /// <summary>
    /// 从页面标题中尝试提取域名（Phase 1 启发式方法）。
    /// 查找标题中类似 "name.tld" 模式的片段。
    /// </summary>
    public static string? ExtractDomain(string pageTitle)
    {
        if (string.IsNullOrWhiteSpace(pageTitle))
        {
            return null;
        }

        // 按常见分隔符拆分标题
        var parts = pageTitle.Split(
            new[] { ' ', '|', '-', '—', '·', '>', '/', '\\', '@', '#', '（', '）', '(', ')' },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            var dotIndex = part.IndexOf('.');
            if (dotIndex > 0 && dotIndex < part.Length - 1)
            {
                var tld = part[(dotIndex + 1)..];
                if (IsKnownTld(tld))
                {
                    return part.ToLowerInvariant();
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 判断是否为已知顶级域名。
    /// </summary>
    private static bool IsKnownTld(string tld)
    {
        return tld switch
        {
            // 通用顶级域名
            "com" or "org" or "net" or "io" or "co" or "app" or "dev"
            or "gov" or "edu" or "info" or "me" or "tv" or "cc" or "top"
            or "xyz" or "online" or "tech" or "site" or "store" or "blog"
            or "pro" or "name" or "biz" or "mobi" or "asia"
            // 国家和地区顶级域名
            or "cn" or "jp" or "kr" or "de" or "fr" or "au" or "ca" or "ru"
            or "uk" or "it" or "es" or "nl" or "br" or "in" or "ch" or "se"
            or "no" or "fi" or "dk" or "pl" or "be" or "at" or "tw" or "hk"
            or "sg" or "nz" or "mx" or "ar" or "za" or "il" => true,
            _ => false,
        };
    }

    private enum BrowserKind { Unknown, Chrome, Edge, Firefox }
}

/// <summary>
/// 浏览器解析结果，包含页面标题和域名。
/// </summary>
public class BrowserInfo
{
    /// <summary>纯页面标题（不含浏览器后缀）。</summary>
    public string PageTitle { get; set; } = string.Empty;

    /// <summary>从标题中提取的域名（可能为 null）。</summary>
    public string? Domain { get; set; }

    /// <summary>是否隐私/无痕模式。</summary>
    public bool IsPrivate { get; set; }
}
