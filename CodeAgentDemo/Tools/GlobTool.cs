using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CodeAgentDemo.Tools;

/// <summary>
/// 使用 glob 模式匹配查找文件的工具。
/// </summary>
public class GlobTool : ITool
{
    private readonly string _workDir;

    public GlobTool(string workDir)
    {
        _workDir = workDir ?? throw new ArgumentNullException(nameof(workDir));
    }

    public string Name => "glob";

    public string Description =>
        "快速文件模式匹配工具，适用于任意大小的代码库。\n\n" +
        "支持 glob 模式如 \"**/*.js\" 或 \"src/**/*.ts\"。\n" +
        "返回按修改时间排序的匹配文件路径。\n" +
        "当需要按名称模式查找文件时使用此工具。\n" +
        "如果需要进行多轮 glob 和 grep 的开放式搜索，请改用 Task 工具。\n" +
        "可以在单次响应中调用多个工具，批量执行可能有用的搜索会更高效。";

    public JsonElement InputSchema => JsonDocument.Parse("""
    {
        "type": "object",
        "properties": {
            "pattern": {
                "type": "string",
                "description": "匹配文件的 glob 模式"
            },
            "path": {
                "type": "string",
                "description": "搜索目录。如未指定则使用当前工作目录。重要：省略此字段使用默认目录，不要输入 \"undefined\" 或 \"null\"。"
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

        const int limit = 100;
        var files = new List<(string Path, DateTime Mtime)>();

        await Task.Run(() =>
        {
            var regex = GlobToRegex(pattern);
            files = Directory.EnumerateFiles(searchPath, "*", SearchOption.AllDirectories)
                .Where(f => regex.IsMatch(Path.GetRelativePath(searchPath, f).Replace('\\', '/')))
                .Select(f => (Path: f, Mtime: File.GetLastWriteTime(f)))
                .OrderByDescending(f => f.Mtime)
                .Take(limit + 1)
                .ToList();
        }, cancellationToken);

        var truncated = files.Count > limit;
        if (truncated) files = files.Take(limit).ToList();

        if (files.Count == 0)
            return new ToolResult(true, "No files found");

        var sb = new StringBuilder();
        foreach (var file in files)
        {
            sb.AppendLine(file.Path);
        }

        if (truncated)
        {
            sb.AppendLine();
            sb.AppendLine($"(Results are truncated: showing first {limit} results. Consider using a more specific path or pattern.)");
        }

        return new ToolResult(true, sb.ToString());
    }

    private static Regex GlobToRegex(string pattern)
    {
        var regex = new StringBuilder("^");
        foreach (var c in pattern)
        {
            switch (c)
            {
                case '*':
                    regex.Append(".*");
                    break;
                case '?':
                    regex.Append(".");
                    break;
                case '.':
                case '+':
                case '(':
                case ')':
                case '[':
                case ']':
                case '{':
                case '}':
                case '^':
                case '$':
                case '|':
                case '\\':
                    regex.Append('\\').Append(c);
                    break;
                default:
                    regex.Append(c);
                    break;
            }
        }
        regex.Append('$');
        return new Regex(regex.ToString(), RegexOptions.IgnoreCase);
    }
}