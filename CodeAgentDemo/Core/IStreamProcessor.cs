using CodeAgentDemo.Models;

namespace CodeAgentDemo.Core;

/// <summary>
/// Processes streaming responses from LLM providers.
/// </summary>
public interface IStreamProcessor
{
    /// <summary>
    /// Collects and processes a streaming response.
    /// </summary>
    /// <param name="stream">The stream of chunks from the provider.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The processed stream response.</returns>
    Task<StreamResponse> ProcessAsync(IAsyncEnumerable<StreamChunk> stream, CancellationToken cancellationToken = default);
}