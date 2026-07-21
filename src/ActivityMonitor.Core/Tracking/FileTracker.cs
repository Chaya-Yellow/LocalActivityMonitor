using System.Text.RegularExpressions;

namespace ActivityMonitor.Core.Tracking;

/// <summary>
/// 文件与项目追踪器。
/// 从窗口标题 + 进程名 + 工作目录推断文件名、路径和所属项目。
/// 支持 VS Code、终端、PowerShell、Photoshop、远程桌面等多种应用。
/// </summary>
public partial class FileTracker
{
    // ── 已知编辑器进程名列表 ──────────────────────────────────────
    private static readonly string[] KnownEditorProcesses =
    {
        "code", "code.exe",
        "notepad++", "notepad++.exe",
        "sublime_text", "sublime_text.exe",
        "atom", "atom.exe",
        "vim", "vim.exe",
        "nvim", "nvim.exe",
        "gvim", "gvim.exe",
    };

    // ── 远程桌面进程 ────────────────────────────────────────────
    private static readonly string[] RemoteDesktopProcesses =
    {
        "mstsc", "mstsc.exe",
        "todesk", "todesk.exe",
        "sunlogin", "sunlogin.exe",
        "anydesk", "anydesk.exe",
        "teamviewer", "teamviewer.exe",
    };

    // ── 终端类进程 ──────────────────────────────────────────────
    private static readonly string[] TerminalProcesses =
    {
        "cmd", "cmd.exe",
        "powershell", "powershell.exe",
        "pwsh", "pwsh.exe",
        "windowsTerminal", "WindowsTerminal.exe",
        "wt", "wt.exe",
    };

    // ── 正则表达式（编译缓存） ──────────────────────────────────
    [GeneratedRegex(@"^(.*?)\s*@", RegexOptions.Compiled)]
    private static partial Regex PhotoshopTitlePattern();

    [GeneratedRegex(@"[\s\-—–_]+", RegexOptions.Compiled)]
    private static partial Regex TitleSplitPattern();

