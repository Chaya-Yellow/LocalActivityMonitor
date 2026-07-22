using ActivityMonitor.Core.Interfaces;
using ActivityMonitor.Core.Models;

namespace ActivityMonitor.Core.Classification;

/// <summary>
/// 活动分类器。
/// 实现 <see cref="IActivityCategorizer"/>，根据进程名、窗口标题等规则自动判断：
/// - 活动类别（web / file / app / idle / sleep）
/// - 子类别（editor / terminal / remote / browser）
/// - 工作/非工作标记（work / break / personal / unknown）
/// </summary>
/// <remarks>
/// 分类规则（优先级从高到低）：
/// 1. 用户手动重标（user_category）→ 覆盖所有规则
/// 2. 浏览器进程 → web
/// 3. 编辑器/IDE 进程 → file
/// 4. 远程桌面进程 → app + remote
/// 5. 系统锁屏/登录 → break
/// 6. 其他所有进程 → app
///
/// 工作标记规则：
/// 1. 用户手动标记优先
/// 2. 域名 + 标题关键词综合判断
/// 3. 默认 unknown
/// </remarks>
public class ActivityCategorizer : IActivityCategorizer
{
    // ── 浏览器进程名列表 ─────────────────────────────────────
    private static readonly HashSet<string> BrowserProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome.exe", "msedge.exe", "firefox.exe",
        "brave.exe", "opera.exe", "vivaldi.exe",
        "iexplore.exe", "iexplore",
    };

    // ── 编辑器/IDE 进程名列表 ────────────────────────────────
    private static readonly HashSet<string> EditorProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        // 主流 IDE
        "devenv.exe",        // Visual Studio
        "code.exe",          // VS Code
        "rider.exe",         // JetBrains Rider
        "idea.exe",          // IntelliJ IDEA
        "idea64.exe",
        "pycharm.exe",       // PyCharm
        "pycharm64.exe",
        "webstorm.exe",      // WebStorm
        "webstorm64.exe",
        "clion.exe",         // CLion
        "clion64.exe",
        "goland.exe",        // GoLand
        "goland64.exe",
        "datagrip.exe",      // DataGrip
        "datagrip64.exe",
        "rubymine.exe",      // RubyMine
        "rubymine64.exe",
        "androidstudio.exe", // Android Studio
        "studio64.exe",
        "xamarin.exe",
        "monodevelop.exe",
        // 文本编辑器
        "notepad++.exe",
        "sublime_text.exe",
        "atom.exe",
        "vim.exe",
        "nvim.exe",
        "gvim.exe",
        "emacs.exe",
        "notepad.exe",
        // Office（归类为 file 编辑）
        "winword.exe",
        "excel.exe",
        "powerpnt.exe",
        "onenote.exe",
        // 设计工具（归类为 file）
        "photoshop.exe",
        "paintdotnet.exe",
        "gimp.exe",
        "figma.exe",         // Figma 桌面版
        "sketchup.exe",
        "blender.exe",
    };

    // ── 远程桌面/远程工具 ────────────────────────────────────
    private static readonly HashSet<string> RemoteProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "mstsc.exe",         // Windows 远程桌面
        "todesk.exe",        // ToDesk
        "sunlogin.exe",      // 向日葵
        "anydesk.exe",       // AnyDesk
        "teamviewer.exe",    // TeamViewer
        "vncserver.exe",
        "vncviewer.exe",
    };

    // ── 终端进程 ─────────────────────────────────────────────
    private static readonly HashSet<string> TerminalProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "cmd.exe",
        "powershell.exe",
        "pwsh.exe",
        "wsl.exe",
        "windowsterminal.exe",
        "wt.exe",
        "conhost.exe",
        "mintty.exe",
        "cygwin.exe",
        "bash.exe",
        "git-bash.exe",
    };

    // ── 系统/锁屏进程 ────────────────────────────────────────
    private static readonly HashSet<string> SystemProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "logonui.exe",       // 锁屏/登录
        "lockapp.exe",       // Windows 锁屏
        "screeningsaver.exe",
    };

    // ── 工作相关域名关键词（用于 work_tag 推断）────────────
    private static readonly HashSet<string> WorkDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "github.com", "gitlab.com", "bitbucket.org",
        "stackoverflow.com", "stackexchange.com",
        "docs.microsoft.com", "learn.microsoft.com",
        "azure.microsoft.com", "portal.azure.com",
        "aws.amazon.com", "console.aws.amazon.com",
        "google.com", "drive.google.com", "docs.google.com",
        "notion.so", "confluence.com",
        "jira.com", "linear.app", "trello.com",
        "slack.com", "teams.microsoft.com",
        "figma.com", "miro.com",
        "npmjs.com", "pypi.org", "nuget.org",
        "docker.com", "hub.docker.com",
        "chatgpt.com", "claude.ai", "openai.com",
        "developer.android.com", "developer.apple.com",
        "leetcode.com", "codepen.io",
    };

    // ── 个人/非工作域名 ─────────────────────────────────────
    private static readonly HashSet<string> PersonalDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "youtube.com", "bilibili.com",
        "twitter.com", "x.com",
        "facebook.com", "instagram.com",
        "reddit.com", "zhihu.com",
        "weibo.com", "tieba.baidu.com",
        "douyin.com", "douban.com",
        "netflix.com", "spotify.com",
        "amazon.com", "taobao.com", "jd.com",
        "baidu.com",
    };

    // ── 工作相关标题关键词 ──────────────────────────────────
    private static readonly HashSet<string> WorkKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        // 开发
        "commit", "merge", "pull request", "pr", "issue", "bug", "debug",
        "refactor", "deploy", "release", "feature", "fix", "hotfix",
        "code review", "code", "API", "sprint", "task", "story",
        "documentation", "spec", "test", "unit test", "integration",
        "build", "compile", "pipeline", "CI", "CD",
        // 办公
        "会议", "周会", "日报", "汇报", "项目", "需求",
        "方案", "设计", "评审", "计划", "总结",
        "meeting", "standup", "workshop", "brainstorm",
        "report", "analysis", "proposal",
        // 设计
        "设计稿", "原型", "mockup", "wireframe",
    };

    // ── 个人/非工作标题关键词 ──────────────────────────────
    private static readonly HashSet<string> PersonalKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "游戏", "视频", "电影", "音乐", "综艺", "直播",
        "购物", "淘宝", "京东", "拼多多",
        "新闻", "八卦", "娱乐", "体育", "追剧",
        "小说", "漫画", "动漫",
        "game", "video", "movie", "music", "tv show",
        "shopping", "news", "entertainment", "sports",
        "lunch", "dinner", "break", "休息", "午休",
    };

    private static readonly HashSet<string> SleepTitleKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "锁屏", "锁定", "登录", "lock", "login", "sign in",
    };

    private static readonly string[] IncognitoMarkers =
    {
        "无痕浏览", "无痕模式", "Incognito", "InPrivate", "隐私浏览", "隐私模式",
    };

    /// <summary>
    /// 对给定的活动事件进行分类。
    /// </summary>
    /// <param name="activity">待分类的活动事件。</param>
    /// <returns>(category, workTag) 元组。</returns>
    public (string category, string workTag) Classify(ActivityEvent activity)
    {
        if (activity is null)
        {
            return (Models.Category.App, Models.WorkTag.Unknown);
        }

        // Step 1: 用户手动重标优先
        if (!string.IsNullOrWhiteSpace(activity.UserCategory))
        {
            var cat = activity.UserCategory.ToLowerInvariant();
            if (IsValidCategory(cat))
            {
                // 即使是用户重标，仍需要推断 workTag
                var workTag = InferWorkTag(activity);
                return (cat, workTag);
            }
        }

        // Step 2: 按进程名分类
        var processName = activity.ProcessName ?? string.Empty;

        // 系统进程 → break（锁屏/登录/Win+L）
        if (SystemProcesses.Contains(processName) ||
            SleepTitleKeywords.Any(k => (activity.WindowTitle ?? string.Empty).Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            return (Models.Category.Break, Models.WorkTag.Break);
        }

        // 浏览器 → web
        if (BrowserProcesses.Contains(processName))
        {
            var isPrivate = IncognitoMarkers.Any(m =>
                (activity.WindowTitle ?? string.Empty).Contains(m, StringComparison.OrdinalIgnoreCase));
            return (Models.Category.Web, InferWorkTag(activity));
        }

        // 编辑器/IDE → file
        if (EditorProcesses.Contains(processName))
        {
            var sub = TerminalProcesses.Contains(processName) ? null : "editor";
            return (Models.Category.File, InferWorkTag(activity));
        }

        // 终端 → file + terminal 子类别
        if (TerminalProcesses.Contains(processName))
        {
            return (Models.Category.File, InferWorkTag(activity));
        }

        // 远程桌面 → app + remote 子类别
        if (RemoteProcesses.Contains(processName))
        {
            return (Models.Category.App, InferWorkTag(activity));
        }

        // Step 3: 兜底 → app
        return (Models.Category.App, InferWorkTag(activity));
    }

    /// <summary>
    /// 推断工作/非工作标记。
    /// 基于域名 + 标题关键词综合判断。
    /// </summary>
    private static string InferWorkTag(ActivityEvent activity)
    {
        // 如果用户已经手动设置了 work_tag，优先使用
        if (!string.IsNullOrWhiteSpace(activity.WorkTag) &&
            activity.WorkTag != Models.WorkTag.Unknown)
        {
            return activity.WorkTag;
        }

        // 域名检测（适用于 web 类）
        if (!string.IsNullOrWhiteSpace(activity.Domain))
        {
            if (WorkDomains.Contains(activity.Domain))
            {
                return Models.WorkTag.Work;
            }

            if (PersonalDomains.Contains(activity.Domain))
            {
                return Models.WorkTag.Personal;
            }
        }

        // 窗口标题关键词检测
        var title = activity.WindowTitle ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(title))
        {
            if (WorkKeywords.Any(k => title.Contains(k, StringComparison.OrdinalIgnoreCase)))
            {
                return Models.WorkTag.Work;
            }

            if (PersonalKeywords.Any(k => title.Contains(k, StringComparison.OrdinalIgnoreCase)))
            {
                return Models.WorkTag.Personal;
            }
        }

        return Models.WorkTag.Unknown;
    }

    /// <summary>
    /// 获取适合当前活动事件的子类别。
    /// </summary>
    public static string? GetSubCategory(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return null;
        }

        if (BrowserProcesses.Contains(processName))
        {
            return "browser";
        }

        if (EditorProcesses.Contains(processName) && !TerminalProcesses.Contains(processName))
        {
            return "editor";
        }

        if (TerminalProcesses.Contains(processName))
        {
            return "terminal";
        }

        if (RemoteProcesses.Contains(processName))
        {
            return "remote";
        }

        return null;
    }

    /// <summary>
    /// 验证类别字符串是否有效。
    /// </summary>
    private static bool IsValidCategory(string category)
    {
        return category switch
        {
            Models.Category.Web or Models.Category.File
            or Models.Category.App or Models.Category.Idle
            or Models.Category.Sleep or Models.Category.Break => true,
            _ => false,
        };
    }
}
