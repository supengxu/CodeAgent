using System.Runtime.CompilerServices;
using CodeAgentDemo.Models;

namespace CodeAgentDemo.Providers;

/// <summary>
/// Unified interface for LLM chat providers.
/// </summary>
public interface IChatProvider
{
    /// <summary>
    /// Gets the provider name.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Streams chat completion responses.
    /// </summary>
    IAsyncEnumerable<StreamChunk> CompleteStreamingAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default);
}
