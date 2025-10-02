using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CodeAgentDemo.Tools;

/// <summary>
/// 执行精确字符串替换的工具。
/// </summary>
public class EditTool : ITool
{
    private readonly string _workDir;

    public EditTool(string workDir)
    {
        _workDir = workDir ?? throw new ArgumentNullException(nameof(workDir));
    }

    public string Name => "edit";

    public string Description =>
        "在文件中执行精确字符串替换。\n\n" +
        "用法：\n" +
        "- 编辑前必须使用 Read 工具读取文件。\n" +
        "- 编辑 Read 工具输出的内容时，请保留行号前缀后的精确缩进（制表符/空格）。行号前缀格式为：行号 + 冒号 + 空格（如 `1: `）。该空格后的内容才是实际要匹配的文件内容。\n" +
        "- 始终优先编辑代码库中的现有文件，除非明确需要才创建新文件。\n" +
        "- 仅在用户明确要求时使用表情符号。\n" +
        "- 如果未找到 oldString，编辑将失败并提示 \"oldString not found in content\"。\n" +
        "- 如果找到多个匹配项，编辑将失败并提示 \"Found multiple matches for oldString\"。请提供更多上下文使匹配唯一，或使用 replaceAll 替换所有实例。\n" +
        "- 使用 replaceAll 可在文件中批量替换和重命名字符串。";

    public JsonElement InputSchema => JsonDocument.Parse("""
    {
        "type": "object",
        "properties": {
            "filePath": {
                "type": "string",
                "description": "要修改的文件的绝对路径"
            },
            "oldString": {
                "type": "string",
                "description": "要替换的文本"
            },
            "newString": {
                "type": "string",
                "description": "替换后的文本（必须与 oldString 不同）"
            },
            "replaceAll": {
                "type": "boolean",
                "description": "替换所有匹配项（默认 false）"
            }
        },
        "required": ["filePath", "oldString", "newString"]
    }
    """).RootElement;

