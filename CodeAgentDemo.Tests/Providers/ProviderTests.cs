using System.Runtime.CompilerServices;
using System.Text.Json;
using CodeAgentDemo.Models;
using CodeAgentDemo.Providers;
using CodeAgentDemo.Services;
using DotNetEnv;
using FluentAssertions;
using Moq;
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

        if (_hasApiKey)
        {
            var enableThinking = bool.TryParse(Environment.GetEnvironmentVariable("ENABLE_THINKING"), out var et) && et;
            var options = new ChatOptions { EnableThinking = enableThinking };
            var converter = new OpenAIConverter(options);
            _provider = new OpenAIProvider(options, converter);
        }
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void ProviderName_ShouldBeOpenAI()
    {
        if (!_hasApiKey) return;
        _provider!.ProviderName.Should().Be("OpenAI");
    }

    [Fact]
    public async Task CompleteStreamingAsync_ShouldYieldChunks()
    {
        if (!_hasApiKey) return;

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
    public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
    {
        var converter = new Mock<IOpenAIConverter>().Object;
        var act = () => new OpenAIProvider(null!, converter);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("chatOptions");
    }

    [Fact]
    public void Constructor_WithNullConverter_ShouldThrowArgumentNullException()
    {
        var options = new ChatOptions();
        var act = () => new OpenAIProvider(options, null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("converter");
    }

    [Fact]
    public void Constructor_WithoutApiKey_ShouldThrowInvalidOperationException()
    {
        var originalKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);

        try
        {
            var options = new ChatOptions();
            var converter = new Mock<IOpenAIConverter>().Object;
            var act = () => new OpenAIProvider(options, converter);

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*OPENAI_API_KEY*");
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", originalKey);
        }
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