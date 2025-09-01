using System.ClientModel;
using System.Runtime.CompilerServices;
using OpenAI;
using OpenAI.Chat;
using CodeAgentDemo.Models;

namespace CodeAgentDemo.Providers;

public class OpenAIProvider : IChatProvider
{
    private readonly ChatClient _client;
    private readonly string _model;

    public OpenAIProvider(string apiKey, string model, string? endpoint = null)
    {
        _model = model;
        var clientOptions = endpoint != null
            ? new OpenAIClientOptions { Endpoint = new Uri(endpoint) }
            : new OpenAIClientOptions();

        var openAIClient = new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions);
        _client = openAIClient.GetChatClient(model);
    }

    public string ProviderName => "OpenAI";

    public async IAsyncEnumerable<StreamChunk> CompleteStreamingAsync(
        IEnumerable<CodeAgentDemo.Models.ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Simplified implementation - always yield a text response
        // Full SDK integration requires proper type conversions
        yield return new StreamChunk("OpenAI response placeholder", null, null);
    }
}
