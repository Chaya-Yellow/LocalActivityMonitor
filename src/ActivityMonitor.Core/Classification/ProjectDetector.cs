namespace ActivityMonitor.Core.Classification;

/// <summary>
/// 项目检测器。
/// 从文件路径或进程路径中检测项目信息：
/// 1. 向上遍历寻找 .git 目录 → 取仓库名
/// 2. 无 .git 时取最后两级文件夹名
/// 3. 兼容 VS Code / 终端 / 通用三种场景
/// </summary>
public class ProjectDetector
{
    /// <summary>
    /// 从完整文件路径检测项目名。
    /// 向上遍历目录树，找到包含 .git 的目录作为项目根。
    /// </summary>
    /// <param name="filePath">文件或目录的完整路径。</param>
    /// <returns>项目名；无法识别时返回 "unknown"。</returns>
    public string DetectFromPath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return "unknown";
        }

        // 清理路径：如果是文件取目录，如果是目录直接使用
        var dir = filePath;
        if (!System.IO.Directory.Exists(dir))
        {
            // 可能是文件路径，尝试取目录
            var parent = System.IO.Path.GetDirectoryName(dir);
            if (parent is not null && System.IO.Directory.Exists(parent))
            {
                dir = parent;
            }
            else
            {
                // 如果路径完全不可解析，退回兜底
                return FallbackProjectName(filePath);
            }
        }

        // 向上遍历寻找 .git
        var current = new System.IO.DirectoryInfo(dir);
        while (current is not null)
        {
            var gitDir = System.IO.Path.Combine(current.FullName, ".git");
            if (System.IO.Directory.Exists(gitDir) || System.IO.File.Exists(gitDir))
            {
                // 找到 Git 仓库根目录
                return current.Name;
            }

            current = current.Parent;
        }

        // 无 .git：取最后两级文件夹作为项目名
        return FallbackProjectName(dir);
    }

    /// <summary>
    /// 从 VS Code 格式的窗口标题中提取项目名。
    /// 格式："{文件名} - {项目名} - Visual Studio Code"
    /// 或："{文件夹名} - Visual Studio Code"（未打开文件时）
    /// </summary>
    public string? DetectFromVsCodeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        // 移除 VS Code 后缀
        const string suffix = " - Visual Studio Code";
        var remaining = title;
        if (remaining.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            remaining = remaining[..^suffix.Length].Trim();
        }

        // 剩余部分按 " - " 拆分，取最后一部分作为项目名
        var parts = remaining.Split(" - ", StringSplitOptions.TrimEntries);
        return parts.Length >= 1 ? parts[^1] : null;
    }

    /// <summary>
    /// 从终端标题中提取路径用于项目检测。
    /// 格式："cmd.exe - C:\Users\name\project" → 提取 "project"
    /// </summary>
    public string? DetectFromTerminalTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        // 查找 " - " 后的路径部分
        var dashIndex = title.IndexOf(" - ", StringComparison.Ordinal);
        var pathPart = dashIndex > 0
            ? title[(dashIndex + 3)..].Trim()
            : title;

        // 清理路径外壳：去掉 "管理员:" 等前缀
        if (pathPart.Contains(':') && !pathPart.Contains('\\') && !pathPart.Contains('/'))
        {
            // 可能是 "管理员: C:\..." 格式，提取 C:\... 部分
            var colonIndex = pathPart.IndexOf(':');
            if (colonIndex > 0 && colonIndex < pathPart.Length - 1)
            {
                var afterColon = pathPart[(colonIndex + 1)..].Trim();
                if (afterColon.Length >= 2 && afterColon[1] == ':')
                {
                    pathPart = afterColon;
                }
            }
        }

        // 从路径中取最末段作为项目名
        if (!string.IsNullOrWhiteSpace(pathPart))
        {
            var trimmed = pathPart.TrimEnd('\\', '/');
            var index = trimmed.LastIndexOfAny(new[] { '\\', '/' });
            return index >= 0 ? trimmed[(index + 1)..] : trimmed;
        }

        return null;
    }

    /// <summary>
    /// 兜底方案：从路径末段取项目名。
    /// 优先取最后两级文件夹（如 "project/submodule"），不足则取一级。
    /// </summary>
    private static string FallbackProjectName(string path)
    {
        // 统一分隔符
        var normalized = path.Replace('/', '\\').TrimEnd('\\');

        var segments = normalized.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return "unknown";
        }

        // 取最后两级（如果存在）
        if (segments.Length >= 2)
        {
            return $"{segments[^2]}/{segments[^1]}";
        }

        return segments[^1];
    }
}
