using CodeAgentDemo.Models;

namespace CodeAgentDemo.Providers;

public static class ChatProviderFactory
{
    public static IChatProvider Create()
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? throw new InvalidOperationException("OPENAI_API_KEY environment variable is required");

        var model = Environment.GetEnvironmentVariable("OPENAI_MODEL")
            ?? "gpt-4o";

        var endpoint = Environment.GetEnvironmentVariable("OPENAI_API_URL");

        var enableThinking = bool.TryParse(Environment.GetEnvironmentVariable("ENABLE_THINKING"), out var et) && et;

        return new OpenAIProvider(apiKey, model, endpoint, enableThinking);
    }
}