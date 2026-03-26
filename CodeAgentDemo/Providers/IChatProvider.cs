using System.Runtime.CompilerServices;
using CodeAgentDemo.Models;

namespace CodeAgentDemo.Providers;

/// <summary>
/// LLM 聊天提供商的统一接口。
/// </summary>
public interface IChatProvider
{
    /// <summary>
    /// 获取提供商名称。
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// 流式返回聊天完成响应。
    /// </summary>
    IAsyncEnumerable<StreamChunk> CompleteStreamingAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default);
}
