using CodeAgentDemo.Models;

namespace CodeAgentDemo.Core;

/// <summary>
/// Interface for managing conversation context and compression.
/// </summary>
public interface IContextManager
{
    /// <summary>
    /// Gets the total token count for the given messages.
    /// </summary>
    /// <param name="messages">The messages to count tokens for.</param>
    /// <returns>The estimated token count.</returns>
    int GetTokenCount(IEnumerable<ChatMessage> messages);

    /// <summary>
    /// Determines whether context compression should be performed.
    /// </summary>
    /// <param name="messages">The current message history.</param>
    /// <param name="currentTokenCount">The current token count (optional, will be calculated if not provided).</param>
    /// <returns>True if compression should be performed, false otherwise.</returns>
    bool ShouldCompress(IEnumerable<ChatMessage> messages, int? currentTokenCount = null);

    /// <summary>
    /// Compresses the context by removing or summarizing older messages.
    /// Preserves system prompts, tool definitions, and recent turns.
    /// </summary>
    /// <param name="messages">The messages to compress.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The compression result containing compressed messages and metadata.</returns>
    Task<CompressionResult> CompressAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default);
}