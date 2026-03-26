using System.Text.Json;
using System.Text.Json.Serialization;
using CodeAgentDemo.Models;

namespace CodeAgentDemo.Core;

/// <summary>
/// 提供流式 JSONL 处理以高效处理大型会话文件。
/// 将每行作为单独的 JSON 对象处理，无需将整个文件加载到内存中。
/// </summary>
public sealed class JsonlStreamProcessor
{
    private readonly JsonSerializerOptions _jsonOptions;

    public JsonlStreamProcessor(JsonSerializerOptions? jsonOptions = null)
    {
        _jsonOptions = jsonOptions ?? new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        // 添加 ContentBlock 多态转换器
        _jsonOptions.Converters.Add(new ContentBlockJsonConverterFactory());
    }

    /// <summary>
    /// 异步读取并处理指定文件中的每个 JSON 行。
    /// 适用于无法将所有内容加载到内存的大型文件。
    /// </summary>
    /// <param name="filePath">JSONL 文件的路径</param>
    /// <param name="processor">处理每条加载消息的函数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示操作完成的 Task</returns>
    public async Task ProcessJsonlFileAsync(
        string filePath,
        Func<ChatMessage, Task<bool>> processor,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"JSONL file not found: {filePath}");
        }

        using var fileStream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            bufferSize: 8192, // 较大的缓冲区以获得更好的顺序吞吐量
            useAsync: true);

        using var reader = new StreamReader(fileStream);
        string? line;
        int lineNumber = 0;

        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            lineNumber++;
            line = line.Trim();

            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            try
            {
                var message = JsonSerializer.Deserialize<ChatMessage>(line, _jsonOptions);
                if (message != null)
                {
                    // 如果处理函数返回 false，则停止处理
                    if (!await processor(message))
                    {
                        break;
                    }
                }
            }
            catch (JsonException ex)
            {
                // 记录错误但继续处理其他行
                OnCriticalLineError(filePath, lineNumber, line, ex);
                continue;
            }
        }
    }

    /// <summary>
    /// 异步从流中解析 JSONL 行，而不是从文件。
    /// </summary>
    /// <param name="stream">包含 JSONL 数据的流</param>
    /// <param name="processor">处理每条加载消息的函数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示操作完成的 Task</returns>
    public async Task ProcessJsonlStreamAsync(
        Stream stream,
        Func<ChatMessage, Task<bool>> processor,
        CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream);
        string? line;
        int lineNumber = 0;

        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            lineNumber++;
            line = line.Trim();

            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            try
            {
                var message = JsonSerializer.Deserialize<ChatMessage>(line, _jsonOptions);
                if (message != null)
                {
                    // 如果处理函数返回 false，则停止处理
                    if (!await processor(message))
                    {
                        break;
                    }
                }
            }
            catch (JsonException ex)
            {
                // 记录错误但继续处理其他行
                OnCriticalLineError("[stream]", lineNumber, line, ex);
                continue;
            }
        }
    }

    /// <summary>
    /// 异步从 JSONL 文件读取行作为 IAsyncEnumerable。
    /// 支持惰性处理并减少大型文件的内存使用。
    /// </summary>
    /// <param name="filePath">JSONL 文件的路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>ChatMessage 的 IAsyncEnumerable</returns>
    public async IAsyncEnumerable<ChatMessage> ReadJsonlFileAsync(
        string filePath,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            yield break; // 不抛出异常，只返回空枚举
        }

        using var fileStream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            bufferSize: 8192,
            useAsync: true);

        using var reader = new StreamReader(fileStream);
        string? line;
        int lineNumber = 0;

        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            lineNumber++;
            line = line.Trim();

            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            ChatMessage? message = null;
            JsonException? exception = null;
            try
            {
                message = JsonSerializer.Deserialize<ChatMessage>(line, _jsonOptions);
            }
            catch (JsonException ex)
            {
                exception = ex;
            }

            if (exception != null)
            {
                OnCriticalLineError(filePath, lineNumber, line, exception);
                continue; // 跳过错误的行并继续处理
            }

            if (message != null)
            {
                yield return message;
            }
        }
    }

    /// <summary>
    /// 异步向 JSONL 文件追加新条目。
    /// </summary>
    /// <param name="filePath">JSONL 文件的路径</param>
    /// <param name="chatMessage">要追加的消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task AppendAsync(string filePath, ChatMessage chatMessage, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var jsonLine = JsonSerializer.Serialize(chatMessage, _jsonOptions);

        // File.AppendText 默认使用 UTF8 编码并正确处理换行符
        using var fileStream = new FileStream(
            filePath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        using var writer = new StreamWriter(fileStream);
        await writer.WriteLineAsync(jsonLine);
        await writer.FlushAsync(); // 确保数据被实际写入
    }

    private void OnCriticalLineError(string fileOrStream, int lineNumber, string content, JsonException ex)
    {
        Console.Error.WriteLine($"Warning: Invalid JSON in {fileOrStream} at line {lineNumber}: {ex.Message}");
        Console.Error.WriteLine($"Content: {content.Substring(0, Math.Min(200, content.Length))}...");
    }
}
