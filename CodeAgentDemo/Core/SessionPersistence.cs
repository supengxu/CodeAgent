using System.Text.Json;
using System.Text.Json.Serialization;
using CodeAgentDemo.Models;

namespace CodeAgentDemo.Core;

/// <summary>
/// 提供基于 JSONL 的会话持久化，具有健壮的错误处理、ContentBlock 多态序列化和并发访问保护。
/// </summary>
public class SessionPersistence
{
    private readonly JsonSerializerOptions _jsonOptions;
    private static readonly Dictionary<string, SemaphoreSlim> _semaphoreLocks = new();
    private static readonly object _lock = new();

    public SessionPersistence()
    {
        _jsonOptions = CreateJsonOptions();
    }

    private JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        // 为 ContentBlock 层次结构配置多态序列化
        options.Converters.Add(new ContentBlockJsonConverterFactory());

        return options;
    }

    /// <summary>
    /// 从 JSONL 文件异步加载会话。
    /// </summary>
    /// <param name="filePath">包含会话的 JSONL 文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>包含消息的已加载 SessionStore</returns>
    public async Task<SessionStore> LoadSessionAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var sessionStore = new SessionStore();

        if (!File.Exists(filePath))
        {
            return sessionStore;
        }

        // 使用 FileShare.ReadWrite 允许并发访问 
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize: 4096, useAsync: true);
        using var reader = new StreamReader(fileStream);

        string? line;
        var lineNumber = 0;

        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            lineNumber++;

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var message = JsonSerializer.Deserialize<ChatMessage>(line, _jsonOptions);
                if (message != null)
                {
                    sessionStore.AddMessage(message);
                }
            }
            catch (JsonException ex)
            {
                // 记录格式错误的 JSON 但继续处理其他行
                OnDeserializationError(line, lineNumber, ex, filePath);

                // 继续处理文件的其余部分
                continue;
            }
            catch (Exception ex)
            {
                // 记录意外错误但继续处理
                OnDeserializationError(line, lineNumber, new JsonException($"反序列化第 {lineNumber} 行时发生意外错误", ex), filePath);
                continue;
            }
        }

        return sessionStore;
    }

    /// <summary>
    /// 异步将会话保存到 JSONL 文件，使用文件互斥锁保证线程安全。
    /// 每条消息作为单独的 JSON 行写入。
    /// </summary>
    /// <param name="sessionStore">要保存的会话存储</param>
    /// <param name="filePath">JSONL 文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task SaveSessionAsync(SessionStore sessionStore, string filePath, CancellationToken cancellationToken = default)
    {
        // 使用 keyed_semaphore 协调跨线程对特定文件的访问
        var semaphore = GetSemaphoreForFile(filePath);

        await semaphore.WaitAsync(cancellationToken);
        try
        {
            // 确保目录存在
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 写入时使用 FileShare.None 防止并发修改
            using var fileStream = new FileStream(
                filePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true);

            using var writer = new StreamWriter(fileStream);

            foreach (var message in sessionStore.Messages)
            {
                var jsonLine = JsonSerializer.Serialize(message, _jsonOptions);
                await writer.WriteLineAsync(jsonLine);
                await writer.FlushAsync(); // 确保数据立即写入
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// 异步将消息追加到现有 JSONL 会话文件。
    /// </summary>
    /// <param name="message">要追加的消息</param>
    /// <param name="filePath">JSONL 文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task AppendToSessionAsync(ChatMessage message, string filePath, CancellationToken cancellationToken = default)
    {
        // 使用 keyed_semaphore 协调跨线程对特定文件的访问
        var semaphore = GetSemaphoreForFile(filePath);

        await semaphore.WaitAsync(cancellationToken);
        try
        {
            // 确保目录存在
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 使用追加模式和 FileShare.Read 允许并发读取
            using var fileStream = new FileStream(
                filePath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);

            using var writer = new StreamWriter(fileStream);

            var jsonLine = JsonSerializer.Serialize(message, _jsonOptions);
            await writer.WriteLineAsync(jsonLine);
            await writer.FlushAsync(); // Ensure data is written immediately
        }
        finally
        {
            semaphore.Release();
        }
    }

    private SemaphoreSlim GetSemaphoreForFile(string filePath)
    {
        lock (_lock)
        {
            var fullPath = Path.GetFullPath(filePath);
            if (!_semaphoreLocks.ContainsKey(fullPath))
            {
                _semaphoreLocks[fullPath] = new SemaphoreSlim(1, 1);
            }
            return _semaphoreLocks[fullPath];
        }
    }

    /// <summary>
    /// 处理加载过程中的反序列化错误。
    /// </summary>
    protected virtual void OnDeserializationError(string line, int lineNumber, JsonException exception, string fileName)
    {
        // 将错误记录到 stderr 并提供文件信息和行号用于诊断
        Console.Error.WriteLine($"Warning: Malformed JSON at {fileName}:{lineNumber}: {exception.Message}");
        Console.Error.WriteLine($"Problematic line: {line}");
    }
}

/// <summary>
/// 自定义 JSON 转换器工厂，用于处理 ContentBlock 类型的多态序列化。
/// 使用类型鉴别器字段来确定在反序列化期间实例化哪个具体的 ContentBlock 实现。
/// </summary>
public class ContentBlockJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeof(ContentBlock).IsAssignableFrom(typeToConvert);
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        return new ContentBlockJsonConverter();
    }

    private class ContentBlockJsonConverter : JsonConverter<ContentBlock>
    {
        public override ContentBlock? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var element = doc.RootElement;

            string? contentBlockType = null;
            if (element.TryGetProperty("type", out JsonElement typeElement))
            {
                contentBlockType = typeElement.GetString();
            }
            else if (element.TryGetProperty("_type", out JsonElement underscoreTypeElement))
            {
                contentBlockType = underscoreTypeElement.GetString();
            }
            else
            {
                if (element.TryGetProperty("text", out _))
                {
                    contentBlockType = "text";
                }
                else if (element.TryGetProperty("thinking", out _))
                {
                    contentBlockType = "thinking";
                }
                else if (element.TryGetProperty("id", out _) && element.TryGetProperty("name", out _) && element.TryGetProperty("input", out _))
                {
                    contentBlockType = "tool_use";
                }
                else if (element.TryGetProperty("tool_use_id", out _) && element.TryGetProperty("content", out _))
                {
                    contentBlockType = "tool_result";
                }
                else
                {
                    throw new JsonException("Cannot determine ContentBlock type from JSON. Required properties missing.");
                }
            }

            return contentBlockType?.ToLowerInvariant() switch
            {
                "text" or "text_block" => ReadTextBlock(element),
                "thinking" or "reasoning_block" or "thinking_block" => ReadThinkingBlock(element),
                "tool_use" or "tooluse" or "tool_use_block" => ReadToolUseBlock(element, options),
                "tool_result" or "toolresult" or "tool_result_block" => ReadToolResultBlock(element),
                _ => throw new JsonException($"Unknown content block type: {contentBlockType}")
            };
        }

        private static TextBlock ReadTextBlock(JsonElement element)
        {
            var text = element.TryGetProperty("text", out var textProp) ? textProp.GetString() ?? string.Empty : string.Empty;
            return new TextBlock(text);
        }

        private static ThinkingBlock ReadThinkingBlock(JsonElement element)
        {
            var thinking = element.TryGetProperty("thinking", out var thinkingProp) ? thinkingProp.GetString() ?? string.Empty : string.Empty;
            return new ThinkingBlock(thinking);
        }

        private static ToolUseBlock ReadToolUseBlock(JsonElement element, JsonSerializerOptions options)
        {
            var id = element.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? string.Empty : string.Empty;
            var name = element.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? string.Empty : string.Empty;
            JsonElement input = default;
            if (element.TryGetProperty("input", out var inputProp))
            {
                input = inputProp;
            }
            return new ToolUseBlock(id, name, input);
        }

        private static ToolResultBlock ReadToolResultBlock(JsonElement element)
        {
            var toolUseId = element.TryGetProperty("tool_use_id", out var idProp) ? idProp.GetString() ?? string.Empty : string.Empty;
            var content = element.TryGetProperty("content", out var contentProp) ? contentProp.GetString() ?? string.Empty : string.Empty;
            var isError = element.TryGetProperty("is_error", out var errorProp) && errorProp.GetBoolean();
            return new ToolResultBlock(toolUseId, content, isError);
        }

        public override void Write(Utf8JsonWriter writer, ContentBlock value, JsonSerializerOptions options)
        {
            // 添加类型鉴别器用于反序列化识别
            if (value is TextBlock textBlock)
            {
                writer.WriteStartObject();
                writer.WriteString("type", "text");
                writer.WriteString("text", textBlock.Text);
                writer.WriteEndObject();
            }
            else if (value is ThinkingBlock thinkingBlock)
            {
                writer.WriteStartObject();
                writer.WriteString("type", "thinking");
                writer.WriteString("thinking", thinkingBlock.Thinking);
                writer.WriteEndObject();
            }
            else if (value is ToolUseBlock toolUseBlock)
            {
                writer.WriteStartObject();
                writer.WriteString("type", "tool_use");
                writer.WriteString("id", toolUseBlock.Id);
                writer.WriteString("name", toolUseBlock.Name);
                writer.WritePropertyName("input");
                writer.WriteRawValue(toolUseBlock.Input.GetRawText());
                writer.WriteEndObject();
            }
            else if (value is ToolResultBlock toolResultBlock)
            {
                writer.WriteStartObject();
                writer.WriteString("type", "tool_result");
                writer.WriteString("tool_use_id", toolResultBlock.ToolUseId);
                writer.WriteString("content", toolResultBlock.Content);
                writer.WriteBoolean("is_error", toolResultBlock.IsError);
                writer.WriteEndObject();
            }
            else
            {
                throw new NotSupportedException($"ContentBlock type not supported: {value.GetType()}");
            }
        }
    }
}

// 帮助将 JsonElement 转换为对象的扩展方法
internal static class JsonElementExtensions
{
    internal static T ToObjectFromJson<T>(this JsonElement element, JsonSerializerOptions options)
    {
        var json = element.GetRawText();
        return JsonSerializer.Deserialize<T>(json, options) ?? throw new JsonException($"Could not deserialize to {typeof(T)}");
    }
}
