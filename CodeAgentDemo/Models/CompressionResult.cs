namespace CodeAgentDemo.Models;

/// <summary>
/// 上下文压缩操作的结果。
/// </summary>
public record CompressionResult
{
    /// <summary>
    /// 压缩后的消息列表。
    /// </summary>
    public required IReadOnlyList<ChatMessage> CompressedMessages { get; init; }

    /// <summary>
    /// 压缩过程中移除的令牌数。
    /// </summary>
    public int RemovedTokens { get; init; }

    /// <summary>
    /// 压缩是否成功。
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 压缩失败时的错误信息。
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 创建成功的压缩结果。
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
    /// 创建失败的压缩结果。
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