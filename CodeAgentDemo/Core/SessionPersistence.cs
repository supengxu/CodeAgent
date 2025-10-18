using System.Text.Json;
using System.Text.Json.Serialization;
using CodeAgentDemo.Models;

namespace CodeAgentDemo.Core;

/// <summary>
/// Provides JSONL-based session persistence with robust error handling, 
/// polymorphic ContentBlock serialization, and concurrent access protection.
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

        // Configure polymorphic serialization for ContentBlock hierarchy
        options.Converters.Add(new ContentBlockJsonConverterFactory());

        return options;
    }

    /// <summary>
    /// Asynchronously loads a session from a JSONL file.
    /// </summary>
    /// <param name="filePath">Path to the JSONL file containing the session</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Loaded SessionStore with messages</returns>
    public async Task<SessionStore> LoadSessionAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var sessionStore = new SessionStore();

        if (!File.Exists(filePath))
        {
            return sessionStore;
        }

        // Use FileShare.ReadWrite to allow concurrent access 
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
                // Log malformed JSON but continue processing other lines
                OnDeserializationError(line, lineNumber, ex, filePath);

                // Continue processing the rest of the file
                continue;
            }
            catch (Exception ex)
            {
                // Log unexpected errors but continue processing
                OnDeserializationError(line, lineNumber, new JsonException($"Unexpected error deserializing line {lineNumber}", ex), filePath);
                continue;
            }
        }

        return sessionStore;
    }

    /// <summary>
    /// Asynchronously saves a session to a JSONL file with file mutex for thread safety.
    /// Each message is written as a separate JSON line.
    /// </summary>
    /// <param name="sessionStore">The session store to save</param>
    /// <param name="filePath">Path to the JSONL file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task SaveSessionAsync(SessionStore sessionStore, string filePath, CancellationToken cancellationToken = default)
    {
        // Use a keyed_semaphore for coordinating access to specific files across threads
        var semaphore = GetSemaphoreForFile(filePath);

        await semaphore.WaitAsync(cancellationToken);
        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Use FileShare.None during write to prevent concurrent modifications
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
                await writer.FlushAsync(); // Ensure data is written immediately
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Asynchronously appends a message to an existing JSONL session file.
    /// </summary>
    /// <param name="message">Message to append</param>
    /// <param name="filePath">Path to the JSONL file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task AppendToSessionAsync(ChatMessage message, string filePath, CancellationToken cancellationToken = default)
    {
        // Use a keyed_semaphore for coordinating access to specific files across threads
        var semaphore = GetSemaphoreForFile(filePath);

        await semaphore.WaitAsync(cancellationToken);
        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Use append mode with FileShare.Read to allow concurrent reads
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
    /// Handles deserialization errors during the loading process.
    /// </summary>
    protected virtual void OnDeserializationError(string line, int lineNumber, JsonException exception, string fileName)
    {
        // Log the error to stderr with file and line info for diagnostic purposes
        Console.Error.WriteLine($"Warning: Malformed JSON at {fileName}:{lineNumber}: {exception.Message}");
        Console.Error.WriteLine($"Problematic line: {line}");
    }
}

/// <summary>
/// Custom JSON converter factory to handle polymorphic serialization of ContentBlock types.
/// Uses a type discriminator field to determine which concrete ContentBlock implementation 
/// to instantiate during deserialization.
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
            // Add a type discriminator for deserialization identification
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

// Extension method to help convert JsonElement to objects
internal static class JsonElementExtensions
{
    internal static T ToObjectFromJson<T>(this JsonElement element, JsonSerializerOptions options)
    {
        var json = element.GetRawText();
        return JsonSerializer.Deserialize<T>(json, options) ?? throw new JsonException($"Could not deserialize to {typeof(T)}");
    }
}
