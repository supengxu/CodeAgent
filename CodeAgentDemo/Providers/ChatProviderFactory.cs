using CodeAgentDemo.Models;
using DotNetEnv;

namespace CodeAgentDemo.Providers;

public static class ChatProviderFactory
{
    public static IChatProvider Create()
    {
        DotNetEnv.Env.Load();

        var provider = Environment.GetEnvironmentVariable("AI_PROVIDER")?.ToLower()
            ?? throw new InvalidOperationException("AI_PROVIDER environment variable is required");

        return provider switch
        {
            "anthropic" => CreateAnthropicProvider(),
            "openai" => CreateOpenAIProvider(),
            _ => throw new InvalidOperationException($"Unknown provider: {provider}")
        };
    }

    private static AnthropicProvider CreateAnthropicProvider()
    {
        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
            ?? throw new InvalidOperationException("ANTHROPIC_API_KEY environment variable is required");

        var model = Environment.GetEnvironmentVariable("ANTHROPIC_MODEL")
            ?? "claude-sonnet-4-20250514";

        var enableThinking = bool.TryParse(Environment.GetEnvironmentVariable("ENABLE_THINKING"), out var et) && et;
        var thinkingBudget = int.TryParse(Environment.GetEnvironmentVariable("THINKING_BUDGET_TOKENS"), out var tb)
            ? tb : 10000;

        return new AnthropicProvider(apiKey, model, enableThinking, thinkingBudget);
    }

    private static OpenAIProvider CreateOpenAIProvider()
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? throw new InvalidOperationException("OPENAI_API_KEY environment variable is required");

        var model = Environment.GetEnvironmentVariable("OPENAI_MODEL")
            ?? "gpt-4o";

        var endpoint = Environment.GetEnvironmentVariable("OPENAI_API_URL");

        return new OpenAIProvider(apiKey, model, endpoint);
    }
}