    public bool RequiresConfirmation(JsonElement arguments) => true;

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken = default)
    {
        if (!arguments.TryGetProperty("filePath", out var pathProp))
            return new ToolResult(false, "filePath is required");

        if (!arguments.TryGetProperty("oldString", out var oldProp))
            return new ToolResult(false, "oldString is required");

        if (!arguments.TryGetProperty("newString", out var newProp))
            return new ToolResult(false, "newString is required");

        var filePath = pathProp.GetString();
        if (string.IsNullOrEmpty(filePath))
            return new ToolResult(false, "filePath cannot be empty");

        var oldString = oldProp.GetString() ?? string.Empty;
        var newString = newProp.GetString() ?? string.Empty;

        if (oldString == newString)
            return new ToolResult(false, "No changes to apply: oldString and newString are identical.");

        var replaceAll = arguments.TryGetProperty("replaceAll", out var replaceAllProp) && replaceAllProp.GetBoolean();

        var fullPath = Path.GetFullPath(Path.IsPathRooted(filePath) ? filePath : Path.Combine(_workDir, filePath));

        if (!File.Exists(fullPath))
            return new ToolResult(false, $"File not found: {fullPath}");

        var content = await File.ReadAllTextAsync(fullPath, cancellationToken);

        var newContent = Replace(content, oldString, newString, replaceAll);

        await File.WriteAllTextAsync(fullPath, newContent, cancellationToken);

        var relativePath = Path.GetRelativePath(_workDir, fullPath);
        return new ToolResult(true, $"Edit applied successfully to: {relativePath}");
    }

    private static string Replace(string content, string oldString, string newString, bool replaceAll)
    {
        var matchFound = false;

        foreach (var match in FindMatches(content, oldString))
        {
            matchFound = true;
            var index = content.IndexOf(match, StringComparison.Ordinal);
            if (index == -1) continue;

            if (replaceAll)
            {
                return content.Replace(match, newString, StringComparison.Ordinal);
            }

            var lastIndex = content.LastIndexOf(match, StringComparison.Ordinal);
            if (index != lastIndex)
            {
                throw new InvalidOperationException("Found multiple matches for oldString. Provide more surrounding lines in oldString to identify the correct match.");
            }

            return content.Substring(0, index) + newString + content.Substring(index + match.Length);
        }

        if (!matchFound)
        {
            throw new InvalidOperationException("Could not find oldString in the file. It must match exactly, including whitespace, indentation, and line endings.");
        }

        throw new InvalidOperationException("Found multiple matches for oldString. Provide more surrounding context to make the match unique.");
    }

    private static IEnumerable<string> FindMatches(string content, string search)
    {
        yield return search;

        foreach (var match in LineTrimmedMatches(content, search))
            yield return match;

        foreach (var match in BlockAnchorMatches(content, search))
            yield return match;

        foreach (var match in WhitespaceNormalizedMatches(content, search))
            yield return match;

        foreach (var match in IndentationFlexibleMatches(content, search))
            yield return match;
    }

    private static IEnumerable<string> LineTrimmedMatches(string content, string search)
    {
        var originalLines = content.Split('\n');
        var searchLines = search.Split('\n');

        if (searchLines.Length > 0 && string.IsNullOrEmpty(searchLines[^1]))
            searchLines = searchLines[..^1];

        for (var i = 0; i <= originalLines.Length - searchLines.Length; i++)
        {
            var matches = true;
            for (var j = 0; j < searchLines.Length; j++)
            {
                if (originalLines[i + j].Trim() != searchLines[j].Trim())
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                yield return string.Join("\n", originalLines.Skip(i).Take(searchLines.Length));
            }
        }
    }

    private static IEnumerable<string> BlockAnchorMatches(string content, string search)
    {
        var originalLines = content.Split('\n');
        var searchLines = search.Split('\n');

        if (searchLines.Length < 3) yield break;
        if (string.IsNullOrEmpty(searchLines[^1])) searchLines = searchLines[..^1];

        var firstLineSearch = searchLines[0].Trim();
        var lastLineSearch = searchLines[^1].Trim();

        for (var i = 0; i < originalLines.Length; i++)
        {
            if (originalLines[i].Trim() != firstLineSearch) continue;

            for (var j = i + 2; j < originalLines.Length; j++)
            {
                if (originalLines[j].Trim() == lastLineSearch)
                {
                    yield return string.Join("\n", originalLines.Skip(i).Take(j - i + 1));
                    break;
                }
            }
        }
    }

    private static IEnumerable<string> WhitespaceNormalizedMatches(string content, string search)
    {
        var normalizedSearch = Regex.Replace(search, @"\s+", " ").Trim();
        var lines = content.Split('\n');

        foreach (var line in lines)
        {
            if (Regex.Replace(line, @"\s+", " ").Trim() == normalizedSearch)
            {
                yield return line;
            }
        }

        var searchLines = search.Split('\n');
        if (searchLines.Length > 1)
        {
            for (var i = 0; i <= lines.Length - searchLines.Length; i++)
            {
                var block = string.Join("\n", lines.Skip(i).Take(searchLines.Length));
                if (Regex.Replace(block, @"\s+", " ").Trim() == normalizedSearch)
                {
                    yield return block;
                }
            }
        }
    }

    private static IEnumerable<string> IndentationFlexibleMatches(string content, string search)
    {
        var contentLines = content.Split('\n');
        var searchLines = search.Split('\n');

        for (var i = 0; i <= contentLines.Length - searchLines.Length; i++)
        {
            var block = contentLines.Skip(i).Take(searchLines.Length).ToArray();
            if (MatchesWithoutIndentation(block, searchLines))
            {
                yield return string.Join("\n", block);
            }
        }
    }

    private static bool MatchesWithoutIndentation(string[] contentLines, string[] searchLines)
    {
        if (contentLines.Length != searchLines.Length) return false;

        for (var i = 0; i < contentLines.Length; i++)
        {
            if (contentLines[i].TrimStart() != searchLines[i].TrimStart())
                return false;
        }

        return true;
    }
}