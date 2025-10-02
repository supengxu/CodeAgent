using System.Text;
using System.Text.Json;

namespace CodeAgentDemo.Tools;

/// <summary>
/// 读取文件或目录的工具，支持文本文件读取、目录列表、图片/PDF 附件。
/// </summary>
public class ReadTool : ITool
{
    private readonly string _workDir;
    
    private const int DefaultReadLimit = 2000;
    private const int MaxLineLength = 2000;
    private const int MaxBytes = 50 * 1024;
    private const string MaxBytesLabel = "50 KB";
    
    private static readonly HashSet<string> BinaryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".tar", ".gz", ".exe", ".dll", ".so", ".class", ".jar", ".war", ".7z",
        ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".odt", ".ods", ".odp",
        ".bin", ".dat", ".obj", ".o", ".a", ".lib", ".wasm", ".pyc", ".pyo"
    };
    
    private static readonly HashSet<string> ImageMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/gif", "image/webp", "image/bmp", "image/tiff"
    };

    public ReadTool(string workDir)
    {
        _workDir = workDir ?? throw new ArgumentNullException(nameof(workDir));
    }

    public string Name => "read";
    
    public string Description => 
        "读取本地文件系统的文件或目录。如果路径不存在，将返回错误。\n\n" +
        "用法：\n" +
        "- filePath 参数应为绝对路径。\n" +
        "- 默认返回文件开头最多 2000 行。\n" +
        "- offset 参数是起始行号（从 1 开始）。\n" +
        "- 要读取后续内容，请使用更大的 offset 再次调用此工具。\n" +
        "- 使用 grep 工具在大文件或长行文件中查找特定内容。\n" +
        "- 如果不确定正确的文件路径，使用 glob 工具按模式查找文件名。\n" +
        "- 返回内容每行前缀行号，格式为 `<行号>: <内容>`。\n" +
        "- 超过 2000 字符的行会被截断。\n" +
        "- 此工具可读取图片文件和 PDF 并作为文件附件返回。";

    public JsonElement InputSchema => JsonDocument.Parse("""
    {
        "type": "object",
        "properties": {
            "filePath": {
                "type": "string",
                "description": "要读取的文件或目录的绝对路径"
            },
            "offset": {
                "type": "integer",
                "description": "起始行号（从 1 开始）",
                "minimum": 1
            },
            "limit": {
                "type": "integer",
                "description": "最大读取行数（默认 2000）"
            }
        },
        "required": ["filePath"]
    }
    """).RootElement;

    public bool RequiresConfirmation(JsonElement arguments) => false;

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken = default)
    {
        if (!arguments.TryGetProperty("filePath", out var pathProp))
            return new ToolResult(false, "filePath is required");

        var filePath = pathProp.GetString();
        if (string.IsNullOrEmpty(filePath))
            return new ToolResult(false, "filePath cannot be empty");

        // 解析路径
var fullPath = Path.GetFullPath(Path.IsPathRooted(filePath) ? filePath : Path.Combine(_workDir, filePath));

        var offset = 1;
        if (arguments.TryGetProperty("offset", out var offsetProp))
        {
            offset = offsetProp.GetInt32();
            if (offset < 1)
                return new ToolResult(false, "offset must be greater than or equal to 1");
        }

        var limit = arguments.TryGetProperty("limit", out var limitProp) ? limitProp.GetInt32() : DefaultReadLimit;

        if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
        {
            var suggestions = FindSimilarFiles(fullPath);
            if (suggestions.Count > 0)
                return new ToolResult(false, $"File not found: {fullPath}\n\nDid you mean one of these?\n{string.Join("\n", suggestions.Take(3))}");
            return new ToolResult(false, $"File not found: {fullPath}");
        }

        if (Directory.Exists(fullPath))
        {
            return await ReadDirectoryAsync(fullPath, offset, limit);
        }

        return await ReadFileAsync(fullPath, offset, limit, cancellationToken);
    }

    private async Task<ToolResult> ReadDirectoryAsync(string dirPath, int offset, int limit)
    {
        try
        {
            var entries = Directory.GetFileSystemEntries(dirPath)
                .Select(p =>
                {
                    var name = Path.GetFileName(p);
                    return Directory.Exists(p) ? name + "/" : name;
                })
                .OrderBy(e => e, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var start = offset - 1;
            var sliced = entries.Skip(start).Take(limit).ToList();
            var truncated = start + sliced.Count < entries.Count;

            var sb = new StringBuilder();
            sb.AppendLine($"<path>{dirPath}</path>");
            sb.AppendLine("<type>directory</type>");
            sb.AppendLine("<entries>");
            foreach (var entry in sliced)
            {
                sb.AppendLine(entry);
            }
            
            if (truncated)
                sb.AppendLine($"\n(Showing {sliced.Count} of {entries.Count} entries. Use 'offset' parameter to read beyond entry {offset + sliced.Count})");
            else
                sb.AppendLine($"\n({entries.Count} entries)");
            
            sb.AppendLine("</entries>");

            return new ToolResult(true, sb.ToString());
        }
        catch (Exception ex)
        {
            return new ToolResult(false, $"Error reading directory: {ex.Message}");
        }
    }

    private async Task<ToolResult> ReadFileAsync(string filePath, int offset, int limit, CancellationToken cancellationToken)
    {
        try
        {
            var mimeType = GetMimeType(filePath);
            if (ImageMimeTypes.Contains(mimeType))
            {
                return await ReadImageAsAttachmentAsync(filePath, mimeType);
            }

            if (mimeType == "application/pdf")
            {
                return await ReadPdfAsAttachmentAsync(filePath);
            }

            if (IsBinaryFile(filePath))
            {
                return new ToolResult(false, $"Cannot read binary file: {filePath}");
            }

            return await ReadTextFileAsync(filePath, offset, limit, cancellationToken);
        }
        catch (Exception ex)
        {
            return new ToolResult(false, $"Error reading file: {ex.Message}");
        }
    }

    private async Task<ToolResult> ReadTextFileAsync(string filePath, int offset, int limit, CancellationToken cancellationToken)
    {
        var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
        
        if (lines.Length < offset && !(lines.Length == 0 && offset == 1))
        {
            return new ToolResult(false, $"Offset {offset} is out of range for this file ({lines.Length} lines)");
        }

        var start = offset - 1;
        var raw = new List<string>();
        var bytes = 0;
        var truncatedByBytes = false;
        var hasMoreLines = false;

        for (var i = start; i < lines.Length; i++)
        {
            if (raw.Count >= limit)
            {
                hasMoreLines = true;
                continue;
            }

            var line = lines[i];
            if (line.Length > MaxLineLength)
                line = line.Substring(0, MaxLineLength) + $"... (line truncated to {MaxLineLength} chars)";

            var size = Encoding.UTF8.GetByteCount(line) + (raw.Count > 0 ? 1 : 0);
            if (bytes + size > MaxBytes)
            {
                truncatedByBytes = true;
                hasMoreLines = true;
                break;
            }

            raw.Add(line);
            bytes += size;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"<path>{filePath}</path>");
        sb.AppendLine("<type>file</type>");
        sb.AppendLine("<content>");
        
        for (var i = 0; i < raw.Count; i++)
        {
            sb.AppendLine($"{i + offset}: {raw[i]}");
        }

        var totalLines = lines.Length;
        var lastReadLine = offset + raw.Count - 1;
        var nextOffset = lastReadLine + 1;

        if (truncatedByBytes)
        {
            sb.AppendLine($"\n(Output capped at {MaxBytesLabel}. Showing lines {offset}-{lastReadLine}. Use offset={nextOffset} to continue.)");
        }
        else if (hasMoreLines)
        {
            sb.AppendLine($"\n(Showing lines {offset}-{lastReadLine} of {totalLines}. Use offset={nextOffset} to continue.)");
        }
        else
        {
            sb.AppendLine($"\n(End of file - total {totalLines} lines)");
        }

        sb.AppendLine("</content>");

        return new ToolResult(true, sb.ToString());
    }

    private async Task<ToolResult> ReadImageAsAttachmentAsync(string filePath, string mimeType)
    {
        var bytes = await File.ReadAllBytesAsync(filePath);
        var base64 = Convert.ToBase64String(bytes);
        
        var sb = new StringBuilder();
        sb.AppendLine($"<path>{filePath}</path>");
        sb.AppendLine("<type>image</type>");
        sb.AppendLine($"<mime>{mimeType}</mime>");
        sb.AppendLine($"<attachment data=\"{base64}\" />");
        sb.AppendLine("Image read successfully. The image data is included as a base64 attachment.");

        return new ToolResult(true, sb.ToString());
    }

    private async Task<ToolResult> ReadPdfAsAttachmentAsync(string filePath)
    {
        var bytes = await File.ReadAllBytesAsync(filePath);
        var base64 = Convert.ToBase64String(bytes);
        
        var sb = new StringBuilder();
        sb.AppendLine($"<path>{filePath}</path>");
        sb.AppendLine("<type>pdf</type>");
        sb.AppendLine($"<attachment data=\"{base64}\" />");
        sb.AppendLine("PDF read successfully. The PDF data is included as a base64 attachment.");

        return new ToolResult(true, sb.ToString());
    }

    private static bool IsBinaryFile(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (BinaryExtensions.Contains(ext))
            return true;

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length == 0)
            return false;

        using var fs = File.OpenRead(filePath);
        var sampleSize = Math.Min(4096, (int)fileInfo.Length);
        var buffer = new byte[sampleSize];
        var bytesRead = fs.Read(buffer, 0, sampleSize);

        if (bytesRead == 0)
            return false;

        for (var i = 0; i < bytesRead; i++)
        {
            if (buffer[i] == 0)
                return true;
        }

        var nonPrintableCount = 0;
        for (var i = 0; i < bytesRead; i++)
        {
            if (buffer[i] < 9 || (buffer[i] > 13 && buffer[i] < 32))
                nonPrintableCount++;
        }

        return nonPrintableCount / (double)bytesRead > 0.3;
    }

    private static string GetMimeType(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".tiff" or ".tif" => "image/tiff",
            ".pdf" => "application/pdf",
            ".svg" => "image/svg+xml",
            ".html" => "text/html",
            ".css" => "text/css",
            ".js" => "application/javascript",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".txt" => "text/plain",
            _ => "application/octet-stream"
        };
    }

    private List<string> FindSimilarFiles(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath) ?? "";
        var baseName = Path.GetFileName(filePath);
        
        if (!Directory.Exists(dir))
            return [];

        try
        {
            return Directory.GetFiles(dir)
                .Select(Path.GetFileName)
                .Where(name => 
                    name!.Contains(baseName, StringComparison.OrdinalIgnoreCase) || 
                    baseName.Contains(name, StringComparison.OrdinalIgnoreCase))
                .Take(3)
                .ToList()!;
        }
        catch
        {
            return [];
        }
    }
}