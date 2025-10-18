using System.Runtime.CompilerServices;
using System.Text.Json;
using CodeAgentDemo.Models;
using CodeAgentDemo.Providers;
using DotNetEnv;
using FluentAssertions;
using Xunit;

namespace CodeAgentDemo.Tests.Providers;

public class OpenAIProviderTests : IAsyncLifetime
{
    private readonly OpenAIProvider? _provider;
    private readonly bool _hasApiKey;

    public OpenAIProviderTests()
    {
        Env.Load();

        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        _hasApiKey = !string.IsNullOrEmpty(apiKey);
        var model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4";
        var endpoint = Environment.GetEnvironmentVariable("OPENAI_API_URL");
        var enableThinking = bool.TryParse(Environment.GetEnvironmentVariable("ENABLE_THINKING"), out var et) && et;

        if (_hasApiKey)
        {
            _provider = new OpenAIProvider(apiKey!, model, endpoint, enableThinking);
        }
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void ProviderName_ShouldBeOpenAI()
    {
        if (!_hasApiKey) return; // Skip if no API key
        _provider!.ProviderName.Should().Be("OpenAI");
    }

    [Fact]
    public async Task CompleteStreamingAsync_ShouldYieldChunks()
    {
        if (!_hasApiKey) return; // Skip if no API key

        var messages = new[]
        {
            ChatMessage.CreateText(ChatRole.User, "Hello")
        };

        var chunks = new List<StreamChunk>();
        await foreach (var chunk in _provider!.CompleteStreamingAsync(messages))
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

    [Fact]
    public void Constructor_WithNullEndpoint_ShouldNotThrow()
    {
        var act = () => new OpenAIProvider("test-key", "gpt-4", null);

        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithThinkingEnabled_ShouldNotThrow()
    {
        var act = () => new OpenAIProvider("test-key", "gpt-4", null, true);

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