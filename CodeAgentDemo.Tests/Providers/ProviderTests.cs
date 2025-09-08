using System.Runtime.CompilerServices;
using System.Text.Json;
using CodeAgentDemo.Models;
using CodeAgentDemo.Providers;
using DotNetEnv;
using FluentAssertions;
using Xunit;

namespace CodeAgentDemo.Tests.Providers;

public class AnthropicProviderTests : IAsyncLifetime
{
    private readonly AnthropicProvider _provider;

    public AnthropicProviderTests()
    {
        Env.Load();
        
        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
            ?? throw new InvalidOperationException("ANTHROPIC_API_KEY not set");
        var model = Environment.GetEnvironmentVariable("ANTHROPIC_MODEL") ?? "claude-sonnet-4";
        var baseUrl = Environment.GetEnvironmentVariable("ANTHROPIC_BASE_URL");
        var enableThinking = bool.TryParse(Environment.GetEnvironmentVariable("ENABLE_THINKING"), out var et) && et;
        var thinkingBudget = int.TryParse(Environment.GetEnvironmentVariable("THINKING_BUDGET_TOKENS"), out var tb) ? tb : 0;

        _provider = new AnthropicProvider(apiKey, model, enableThinking, thinkingBudget, baseUrl);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void ProviderName_ShouldBeAnthropic()
    {
        _provider.ProviderName.Should().Be("Anthropic");
    }

    [Fact]
    public async Task CompleteStreamingAsync_ShouldYieldChunks()
    {
        var messages = new[]
        {
            ChatMessage.CreateText(ChatRole.User, "Hello")
        };

        var chunks = new List<StreamChunk>();
        await foreach (var chunk in _provider.CompleteStreamingAsync(messages))
        {
            chunks.Add(chunk);
        }

        chunks.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CompleteStreamingAsync_WithCancellationToken_ShouldRespectCancellation()
    {
        var messages = new[]
        {
            ChatMessage.CreateText(ChatRole.User, "Hello")
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () =>
        {
            await foreach (var chunk in _provider.CompleteStreamingAsync(messages, null, cts.Token))
            {
            }
        };

        await act.Should().NotThrowAsync();
    }
}

public class OpenAIProviderTests : IAsyncLifetime
{
    private readonly OpenAIProvider _provider;

    public OpenAIProviderTests()
    {
        Env.Load();
        
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? throw new InvalidOperationException("OPENAI_API_KEY not set");
        var model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4";
        var endpoint = Environment.GetEnvironmentVariable("OPENAI_API_URL");
        var enableThinking = bool.TryParse(Environment.GetEnvironmentVariable("ENABLE_THINKING"), out var et) && et;

        _provider = new OpenAIProvider(apiKey, model, endpoint, enableThinking);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void ProviderName_ShouldBeOpenAI()
    {
        _provider.ProviderName.Should().Be("OpenAI");
    }

    [Fact]
    public async Task CompleteStreamingAsync_ShouldYieldChunks()
    {
        var messages = new[]
        {
            ChatMessage.CreateText(ChatRole.User, "Hello")
        };

        var chunks = new List<StreamChunk>();
        await foreach (var chunk in _provider.CompleteStreamingAsync(messages))
        {
            chunks.Add(chunk);
        }

        chunks.Should().NotBeEmpty();
    }

    [Fact]
    public void Constructor_WithCustomEndpoint_ShouldNotThrow()
    {
        var act = () => new OpenAIProvider("test-key", "gpt-4", "https://custom.api/v1");

        act.Should().NotThrow();
    }
}

public class IChatProviderTests
{
    [Fact]
    public void IChatProvider_ShouldDefineProviderName()
    {
        typeof(IChatProvider).GetProperty("ProviderName").Should().NotBeNull();
    }

    [Fact]
    public void IChatProvider_ShouldDefineCompleteStreamingAsync()
    {
        var method = typeof(IChatProvider).GetMethod("CompleteStreamingAsync");
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(IAsyncEnumerable<StreamChunk>));
    }
}