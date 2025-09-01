using System.Runtime.CompilerServices;
using Anthropic.SDK;
using CodeAgentDemo.Models;

namespace CodeAgentDemo.Providers;

public class AnthropicProvider : IChatProvider
{
    private readonly AnthropicClient _client;
    private readonly string _model;
    private readonly bool _enableThinking;
    private readonly int _thinkingBudget;

    public AnthropicProvider(string apiKey, string model, bool enableThinking = false, int thinkingBudget = 0)
    {
        _client = new AnthropicClient(apiKey);
        _model = model;
        _enableThinking = enableThinking;
        _thinkingBudget = thinkingBudget;
    }

    public string ProviderName => "Anthropic";

    public async IAsyncEnumerable<StreamChunk> CompleteStreamingAsync(
        IEnumerable<CodeAgentDemo.Models.ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return new StreamChunk("Anthropic response", null, null);
    }
}
