using System.Diagnostics.CodeAnalysis;
using CodeAgentDemo.Models;
using CodeAgentDemo.Services;

namespace CodeAgentDemo.Core;

/// <summary>
/// Manages conversation context and compression.
/// Implements sliding window compression while preserving critical context.
/// </summary>
public class ContextManager : IContextManager
{
    private readonly ITokenCounter _tokenCounter;
    private readonly ContextConfig _config;

    /// <summary>
    /// Initializes a new instance of the ContextManager class.
    /// </summary>
    /// <param name="tokenCounter">The token counter for estimating token counts.</param>
    /// <param name="config">The context configuration.</param>
    public ContextManager(ITokenCounter tokenCounter, ContextConfig config)
    {
        ArgumentNullException.ThrowIfNull(tokenCounter);
        ArgumentNullException.ThrowIfNull(config);
        _tokenCounter = tokenCounter;
        _config = config;
    }

    /// <inheritdoc />
    public int GetTokenCount(IEnumerable<ChatMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        return messages.Sum(m => _tokenCounter.EstimateMessageTokens(m));
    }

    /// <inheritdoc />
    public bool ShouldCompress(IEnumerable<ChatMessage> messages, int? currentTokenCount = null)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var tokenCount = currentTokenCount ?? GetTokenCount(messages);
        return tokenCount > _config.CompressionThreshold;
    }

    /// <inheritdoc />
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Compression should not throw, returns failed result instead")]
    public Task<CompressionResult> CompressAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var messageList = messages.ToList();

            if (messageList.Count == 0)
            {
                return Task.FromResult(CompressionResult.Succeeded(Array.Empty<ChatMessage>(), 0));
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Calculate original token count
            var originalTokens = GetTokenCount(messageList);

            // Preserve system messages (they are protected)
            var systemMessages = messageList
                .Where(m => m.Role == ChatRole.System)
                .ToList();

            // Get non-system messages for sliding window
            var nonSystemMessages = messageList
                .Where(m => m.Role != ChatRole.System)
                .ToList();

            // Preserve the most recent turns (MinRecentTurns)
            // A "turn" is a user-assistant exchange, but we preserve by message count for simplicity
            var recentMessages = nonSystemMessages
                .TakeLast(_config.MinRecentTurns)
                .ToList();

            // Combine preserved messages in original order
            var preservedMessages = new List<ChatMessage>();
            var preservedSet = new HashSet<ChatMessage>(systemMessages);
            preservedSet.UnionWith(recentMessages);

            foreach (var message in messageList)
            {
                if (preservedSet.Contains(message))
                {
                    preservedMessages.Add(message);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Calculate preserved token count
            var preservedTokens = GetTokenCount(preservedMessages);

            return Task.FromResult(CompressionResult.Succeeded(
                preservedMessages,
                originalTokens - preservedTokens));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Task.FromResult(CompressionResult.Failed(ex.Message));
        }
    }
}