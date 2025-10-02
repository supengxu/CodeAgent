using System.Text.Json;
using CodeAgentDemo.Core;
using CodeAgentDemo.Models;
using CodeAgentDemo.Services;
using FluentAssertions;
using Xunit;

namespace CodeAgentDemo.Tests.Services;

public class SimpleTokenCounterTests
{
    private readonly SimpleTokenCounter _counter = new();

    [Fact]
    public void EstimateTokenCount_WithEmptyString_ShouldReturnZero()
    {
        var result = _counter.EstimateTokenCount("");

        result.Should().Be(0);
    }

    [Fact]
    public void EstimateTokenCount_WithNullString_ShouldReturnZero()
    {
        var result = _counter.EstimateTokenCount(null!);

        result.Should().Be(0);
    }

    [Theory]
    [InlineData("test", 2)]
    [InlineData("hello world", 3)]
    [InlineData("a", 1)]
    [InlineData("abcd", 2)]
    public void EstimateTokenCount_WithVariousStrings_ShouldEstimateCorrectly(string text, int expected)
    {
        var result = _counter.EstimateTokenCount(text);

        result.Should().Be(expected);
    }

    [Fact]
    public void EstimateMessageTokens_WithTextBlock_ShouldEstimateTokens()
    {
        var message = ChatMessage.CreateText(ChatRole.User, "Hello World");

        var result = _counter.EstimateMessageTokens(message);

        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public void EstimateMessageTokens_WithMultipleBlocks_ShouldSumTokens()
    {
        var blocks = new ContentBlock[]
        {
            new TextBlock("Hello"),
            new ThinkingBlock("Thinking...")
        };
        var message = new ChatMessage(ChatRole.Assistant, blocks);

        var result = _counter.EstimateMessageTokens(message);

        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public void EstimateMessageTokens_WithToolUseBlock_ShouldEstimateTokens()
    {
        var input = JsonDocument.Parse("{\"command\":\"ls\"}").RootElement;
        var blocks = new ContentBlock[]
        {
            new ToolUseBlock("id-1", "bash", input)
        };
        var message = new ChatMessage(ChatRole.Assistant, blocks);

        var result = _counter.EstimateMessageTokens(message);

        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public void EstimateMessageTokens_WithToolResultBlock_ShouldEstimateTokens()
    {
        var blocks = new ContentBlock[]
        {
            new ToolResultBlock("id-1", "Result output")
        };
        var message = new ChatMessage(ChatRole.Tool, blocks);

        var result = _counter.EstimateMessageTokens(message);

        result.Should().BeGreaterThan(0);
    }
}

public class HistoryManagerTests
{
    private readonly SimpleTokenCounter _counter = new();

    [Fact]
    public void Trim_WithEmptyMessages_ShouldReturnEmpty()
    {
        var manager = new HistoryManager(_counter, 1000);
        var messages = Array.Empty<ChatMessage>();

        var result = manager.Trim(messages);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Trim_WithSmallMessages_ShouldReturnAll()
    {
        var manager = new HistoryManager(_counter, 10000);
        var messages = new[]
        {
            ChatMessage.CreateText(ChatRole.User, "Hello"),
            ChatMessage.CreateText(ChatRole.Assistant, "Hi there!")
        };

        var result = manager.Trim(messages);

        result.Should().HaveCount(2);
    }

    [Fact]
    public void Trim_WithLargeMessages_ShouldTrim()
    {
        var manager = new HistoryManager(_counter, 100, 1);
        var messages = new List<ChatMessage>();

        for (int i = 0; i < 10; i++)
        {
            messages.Add(ChatMessage.CreateText(ChatRole.User, $"Message {i} with some additional text to make it longer"));
            messages.Add(ChatMessage.CreateText(ChatRole.Assistant, $"Response {i} with some additional text to make it longer"));
        }

        var result = manager.Trim(messages);

        result.Count().Should().BeLessThan(messages.Count);
    }

    [Fact]
    public void Trim_ShouldPreserveRecentMessages()
    {
        var manager = new HistoryManager(_counter, 100, 2);
        var messages = new List<ChatMessage>();

        for (int i = 0; i < 10; i++)
        {
            messages.Add(ChatMessage.CreateText(ChatRole.User, $"User message {i}"));
            messages.Add(ChatMessage.CreateText(ChatRole.Assistant, $"Assistant response {i}"));
        }

        var result = manager.Trim(messages).ToList();

        result.Should().Contain(m => ((TextBlock)m.Content.First()).Text.Contains("User message 9"));
        result.Should().Contain(m => ((TextBlock)m.Content.First()).Text.Contains("Assistant response 9"));
    }

    [Fact]
    public void EstimateTotalTokens_WithEmptyMessages_ShouldReturnZero()
    {
        var manager = new HistoryManager(_counter);

        var result = manager.EstimateTotalTokens(Array.Empty<ChatMessage>());

        result.Should().Be(0);
    }

    [Fact]
    public void EstimateTotalTokens_WithMessages_ShouldReturnSum()
    {
        var manager = new HistoryManager(_counter);
        var messages = new[]
        {
            ChatMessage.CreateText(ChatRole.User, "Hello"),
            ChatMessage.CreateText(ChatRole.Assistant, "Hi!")
        };

        var result = manager.EstimateTotalTokens(messages);

        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public void NeedsTrimming_WithSmallMessages_ShouldReturnFalse()
    {
        var manager = new HistoryManager(_counter, 10000);
        var messages = new[]
        {
            ChatMessage.CreateText(ChatRole.User, "Hello")
        };

        var result = manager.NeedsTrimming(messages);

        result.Should().BeFalse();
    }

    [Fact]
    public void NeedsTrimming_WithLargeMessages_ShouldReturnTrue()
    {
        var manager = new HistoryManager(_counter, 10);
        var messages = new List<ChatMessage>();

        for (int i = 0; i < 100; i++)
        {
            messages.Add(ChatMessage.CreateText(ChatRole.User, $"Message {i} with lots of text"));
        }

        var result = manager.NeedsTrimming(messages);

        result.Should().BeTrue();
    }
}

public class ITokenCounterTests
{
    [Fact]
    public void ITokenCounter_ShouldHaveEstimateTokenCountMethod()
    {
        var method = typeof(ITokenCounter).GetMethod("EstimateTokenCount");
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(int));
    }

    [Fact]
    public void ITokenCounter_ShouldHaveEstimateMessageTokensMethod()
    {
        var method = typeof(ITokenCounter).GetMethod("EstimateMessageTokens");
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(int));
    }
}