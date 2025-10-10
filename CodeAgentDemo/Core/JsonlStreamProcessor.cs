using System.Text.Json;
using System.Text.Json.Serialization;
using CodeAgentDemo.Models;

namespace CodeAgentDemo.Core;

/// <summary>
/// Provides streaming JSONL processing to handle large session files efficiently.
/// Processes each line as a separate JSON object without loading the entire file into memory.
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
        
        // Add the ContentBlock polymorphic converter
        _jsonOptions.Converters.Add(new ContentBlockJsonConverterFactory());
    }

    /// <summary>
    /// Asynchronously reads and processes each JSON line in the specified file.
    /// Useful for large files where loading everything into memory isn't feasible.
    /// </summary>
    /// <param name="filePath">Path to the JSONL file</param>
    /// <param name="processor">Function to process each loaded message</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the completion of the operation</returns>
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
            bufferSize: 8192, // Larger buffer for better sequential throughput
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
                    // Stop processing if the processor returns false
                    if (!await processor(message))
                    {
                        break;
                    }
                }
            }
            catch (JsonException ex)
            {
                // Log error but continue processing other lines
                OnCriticalLineError(filePath, lineNumber, line, ex);
                continue;
            }
        }
    }

    /// <summary>
    /// Asynchronously parses JSONL lines from a stream instead of a file.
    /// </summary>
    /// <param name="stream">Stream containing JSONL data</param>
    /// <param name="processor">Function to process each loaded message</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the completion of the operation</returns>
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
                    // Stop processing if the processor returns false
                    if (!await processor(message))
                    {
                        break;
                    }
                }
            }
            catch (JsonException ex)
            {
                // Log error but continue processing other lines
                OnCriticalLineError("[stream]", lineNumber, line, ex);
                continue;
            }
        }
    }

    /// <summary>
    /// Reads lines from a JSONL file asynchronously as an IAsyncEnumerable.
    /// Enables lazy processing and reduced memory usage for very large files.
    /// </summary>
    /// <param name="filePath">Path to the JSONL file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>IAsyncEnumerable of ChatMessage</returns>
    public async IAsyncEnumerable<ChatMessage> ReadJsonlFileAsync(
        string filePath, 
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            yield break; // Don't throw, just return an empty enumeration
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
                continue; // Skip erroneous lines and continue processing
            }

            if (message != null)
            {
                yield return message;
            }
        }
    }

    /// <summary>
    /// Appends a new entry to the JSONL file asynchronously.
    /// </summary>
    /// <param name="filePath">Path to JSONL file</param>
    /// <param name="chatMessage">Message to append</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task AppendAsync(string filePath, ChatMessage chatMessage, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var jsonLine = JsonSerializer.Serialize(chatMessage, _jsonOptions);
        
        // File.AppendText uses UTF8 encoding by default and handles line ending properly
        using var fileStream = new FileStream(
            filePath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);
            
        using var writer = new StreamWriter(fileStream);
        await writer.WriteLineAsync(jsonLine);
        await writer.FlushAsync(); // Ensure data is physically written
    }
    
    private void OnCriticalLineError(string fileOrStream, int lineNumber, string content, JsonException ex)
    {
        Console.Error.WriteLine($"Warning: Invalid JSON in {fileOrStream} at line {lineNumber}: {ex.Message}");
        Console.Error.WriteLine($"Content: {content.Substring(0, Math.Min(200, content.Length))}...");
    }
}
