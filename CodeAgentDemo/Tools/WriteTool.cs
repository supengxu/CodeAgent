using System.Text;
using System.Text.Json;

namespace CodeAgentDemo.Tools;

/// <summary>
/// 写入文件的工具，支持覆盖写入。
/// </summary>
public class WriteTool : ITool
{
    private readonly string _workDir;

    public WriteTool(string workDir)
    {
        _workDir = workDir ?? throw new ArgumentNullException(nameof(workDir));
    }

    public string Name => "write";

    public string Description =>
        "将文件写入本地文件系统。\n\n" +
        "用法：\n" +
        "- 此工具会覆盖已存在的文件。\n" +
        "- 如果是现有文件，必须先使用 Read 工具读取文件内容。\n" +
        "- 始终优先编辑代码库中的现有文件，除非明确需要才创建新文件。\n" +
        "- 不要主动创建文档文件（*.md）或 README 文件。\n" +
        "- 仅在用户明确要求时使用表情符号。";

    public JsonElement InputSchema => JsonDocument.Parse("""
    {
        "type": "object",
        "properties": {
            "filePath": {
                "type": "string",
                "description": "要写入的文件的绝对路径（必须是绝对路径，不能是相对路径）"
            },
            "content": {
                "type": "string",
                "description": "要写入文件的内容"
            }
        },
        "required": ["filePath", "content"]
    }
    """).RootElement;

    public bool RequiresConfirmation(JsonElement arguments) => true;

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken = default)
    {
        if (!arguments.TryGetProperty("filePath", out var pathProp))
            return new ToolResult(false, "filePath is required");

        if (!arguments.TryGetProperty("content", out var contentProp))
            return new ToolResult(false, "content is required");

        var filePath = pathProp.GetString();
        if (string.IsNullOrEmpty(filePath))
            return new ToolResult(false, "filePath cannot be empty");

        var content = contentProp.GetString() ?? string.Empty;

        var fullPath = Path.GetFullPath(Path.IsPathRooted(filePath) ? filePath : Path.Combine(_workDir, filePath));

        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(fullPath, content, cancellationToken);

        var relativePath = Path.GetRelativePath(_workDir, fullPath);
        return new ToolResult(true, $"Wrote file successfully: {relativePath}");
    }
}