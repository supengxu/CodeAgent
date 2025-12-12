using System.Text.Json;
using CodeAgentDemo.Cli;
using CodeAgentDemo.Core;
using CodeAgentDemo.Models;
using CodeAgentDemo.Providers;
using CodeAgentDemo.Services;
using CodeAgentDemo.Tools;
using FluentAssertions;
using Moq;
using Xunit;
using CodeAgentDemo.Tests;

namespace CodeAgentDemo.Tests.Core;

/// <summary>
/// Integration tests for ContextManager with AgentLoop.
/// Tests end-to-end scenarios including compression triggers and context preservation.
/// </summary>
public class ContextManagerIntegrationTests
{
    #region Long Conversation Compression Tests

    [Fact]
    public async Task LongConversation_ShouldTriggerCompression_WhenThresholdExceeded()
    {
        // Arrange
        var config = new ContextConfig
        {
            CompressionThreshold = 100, // Low threshold to trigger compression
            MinRecentTurns = 4
        };
        var tokenCounter = new SimpleTokenCounter();
        var contextManager = new ContextManager(tokenCounter, config);

        // Create a long conversation that exceeds threshold
        var messages = new List<ChatMessage>
        {
            ChatMessage.CreateText(ChatRole.System, "System prompt"),
            ChatMessage.CreateText(ChatRole.User, new string('a', 500)), // Long message
            ChatMessage.CreateText(ChatRole.Assistant, new string('b', 500)),
            ChatMessage.CreateText(ChatRole.User, new string('c', 500)),
            ChatMessage.CreateText(ChatRole.Assistant, new string('d', 500)),
            ChatMessage.CreateText(ChatRole.User, "Recent message 1"),
            ChatMessage.CreateText(ChatRole.Assistant, "Recent response 1"),
            ChatMessage.CreateText(ChatRole.User, "Recent message 2"),
            ChatMessage.CreateText(ChatRole.Assistant, "Recent response 2")
        };

        // Act
        var shouldCompress = contextManager.ShouldCompress(messages);
        var result = await contextManager.CompressAsync(messages);

        // Assert
        shouldCompress.Should().BeTrue("conversation exceeds threshold");
        result.Success.Should().BeTrue();
        result.RemovedTokens.Should().BeGreaterThan(0, "some tokens should be removed");
    }

    [Fact]
    public async Task LongConversation_CompressionShouldReduceTokenCount()
    {
        // Arrange
        var config = new ContextConfig
        {
            CompressionThreshold = 50,
            MinRecentTurns = 2
        };
        var tokenCounter = new SimpleTokenCounter();
        var contextManager = new ContextManager(tokenCounter, config);

        var messages = new List<ChatMessage>
        {
            ChatMessage.CreateText(ChatRole.User, new string('x', 1000)),
            ChatMessage.CreateText(ChatRole.Assistant, new string('y', 1000)),
            ChatMessage.CreateText(ChatRole.User, "Keep this"),
            ChatMessage.CreateText(ChatRole.Assistant, "Keep this too")
        };

        // Act
        var originalTokenCount = contextManager.GetTokenCount(messages);
        var result = await contextManager.CompressAsync(messages);
        var compressedTokenCount = contextManager.GetTokenCount(result.CompressedMessages);

        // Assert
        compressedTokenCount.Should().BeLessThan(originalTokenCount, "compression should reduce tokens");
        result.RemovedTokens.Should().Be(originalTokenCount - compressedTokenCount);
    }

    #endregion

    #region System Prompt Protection Tests

    [Fact]
    public async Task Compression_ShouldPreserveSystemPrompt()
    {
        // Arrange
        var config = new ContextConfig
        {
            CompressionThreshold = 10,
            MinRecentTurns = 2
        };
        var tokenCounter = new SimpleTokenCounter();
        var contextManager = new ContextManager(tokenCounter, config);

        var systemPrompt = "You are a helpful assistant. Follow these rules carefully.";
        var messages = new List<ChatMessage>
        {
            ChatMessage.CreateText(ChatRole.System, systemPrompt),
            ChatMessage.CreateText(ChatRole.User, new string('a', 1000)),
            ChatMessage.CreateText(ChatRole.Assistant, new string('b', 1000)),
            ChatMessage.CreateText(ChatRole.User, "Recent"),
            ChatMessage.CreateText(ChatRole.Assistant, "Response")
        };

        // Act
        var result = await contextManager.CompressAsync(messages);

        // Assert
        result.Success.Should().BeTrue();
        result.CompressedMessages.Should().Contain(m => m.Role == ChatRole.System,
            "system prompt should be preserved");

        var systemMessage = result.CompressedMessages.FirstOrDefault(m => m.Role == ChatRole.System);
        systemMessage.Should().NotBeNull();
        var textBlock = systemMessage!.Content.OfType<TextBlock>().FirstOrDefault();
        textBlock?.Text.Should().Be(systemPrompt, "system prompt content should be unchanged");
    }