    /// <summary>
    /// 判断进程名是否属于编辑器类。
    /// </summary>
    public static bool IsEditor(string processName)
    {
        return KnownEditorProcesses.Contains(processName, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 判断进程名是否属于终端类。
    /// </summary>
    public static bool IsTerminal(string processName)
    {
        return TerminalProcesses.Contains(processName, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 判断进程名是否属于远程桌面类。
    /// </summary>
    public static bool IsRemoteDesktop(string processName)
    {
        return RemoteDesktopProcesses.Contains(processName, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 解析窗口标题，推断文件名和项目信息。
    /// </summary>
    /// <param name="windowTitle">前台窗口标题。</param>
    /// <param name="processName">进程名（含 .exe 后缀）。</param>
    /// <param name="workingDirectory">进程工作目录（可选）。</param>
    /// <returns>解析结果；若无法解析返回 null。</returns>
    public FileInfo? Parse(string windowTitle, string processName, string? workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(windowTitle))
        {
            return null;
        }

        // 按进程名分发到对应解析器
        if (KnownEditorProcesses.Contains(processName, StringComparer.OrdinalIgnoreCase))
        {
            return ParseEditorTitle(windowTitle, processName);
        }

        if (TerminalProcesses.Contains(processName, StringComparer.OrdinalIgnoreCase))
        {
            return ParseTerminalTitle(windowTitle, processName, workingDirectory);
        }

        if (RemoteDesktopProcesses.Contains(processName, StringComparer.OrdinalIgnoreCase))
        {
            return ParseRemoteDesktopTitle(windowTitle);
        }

        // Photoshop 等设计软件：标题含 @ 符号
        if (windowTitle.Contains('@') && workingDirectory is not null)
        {
            return ParsePhotoShopLikeTitle(windowTitle, workingDirectory);
        }

        // Office 应用
        if (IsOfficeApp(processName))
        {
            return ParseOfficeTitle(windowTitle);
        }

        // 通用兜底：尝试从窗口标题提取可能的文件名
        return ParseGenericTitle(windowTitle, workingDirectory);
    }

    /// <summary>
    /// 解析 VS Code / 编辑器窗口标题。
    /// 格式："{文件名} - {项目名} - Visual Studio Code"
    ///       或 "{文件夹名} - Visual Studio Code"（未打开文件时）
    /// </summary>
    private static FileInfo? ParseEditorTitle(string windowTitle, string processName)
    {
        // 识别后缀模式：process name 去掉 .exe 作为后缀
        var suffixBase = Path.GetFileNameWithoutExtension(processName);
        var fileName = (string?)null;
        var project = (string?)null;

        // 尝试匹配常见的编辑器后缀模式
        string[] knownSuffixes =
        {
            "Visual Studio Code",
            "Sublime Text",
            "Notepad++",
            "Atom",
            "Vim",
        };

        var remaining = windowTitle;

        foreach (var suffix in knownSuffixes)
        {
            var fullSuffix = $" - {suffix}";
            if (remaining.EndsWith(fullSuffix, StringComparison.OrdinalIgnoreCase))
            {
                remaining = remaining[..^fullSuffix.Length].Trim();
                break;
            }
        }

        // 剩余部分："{文件名} - {项目名}" 或 "{文件夹名}"
        var parts = remaining.Split(" - ", StringSplitOptions.TrimEntries);

        if (parts.Length >= 2)
        {
            fileName = parts[0];
            // 项目名可能是 parts[1..^0] 的拼接，但通常就是 parts[1]
            project = parts[1];
        }
        else if (parts.Length == 1)
        {
            project = parts[0];
        }

        return new FileInfo
        {
            FileName = fileName,
            ProjectName = project,
        };
    }

    /// <summary>
    /// 解析终端/CMD/PowerShell 窗口标题。
    /// 格式："cmd.exe - C:\path\to\dir"
    ///       "管理员: C:\Windows\System32\cmd.exe"
    ///       "Windows PowerShell - C:\path"
    ///       "pwsh.exe - D:\projects\code"
    /// </summary>
    private static FileInfo? ParseTerminalTitle(string windowTitle, string processName, string? workingDirectory)
    {
        var path = (string?)null;

        // 模式1："{processName} - {path}" 或 "{friendlyName} - {path}"
        var dashIndex = windowTitle.IndexOf(" - ", StringComparison.Ordinal);
        if (dashIndex > 0)
        {
            var afterDash = windowTitle[(dashIndex + 3)..].Trim();
            if (IsLikelyPath(afterDash) || System.IO.Directory.Exists(afterDash))
            {
                path = afterDash;
            }
        }

        // 模式2："管理员: C:\..." 或标题本身是路径
        if (path is null && IsLikelyPath(windowTitle))
        {
            path = windowTitle;
        }

        // 模式3：如果标题解析不出路径但提供了工作目录，使用工作目录
        path ??= workingDirectory;

        return new FileInfo
        {
            FilePath = path,
            ProjectName = path is not null ? GetFolderName(path) : null,
        };
    }

    /// <summary>
    /// 解析远程桌面窗口标题。
    /// 格式："远程桌面连接 - 192.168.1.100"
    ///       "Remote Desktop - server-name"
    /// </summary>
    private static FileInfo? ParseRemoteDesktopTitle(string windowTitle)
    {
        // 中文远程桌面
        var cnPrefix = "远程桌面连接";
        var enPrefix = "Remote Desktop";

        var address = (string?)null;

        if (windowTitle.StartsWith(cnPrefix, StringComparison.Ordinal) && windowTitle.Length > cnPrefix.Length)
        {
            var afterPrefix = windowTitle[cnPrefix.Length..].TrimStart(' ', '-', '—');
            address = afterPrefix;
        }
        else if (windowTitle.StartsWith(enPrefix, StringComparison.Ordinal) && windowTitle.Length > enPrefix.Length)
        {
            var afterPrefix = windowTitle[enPrefix.Length..].TrimStart(' ', '-', '—');
            address = afterPrefix;
        }
        else
        {
            address = windowTitle;
        }

        return new FileInfo
        {
            FileName = address,
            FilePath = address,
            IsRemote = true,
        };
    }

    /// <summary>
    /// 解析 Photoshop 等设计软件窗口标题。
    /// 格式："文件名 @ 100% (RGB/8)"
    /// 标题只含文件名，完整路径通过工作目录推断。
    /// </summary>
    private static FileInfo? ParsePhotoShopLikeTitle(string windowTitle, string workingDirectory)
    {
        // 截取 @ 符号之前的内容作为文件名
        var match = PhotoshopTitlePattern().Match(windowTitle);
        var fileName = match.Success
            ? match.Groups[1].Value.Trim()
            : windowTitle;

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        // 构造完整路径：工作目录 + 文件名
        var fullPath = !string.IsNullOrWhiteSpace(workingDirectory)
            ? Path.Combine(workingDirectory, fileName)
            : null;

        return new FileInfo
        {
            FileName = fileName,
            FilePath = fullPath,
            ProjectName = !string.IsNullOrWhiteSpace(workingDirectory)
                ? GetFolderName(workingDirectory)
                : null,
        };
    }

    /// <summary>
    /// 解析 Office 应用标题。
    /// Office 标题通常为"文件名 - 应用名"。
    /// </summary>
    private static FileInfo? ParseOfficeTitle(string windowTitle)
    {
        var parts = windowTitle.Split(" - ", StringSplitOptions.TrimEntries);
        var fileName = parts.Length >= 2 ? parts[0] : windowTitle;

        return new FileInfo
        {
            FileName = fileName,
        };
    }

    /// <summary>
    /// 通用兜底解析：从标题中尝试提取文件名。
    /// </summary>
    private static FileInfo? ParseGenericTitle(string windowTitle, string? workingDirectory)
    {
        // 检查标题中是否包含文件扩展名
        var fileName = windowTitle.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(w => w.Contains('.') && w.Length < 260 && !w.StartsWith('.'));

        return new FileInfo
        {
            FileName = fileName,
            FilePath = fileName is not null && !string.IsNullOrWhiteSpace(workingDirectory)
                ? Path.Combine(workingDirectory, fileName)
                : null,
            ProjectName = !string.IsNullOrWhiteSpace(workingDirectory)
                ? GetFolderName(workingDirectory)
                : null,
        };
    }

    /// <summary>
    /// 判断字符串是否可能为路径（包含驱动器号或路径分隔符）。
    /// </summary>
    private static bool IsLikelyPath(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains('\\') || text.Contains('/') ||
               (text.Length >= 2 && text[1] == ':') ||
               text.StartsWith("\\\\", StringComparison.Ordinal);
    }

    /// <summary>
    /// 从路径中获取最末段文件夹名。
    /// </summary>
    private static string? GetFolderName(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        // 去掉末尾的路径分隔符后取目录名
        var trimmed = path.TrimEnd('\\', '/');
        var index = trimmed.LastIndexOfAny(new[] { '\\', '/' });
        return index >= 0 ? trimmed[(index + 1)..] : trimmed;
    }

    /// <summary>
    /// 判断是否为 Office 应用。
    /// </summary>
    private static bool IsOfficeApp(string processName)
    {
        return processName switch
        {
            "winword.exe" or "WINWORD.EXE" or "excel.exe" or "EXCEL.EXE"
            or "powerpnt.exe" or "POWERPNT.EXE" or "outlook.exe" or "OUTLOOK.EXE"
            or "onenote.exe" or "ONENOTE.EXE" or "msaccess.exe" or "MSACCESS.EXE" => true,
            _ => false,
        };
    }
}

/// <summary>
/// 文件追踪解析结果。
/// </summary>
public class FileInfo
{
    /// <summary>文件名（不含路径）。</summary>
    public string? FileName { get; set; }

    /// <summary>完整的文件路径（可能为空，PS 等场景需要工作目录推断）。</summary>
    public string? FilePath { get; set; }

    /// <summary>所属项目/文件夹名称。</summary>
    public string? ProjectName { get; set; }

    /// <summary>是否为远程桌面连接。</summary>
    public bool IsRemote { get; set; }
}
