namespace CodeAgentDemo.Models;

/// <summary>
/// Result of a context compression operation.
/// </summary>
public record CompressionResult
{
    /// <summary>
    /// The compressed list of messages after the operation.
    /// </summary>
    public required IReadOnlyList<ChatMessage> CompressedMessages { get; init; }

    /// <summary>
    /// Number of tokens removed during compression.
    /// </summary>
    public int RemovedTokens { get; init; }

    /// <summary>
    /// Indicates whether the compression was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Optional error message if compression failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a successful compression result.
    /// </summary>
    public static CompressionResult Succeeded(IReadOnlyList<ChatMessage> messages, int removedTokens)
    {
        return new CompressionResult
        {
            CompressedMessages = messages,
            RemovedTokens = removedTokens,
            Success = true
        };
    }

    /// <summary>
    /// Creates a failed compression result.
    /// </summary>
    public static CompressionResult Failed(string errorMessage)
    {
        return new CompressionResult
        {
            CompressedMessages = Array.Empty<ChatMessage>(),
            RemovedTokens = 0,
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}