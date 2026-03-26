using System.Diagnostics.CodeAnalysis;
using CodeAgentDemo.Models;
using CodeAgentDemo.Services;

namespace CodeAgentDemo.Core;

/// <summary>
/// 管理对话上下文和压缩。
/// 实现滑动窗口压缩，同时保留关键上下文。
/// </summary>
public class ContextManager : IContextManager
{
    private readonly ITokenCounter _tokenCounter;
    private readonly ContextConfig _config;

    /// <summary>
    /// 初始化 ContextManager 类的新实例。
    /// </summary>
    /// <param name="tokenCounter">用于估算 token 数量的 Token 计数器。</param>
    /// <param name="config">上下文配置。</param>
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

            // 计算原始 token 数量
            var originalTokens = GetTokenCount(messageList);

            // 保留系统消息（受保护）
            var systemMessages = messageList
                .Where(m => m.Role == ChatRole.System)
                .ToList();

            // 获取非系统消息用于滑动窗口
            var nonSystemMessages = messageList
                .Where(m => m.Role != ChatRole.System)
                .ToList();

            // 保留最近的几轮对话（MinRecentTurns）
            // "一轮" 是指用户-助手的交互，但为简单起见我们按消息数量保留
            var recentMessages = nonSystemMessages
                .TakeLast(_config.MinRecentTurns)
                .ToList();

            // 按原始顺序组合保留的消息
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

            // 计算保留的 token 数量
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