    [Fact]
    public async Task Compression_ShouldPreserveMultipleSystemMessages()
    {
        // Arrange
        var config = new ContextConfig
        {
            CompressionThreshold = 10,
            MinRecentTurns = 2
        };
        var tokenCounter = new SimpleTokenCounter();
        var contextManager = new ContextManager(tokenCounter, config);

        var messages = new List<ChatMessage>
        {
            ChatMessage.CreateText(ChatRole.System, "System prompt 1"),
            ChatMessage.CreateText(ChatRole.System, "System prompt 2"),
            ChatMessage.CreateText(ChatRole.User, new string('a', 1000)),
            ChatMessage.CreateText(ChatRole.Assistant, new string('b', 1000)),
            ChatMessage.CreateText(ChatRole.User, "Recent"),
            ChatMessage.CreateText(ChatRole.Assistant, "Response")
        };

        // Act
        var result = await contextManager.CompressAsync(messages);

        // Assert
        result.Success.Should().BeTrue();
        result.CompressedMessages.Count(m => m.Role == ChatRole.System).Should().Be(2,
            "all system messages should be preserved");
    }

    #endregion

    #region Recent Turns Preservation Tests

    [Fact]
    public async Task Compression_ShouldPreserveMinRecentTurns()
    {
        // Arrange
        var config = new ContextConfig
        {
            CompressionThreshold = 10,
            MinRecentTurns = 4
        };
        var tokenCounter = new SimpleTokenCounter();
        var contextManager = new ContextManager(tokenCounter, config);

        var messages = new List<ChatMessage>
        {
            ChatMessage.CreateText(ChatRole.User, "Old message 1"),
            ChatMessage.CreateText(ChatRole.Assistant, "Old response 1"),
            ChatMessage.CreateText(ChatRole.User, "Old message 2"),
            ChatMessage.CreateText(ChatRole.Assistant, "Old response 2"),
            ChatMessage.CreateText(ChatRole.User, "Recent message 1"),
            ChatMessage.CreateText(ChatRole.Assistant, "Recent response 1"),
            ChatMessage.CreateText(ChatRole.User, "Recent message 2"),
            ChatMessage.CreateText(ChatRole.Assistant, "Recent response 2")
        };

        // Act
        var result = await contextManager.CompressAsync(messages);

        // Assert
        result.Success.Should().BeTrue();
        result.CompressedMessages.Should().HaveCount(4, "MinRecentTurns = 4 should preserve 4 messages");

        // Verify the preserved messages are the most recent ones
        var recentMessages = messages.TakeLast(4).ToList();
        result.CompressedMessages.Should().BeEquivalentTo(recentMessages,
            opts => opts.WithStrictOrdering(),
            "the most recent messages should be preserved");
    }

    [Fact]
    public async Task Compression_ShouldPreserveRecentTurnsWithSystemMessages()
    {
        // Arrange
        var config = new ContextConfig
        {
            CompressionThreshold = 10,
            MinRecentTurns = 4
        };
        var tokenCounter = new SimpleTokenCounter();
        var contextManager = new ContextManager(tokenCounter, config);

        var messages = new List<ChatMessage>
        {
            ChatMessage.CreateText(ChatRole.System, "System prompt"),
            ChatMessage.CreateText(ChatRole.User, "Old message 1"),
            ChatMessage.CreateText(ChatRole.Assistant, "Old response 1"),
            ChatMessage.CreateText(ChatRole.User, "Old message 2"),
            ChatMessage.CreateText(ChatRole.Assistant, "Old response 2"),
            ChatMessage.CreateText(ChatRole.User, "Recent message 1"),
            ChatMessage.CreateText(ChatRole.Assistant, "Recent response 1"),
            ChatMessage.CreateText(ChatRole.User, "Recent message 2"),
            ChatMessage.CreateText(ChatRole.Assistant, "Recent response 2")
        };

        // Act
        var result = await contextManager.CompressAsync(messages);

        // Assert
        result.Success.Should().BeTrue();

        // Should have 1 system + 4 recent = 5 messages
        result.CompressedMessages.Should().HaveCount(5);
        result.CompressedMessages.Count(m => m.Role == ChatRole.System).Should().Be(1);

        // Verify order is preserved (system first, then recent messages)
        result.CompressedMessages[0].Role.Should().Be(ChatRole.System);
    }

