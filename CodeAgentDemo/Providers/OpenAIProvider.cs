using System.Runtime.CompilerServices;
using CodeAgentDemo.Models;
using CodeAgentDemo.Services;

namespace CodeAgentDemo.Providers;

public class OpenAIProvider : IChatProvider
{
    private readonly OpenAI.Chat.ChatClient _client;
    private readonly string _model;
    private readonly IOpenAIConverter _converter;

    public OpenAIProvider(
        ChatOptions chatOptions,
        IOpenAIConverter converter)
    {
        ArgumentNullException.ThrowIfNull(chatOptions);
        ArgumentNullException.ThrowIfNull(converter);

        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? throw new InvalidOperationException("OPENAI_API_KEY environment variable is required");

        var model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o";
        var endpoint = Environment.GetEnvironmentVariable("OPENAI_API_URL");

        _model = model;
        var options = new OpenAI.OpenAIClientOptions();

        if (!string.IsNullOrEmpty(endpoint))
        {
            options.Endpoint = new Uri(endpoint);
        }

        var openAIClient = new OpenAI.OpenAIClient(new System.ClientModel.ApiKeyCredential(apiKey), options);
        _client = openAIClient.GetChatClient(model);
        _converter = converter;
    }

    public string ProviderName => "OpenAI";

    public async IAsyncEnumerable<StreamChunk> CompleteStreamingAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var openaiMessages = _converter.ToOpenAIMessages(messages, options?.SystemPrompt);

        var chatOptions = new OpenAI.Chat.ChatCompletionOptions();
        if (options?.Tools != null)
        {
            foreach (var tool in options.Tools)
            {
                chatOptions.Tools.Add(_converter.ToOpenAITool(tool));
            }
        }

        await foreach (var update in _client.CompleteChatStreamingAsync(openaiMessages, chatOptions, cancellationToken))
        {
            yield return _converter.FromOpenAIUpdate(update);
        }
    }
}