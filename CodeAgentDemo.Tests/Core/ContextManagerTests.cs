using CodeAgentDemo.Core;
using CodeAgentDemo.Models;
using CodeAgentDemo.Services;
using FluentAssertions;
using Xunit;

namespace CodeAgentDemo.Tests.Core;

public class ContextManagerTests
{
    #region GetTokenCount Tests

    [Fact]
    public void GetTokenCount_WithEmptyMessages_ShouldReturnZero()
    {
        // Arrange
        var config = new ContextConfig();
        var contextManager = CreateContextManager(config);
        var messages = Array.Empty<ChatMessage>();

        // Act
        var count = contextManager.GetTokenCount(messages);

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public void GetTokenCount_WithSingleMessage_ShouldReturnCorrectCount()
    {
        // Arrange
        var config = new ContextConfig();
        var contextManager = CreateContextManager(config);
        var messages = new[]
        {
            ChatMessage.CreateText(ChatRole.User, "Hello world")
        };

        // Act
        var count = contextManager.GetTokenCount(messages);

        // Assert
        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetTokenCount_WithMultipleMessages_ShouldReturnSumOfAllTokens()
    {
        // Arrange
        var config = new ContextConfig();
        var contextManager = CreateContextManager(config);
        var messages = new[]
        {
            ChatMessage.CreateText(ChatRole.User, "Hello"),
            ChatMessage.CreateText(ChatRole.Assistant, "Hi there"),
            ChatMessage.CreateText(ChatRole.User, "How are you?")
        };

        // Act
        var count = contextManager.GetTokenCount(messages);

        // Assert
        count.Should().BeGreaterThan(0);
    }

    #endregion

    #region ShouldCompress Tests

    [Fact]
    public void ShouldCompress_WhenBelowThreshold_ShouldReturnFalse()
    {
        // Arrange
        var config = new ContextConfig { CompressionThreshold = 10000 };
        var contextManager = CreateContextManager(config);
        var messages = new[]
        {
            ChatMessage.CreateText(ChatRole.User, "Short message")
        };

        // Act
        var result = contextManager.ShouldCompress(messages);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldCompress_WhenAboveThreshold_ShouldReturnTrue()
    {
        // Arrange
        var config = new ContextConfig { CompressionThreshold = 10 };
        var contextManager = CreateContextManager(config);

        // Create a long message that exceeds threshold
        var longText = new string('a', 1000);
        var messages = new[]
        {
            ChatMessage.CreateText(ChatRole.User, longText)
        };

        // Act
        var result = contextManager.ShouldCompress(messages);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldCompress_WithProvidedTokenCount_ShouldUseProvidedCount()
    {
        // Arrange
        var config = new ContextConfig { CompressionThreshold = 100 };
        var contextManager = CreateContextManager(config);
        var messages = Array.Empty<ChatMessage>();

        // Act
        var result = contextManager.ShouldCompress(messages, currentTokenCount: 200);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region CompressAsync Tests

    [Fact]
    public async Task CompressAsync_WithEmptyMessages_ShouldReturnEmptyResult()
    {
        // Arrange
        var config = new ContextConfig();
        var contextManager = CreateContextManager(config);
        var messages = Array.Empty<ChatMessage>();

        // Act
        var result = await contextManager.CompressAsync(messages);

        // Assert
        result.Success.Should().BeTrue();
        result.CompressedMessages.Should().BeEmpty();
        result.RemovedTokens.Should().Be(0);
    }

    [Fact]
    public async Task CompressAsync_ShouldPreserveSystemMessages()
    {
        // Arrange
        var config = new ContextConfig { MinRecentTurns = 2 };
        var contextManager = CreateContextManager(config);
        var messages = new[]
        {
            ChatMessage.CreateText(ChatRole.System, "System prompt"),
            ChatMessage.CreateText(ChatRole.User, "User message 1"),
            ChatMessage.CreateText(ChatRole.Assistant, "Assistant response 1"),
            ChatMessage.CreateText(ChatRole.User, "User message 2")
        };

        // Act
        var result = await contextManager.CompressAsync(messages);

        // Assert
        result.Success.Should().BeTrue();
        result.CompressedMessages.Should().Contain(m => m.Role == ChatRole.System);
    }

    [Fact]
    public async Task CompressAsync_ShouldPreserveMinRecentTurns()
    {
        // Arrange
        var config = new ContextConfig { MinRecentTurns = 4 };
        var contextManager = CreateContextManager(config);
        var messages = new[]
        {
            ChatMessage.CreateText(ChatRole.User, "Message 1"),
            ChatMessage.CreateText(ChatRole.Assistant, "Response 1"),
            ChatMessage.CreateText(ChatRole.User, "Message 2"),
            ChatMessage.CreateText(ChatRole.Assistant, "Response 2")
        };

        // Act
        var result = await contextManager.CompressAsync(messages);

        // Assert
        result.Success.Should().BeTrue();
        result.CompressedMessages.Should().HaveCount(4);
    }

    [Fact]
    public async Task CompressAsync_WhenCancelled_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var config = new ContextConfig();
        var contextManager = CreateContextManager(config);
        var messages = new[] { ChatMessage.CreateText(ChatRole.User, "Test") };
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => contextManager.CompressAsync(messages, cts.Token));
    }

    #endregion

    #region Protected Context Types Tests

    [Fact]
    public void ContextConfig_DefaultProtectedTypes_ShouldIncludeSystemAndToolDefinition()
    {
        // Arrange & Act
        var config = new ContextConfig();

        // Assert
        config.ProtectedContextTypes.Should().Contain("system");
        config.ProtectedContextTypes.Should().Contain("tool_definition");
    }

    [Fact]
    public void ContextConfig_DefaultMinRecentTurns_ShouldBeFour()
    {
        // Arrange & Act
        var config = new ContextConfig();

        // Assert
        config.MinRecentTurns.Should().Be(4);
    }

    #endregion

    #region Helper Methods

    private static IContextManager CreateContextManager(ContextConfig config)
    {
        var tokenCounter = new SimpleTokenCounter();
        return new ContextManager(tokenCounter, config);
    }

    #endregion
}