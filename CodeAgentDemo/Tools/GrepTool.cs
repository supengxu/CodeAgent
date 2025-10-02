using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CodeAgentDemo.Tools;

/// <summary>
/// 使用正则表达式搜索文件内容的工具。
/// </summary>
public class GrepTool : ITool
{
    private readonly string _workDir;

    public GrepTool(string workDir)
    {
        _workDir = workDir ?? throw new ArgumentNullException(nameof(workDir));
    }

    public string Name => "grep";

    public string Description =>
        "快速内容搜索工具，适用于任意大小的代码库。\n\n" +
        "使用正则表达式搜索文件内容。\n" +
        "支持完整的正则语法（如 \"log.*Error\"、\"function\\s+\\w+\" 等）。\n" +
        "通过 include 参数按文件模式过滤（如 \"*.js\"、\"*.{ts,tsx}\"）。\n" +
        "返回至少有一个匹配项的文件路径和行号，按修改时间排序。\n" +
        "当需要查找包含特定模式的文件时使用此工具。\n" +
        "如果需要统计文件内的匹配数量，请使用 Bash 工具直接调用 `rg`（ripgrep）。不要使用 `grep`。\n" +
        "如果需要进行多轮 glob 和 grep 的开放式搜索，请改用 Task 工具。";

    public JsonElement InputSchema => JsonDocument.Parse("""
    {
        "type": "object",
        "properties": {
            "pattern": {
                "type": "string",
                "description": "在文件内容中搜索的正则表达式模式"
            },
            "path": {
                "type": "string",
                "description": "搜索目录，默认为当前工作目录"
            },
            "include": {
                "type": "string",
                "description": "包含在搜索中的文件模式（如 \"*.js\"、\"*.{ts,tsx}\"）"
            }
        },
        "required": ["pattern"]
    }
    """).RootElement;

    public bool RequiresConfirmation(JsonElement arguments) => false;

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken = default)
    {
        if (!arguments.TryGetProperty("pattern", out var patternProp))
            return new ToolResult(false, "pattern is required");

        var pattern = patternProp.GetString();
        if (string.IsNullOrEmpty(pattern))
            return new ToolResult(false, "pattern cannot be empty");

        var searchPath = _workDir;
        if (arguments.TryGetProperty("path", out var pathProp))
        {
            var customPath = pathProp.GetString();
            if (!string.IsNullOrEmpty(customPath))
            {
                searchPath = Path.GetFullPath(Path.IsPathRooted(customPath) ? customPath : Path.Combine(_workDir, customPath));
            }
        }

        if (!Directory.Exists(searchPath))
            return new ToolResult(false, $"Directory not found: {searchPath}");

        var include = arguments.TryGetProperty("include", out var includeProp) ? includeProp.GetString() : null;

        Regex regex;
        try
        {
            regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }
        catch (ArgumentException ex)
        {
            return new ToolResult(false, $"Invalid regex pattern: {ex.Message}");
        }

        var includePatterns = ParseIncludePatterns(include);

        const int limit = 100;
        var matches = new List<(string Path, DateTime Mtime, int LineNum, string LineText)>();

        await Task.Run(() =>
        {
            var files = Directory.EnumerateFiles(searchPath, "*", SearchOption.AllDirectories)
                .Where(f => MatchesIncludePattern(f, includePatterns))
                .Where(f => !IsBinaryFile(f));

            foreach (var file in files)
            {
                if (matches.Count >= limit + 100) break;

                try
                {
                    var lines = File.ReadAllLines(file);
                    for (var i = 0; i < lines.Length; i++)
                    {
                        if (regex.IsMatch(lines[i]))
                        {
                            var mtime = File.GetLastWriteTime(file);
                            matches.Add((file, mtime, i + 1, lines[i]));
                        }
                    }
                }
                catch
                {
                }
            }
        }, cancellationToken);

        matches = matches.OrderByDescending(m => m.Mtime).Take(limit).ToList();

        if (matches.Count == 0)
            return new ToolResult(true, "No files found");

        var sb = new StringBuilder();
        sb.AppendLine($"Found {matches.Count} matches");

        var currentFile = "";
        foreach (var match in matches)
        {
            if (currentFile != match.Path)
            {
                if (currentFile != "") sb.AppendLine();
                currentFile = match.Path;
                sb.AppendLine($"{match.Path}:");
            }
            var truncatedLine = match.LineText.Length > 200 ? match.LineText[..200] + "..." : match.LineText;
            sb.AppendLine($"  Line {match.LineNum}: {truncatedLine}");
        }

        return new ToolResult(true, sb.ToString());
    }

    private static List<string> ParseIncludePatterns(string? include)
    {
        if (string.IsNullOrEmpty(include)) return ["*"];

        if (include.StartsWith("*.{") && include.EndsWith("}"))
        {
            var extensions = include[2..^1].Split(',');
            return extensions.Select(ext => $"*{ext.Trim()}").ToList();
        }

        return [include];
    }

    private static bool MatchesIncludePattern(string filePath, List<string> patterns)
    {
        var fileName = Path.GetFileName(filePath);
        return patterns.Any(p =>
        {
            if (p == "*") return true;
            if (p.StartsWith("*."))
            {
                var ext = p[1..];
                return fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase);
            }
            return string.Equals(fileName, p, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static bool IsBinaryFile(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var binaryExts = new HashSet<string>
        {
            ".zip", ".tar", ".gz", ".exe", ".dll", ".so", ".class", ".jar", ".war", ".7z",
            ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".odt", ".ods", ".odp",
            ".bin", ".dat", ".obj", ".o", ".a", ".lib", ".wasm", ".pyc", ".pyo",
            ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".tiff", ".ico",
            ".pdf", ".mp3", ".mp4", ".wav", ".avi", ".mov", ".mkv"
        };
        return binaryExts.Contains(ext);
    }
}