    [Fact]
    public async Task Compression_WhenMessagesLessThanMinRecentTurns_ShouldPreserveAll()
    {
        // Arrange
        var config = new ContextConfig
        {
            CompressionThreshold = 10,
            MinRecentTurns = 10
        };
        var tokenCounter = new SimpleTokenCounter();
        var contextManager = new ContextManager(tokenCounter, config);

        var messages = new List<ChatMessage>
        {
            ChatMessage.CreateText(ChatRole.User, "Message 1"),
            ChatMessage.CreateText(ChatRole.Assistant, "Response 1"),
            ChatMessage.CreateText(ChatRole.User, "Message 2")
        };

        // Act
        var result = await contextManager.CompressAsync(messages);

        // Assert
        result.Success.Should().BeTrue();
        result.CompressedMessages.Should().HaveCount(3, "all messages should be preserved when count < MinRecentTurns");
        result.RemovedTokens.Should().Be(0, "no tokens should be removed");
    }

    #endregion

    #region AgentLoop Integration Tests

    [Fact]
    public async Task AgentLoop_WithRealContextManager_ShouldCompressWhenNeeded()
    {
        // Arrange
        var config = new ContextConfig
        {
            CompressionThreshold = 100,
            MinRecentTurns = 4
        };
        var tokenCounter = new SimpleTokenCounter();
        var contextManager = new ContextManager(tokenCounter, config);

        var providerMock = new Mock<IChatProvider>();
        providerMock.SetupGet(p => p.ProviderName).Returns("Test");
        providerMock
            .Setup(p => p.CompleteStreamingAsync(It.IsAny<IReadOnlyList<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .Returns(new List<StreamChunk> { new(null, null, null, "end_turn", null) }.ToAsyncEnumerable());

        var tools = new ToolRegistry();
        var options = new ChatOptions();
        var consoleMock = new Mock<IConsoleIO>();
        var sessionCli = new SessionCli(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

        // Act
        var loop = new AgentLoop(
            providerMock.Object, tools, options, consoleMock.Object, sessionCli,
            new ConsoleUI(), null, null, null, contextManager);

        // Send multiple messages to build up context
        for (int i = 0; i < 10; i++)
        {
            await loop.SendMessageAsync(new string('x', 100) + i);
        }

        // Assert
        loop.History.Count.Should().BeLessThanOrEqualTo(12, "compression should have occurred");
        // History should have at most MinRecentTurns (4) user messages + 4 assistant responses + possibly system
    }

    [Fact]
    public async Task AgentLoop_CompressionShouldPreserveConversationIntegrity()
    {
        // Arrange
        var config = new ContextConfig
        {
            CompressionThreshold = 50,
            MinRecentTurns = 4
        };
        var tokenCounter = new SimpleTokenCounter();
        var contextManager = new ContextManager(tokenCounter, config);

        var providerMock = new Mock<IChatProvider>();
        providerMock.SetupGet(p => p.ProviderName).Returns("Test");

        var callCount = 0;
        providerMock
            .Setup(p => p.CompleteStreamingAsync(It.IsAny<IReadOnlyList<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                return new List<StreamChunk>
                {
                    new(null, null, null, "end_turn", null)
                }.ToAsyncEnumerable();
            });

        var tools = new ToolRegistry();
        var options = new ChatOptions();
        var consoleMock = new Mock<IConsoleIO>();
        var sessionCli = new SessionCli(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

        // Act
        var loop = new AgentLoop(
            providerMock.Object, tools, options, consoleMock.Object, sessionCli,
            new ConsoleUI(), null, null, null, contextManager);

        // Send messages
        await loop.SendMessageAsync("First message");
        await loop.SendMessageAsync("Second message");
        await loop.SendMessageAsync("Third message");

        // Assert - conversation should still work after potential compression
        loop.History.Should().NotBeEmpty();
        callCount.Should().Be(3, "all messages should have been processed");
    }

    [Fact]
    public async Task AgentLoop_CompressionWithToolCalls_ShouldWorkCorrectly()
    {
        // Arrange
        var config = new ContextConfig
        {
            CompressionThreshold = 100,
            MinRecentTurns = 4
        };
        var tokenCounter = new SimpleTokenCounter();
        var contextManager = new ContextManager(tokenCounter, config);

        var providerMock = new Mock<IChatProvider>();
        providerMock.SetupGet(p => p.ProviderName).Returns("Test");

        var tools = new ToolRegistry();
        var toolMock = new Mock<ITool>();
        toolMock.SetupGet(t => t.Name).Returns("test_tool");
        toolMock.SetupGet(t => t.Description).Returns("Test tool");
        toolMock.SetupGet(t => t.InputSchema).Returns(JsonDocument.Parse("{}").RootElement);
        toolMock.Setup(t => t.ExecuteAsync(It.IsAny<JsonElement>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResult(true, "Success"));
        toolMock.Setup(t => t.RequiresConfirmation(It.IsAny<JsonElement>()))
            .Returns(false);
        tools.Register(toolMock.Object);

        var options = new ChatOptions();
        var consoleMock = new Mock<IConsoleIO>();

        // Setup provider to return tool call then normal response
        var callCount = 0;
        providerMock
            .Setup(p => p.CompleteStreamingAsync(It.IsAny<IReadOnlyList<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return new List<StreamChunk>
                    {
                        new(null, null, new ToolCallDelta("call_1", "test_tool", null), "tool_use", null),
                        new(null, null, new ToolCallDelta(null, null, "{}"), null, null)
                    }.ToAsyncEnumerable();
                }
                return new List<StreamChunk>
                {
                    new(null, null, null, "end_turn", null)
                }.ToAsyncEnumerable();
            });

        var sessionCli = new SessionCli(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

        // Act
        var loop = new AgentLoop(
            providerMock.Object, tools, options, consoleMock.Object, sessionCli,
            new ConsoleUI(), null, null, null, contextManager);

        var result = await loop.SendMessageAsync("Use the tool");

        // Assert
        result.StopReason.Should().Be("end_turn");
        loop.History.Should().Contain(m => m.Role == ChatRole.Tool,
            "tool result should be in history");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Compression_WithOnlySystemMessages_ShouldPreserveAll()
    {
        // Arrange
        var config = new ContextConfig
        {
            CompressionThreshold = 10,
            MinRecentTurns = 4
        };
        var tokenCounter = new SimpleTokenCounter();
        var contextManager = new ContextManager(tokenCounter, config);

        var messages = new List<ChatMessage>
        {
            ChatMessage.CreateText(ChatRole.System, "System prompt 1"),
            ChatMessage.CreateText(ChatRole.System, "System prompt 2")
        };

        // Act
        var result = await contextManager.CompressAsync(messages);

        // Assert
        result.Success.Should().BeTrue();
        result.CompressedMessages.Should().HaveCount(2);
        result.RemovedTokens.Should().Be(0);
    }

    [Fact]
    public async Task Compression_WithMixedRoles_ShouldPreserveCorrectMessages()
    {
        // Arrange
        var config = new ContextConfig
        {
            CompressionThreshold = 10,
            MinRecentTurns = 3
        };
        var tokenCounter = new SimpleTokenCounter();
        var contextManager = new ContextManager(tokenCounter, config);

        var messages = new List<ChatMessage>
        {
            ChatMessage.CreateText(ChatRole.System, "System"),
            ChatMessage.CreateText(ChatRole.User, "User 1"),
            ChatMessage.CreateText(ChatRole.Assistant, "Assistant 1"),
            ChatMessage.CreateText(ChatRole.User, "User 2"),
            ChatMessage.CreateText(ChatRole.Assistant, "Assistant 2"),
            ChatMessage.CreateText(ChatRole.User, "User 3"),
            ChatMessage.CreateText(ChatRole.Assistant, "Assistant 3")
        };

        // Act
        var result = await contextManager.CompressAsync(messages);

        // Assert
        result.Success.Should().BeTrue();

        // Should have: System + last 3 messages (User 3, Assistant 3, and one more from MinRecentTurns)
        result.CompressedMessages.Should().Contain(m => m.Role == ChatRole.System);
        result.CompressedMessages.Should().Contain(m => m.Content.OfType<TextBlock>().Any(b => b.Text == "User 3"));
        result.CompressedMessages.Should().Contain(m => m.Content.OfType<TextBlock>().Any(b => b.Text == "Assistant 3"));
    }

    [Fact]
    public void ShouldCompress_WithExactThreshold_ShouldReturnFalse()
    {
        // Arrange
        var config = new ContextConfig
        {
            CompressionThreshold = 100
        };
        var tokenCounter = new SimpleTokenCounter();
        var contextManager = new ContextManager(tokenCounter, config);

        // Act - use provided token count exactly at threshold
        var result = contextManager.ShouldCompress(Array.Empty<ChatMessage>(), currentTokenCount: 100);

        // Assert - should NOT compress when exactly at threshold (only when > threshold)
        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldCompress_WithOneOverThreshold_ShouldReturnTrue()
    {
        // Arrange
        var config = new ContextConfig
        {
            CompressionThreshold = 100
        };
        var tokenCounter = new SimpleTokenCounter();
        var contextManager = new ContextManager(tokenCounter, config);

        // Act - use provided token count one over threshold
        var result = contextManager.ShouldCompress(Array.Empty<ChatMessage>(), currentTokenCount: 101);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region SessionStore Integration

    [Fact]
    public async Task SessionStore_ReplaceMessages_ShouldWorkWithCompressionResult()
    {
        // Arrange
        var config = new ContextConfig
        {
            CompressionThreshold = 10,
            MinRecentTurns = 2
        };
        var tokenCounter = new SimpleTokenCounter();
        var contextManager = new ContextManager(tokenCounter, config);
        var session = new SessionStore();

        // Add many messages
        for (int i = 0; i < 10; i++)
        {
            session.AddMessage(ChatMessage.CreateText(ChatRole.User, $"Message {i}"));
        }

        // Act
        var result = await contextManager.CompressAsync(session.Messages);
        session.ReplaceMessages(result.CompressedMessages);

        // Assert
        session.Messages.Should().HaveCount(2, "only MinRecentTurns messages should remain");
    }

    [Fact]
    public async Task Compression_WithSessionStoreIntegration_ShouldMaintainOrder()
    {
        // Arrange
        var config = new ContextConfig
        {
            CompressionThreshold = 10,
            MinRecentTurns = 4
        };
        var tokenCounter = new SimpleTokenCounter();
        var contextManager = new ContextManager(tokenCounter, config);
        var session = new SessionStore();

        session.AddMessage(ChatMessage.CreateText(ChatRole.System, "System"));
        session.AddMessage(ChatMessage.CreateText(ChatRole.User, "User 1"));
        session.AddMessage(ChatMessage.CreateText(ChatRole.Assistant, "Assistant 1"));
        session.AddMessage(ChatMessage.CreateText(ChatRole.User, "User 2"));
        session.AddMessage(ChatMessage.CreateText(ChatRole.Assistant, "Assistant 2"));
        session.AddMessage(ChatMessage.CreateText(ChatRole.User, "User 3"));
        session.AddMessage(ChatMessage.CreateText(ChatRole.Assistant, "Assistant 3"));

        // Act
        var result = await contextManager.CompressAsync(session.Messages);
        session.ReplaceMessages(result.CompressedMessages);

        // Assert - order should be preserved
        session.Messages[0].Role.Should().Be(ChatRole.System);

        // Verify System message is first, followed by non-system messages
        var systemMessages = session.Messages.Where(m => m.Role == ChatRole.System).ToList();
        var nonSystemMessages = session.Messages.Where(m => m.Role != ChatRole.System).ToList();

        // All system messages should come before non-system messages
        if (systemMessages.Count > 0 && nonSystemMessages.Count > 0)
        {
            var lastSystemIndex = session.Messages.ToList().FindLastIndex(m => m.Role == ChatRole.System);
            var firstNonSystemIndex = session.Messages.ToList().FindIndex(m => m.Role != ChatRole.System);
            lastSystemIndex.Should().BeLessThan(firstNonSystemIndex, "system messages should come before non-system messages");
        }
    }

    #endregion
}