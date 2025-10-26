using System.Runtime.CompilerServices;
using System.Text.Json;
using CodeAgentDemo.Core;
using CodeAgentDemo.Models;
using CodeAgentDemo.Providers;
using CodeAgentDemo.Tools;
using FluentAssertions;
using Moq;
using Xunit;
using CodeAgentDemo.Tests;

namespace CodeAgentDemo.Tests.Core;

/// <summary>
/// Integration tests for LoopController with AgentLoop.
/// Tests end-to-end scenarios using real LoopController instances.
/// </summary>
public class LoopControllerIntegrationTests
{
    #region Max Iteration Tests

    [Fact]
    public async Task AgentLoop_ShouldStop_WhenMaxIterationsReached()
    {
        // Arrange
        var providerMock = CreateProviderMockWithToolCalls(20);
        var tools = CreateToolRegistry();
        var options = new ChatOptions();
        var consoleMock = new Mock<IConsoleIO>();

        // Create real LoopController with max 15 iterations and large cycle detection window
        var config = new LoopControlConfig { MaxIterations = 15, CycleDetectionWindow = 100 };
        var loopController = new LoopController(config);

        var loop = new AgentLoop(
            providerMock.Object, tools, options, consoleMock.Object,
            null, null, null, null, loopController, null, null);

        // Act
        var result = await loop.SendMessageAsync("Test message");

        // Assert
        result.StopReason.Should().Be("loop_control_stop");
        loopController.State.IterationLimitReached.Should().BeTrue();
        loopController.State.IterationCount.Should().Be(15);
    }

    [Fact]
    public async Task AgentLoop_ShouldStop_WhenCustomMaxIterationsReached()
    {
        // Arrange
        var providerMock = CreateProviderMockWithToolCalls(10);
        var tools = CreateToolRegistry();
        var options = new ChatOptions();
        var consoleMock = new Mock<IConsoleIO>();

        // Create real LoopController with custom max 3 iterations and large cycle detection window
        var config = new LoopControlConfig { MaxIterations = 3, CycleDetectionWindow = 100 };
        var loopController = new LoopController(config);

        var loop = new AgentLoop(
            providerMock.Object, tools, options, consoleMock.Object,
            null, null, null, null, loopController, null, null);

        // Act
        var result = await loop.SendMessageAsync("Test message");

        // Assert
        result.StopReason.Should().Be("loop_control_stop");
        loopController.State.IterationLimitReached.Should().BeTrue();
        loopController.State.IterationCount.Should().Be(3);
    }

    #endregion

    #region Cycle Detection Tests

    [Fact]
    public async Task AgentLoop_ShouldStop_WhenCycleDetected()
    {
        // Arrange
        var providerMock = new Mock<IChatProvider>();
        providerMock.SetupGet(p => p.ProviderName).Returns("Test");

        var tools = CreateToolRegistry();
        var options = new ChatOptions();
        var consoleMock = new Mock<IConsoleIO>();

        // Create real LoopController with small cycle detection window
        var config = new LoopControlConfig
        {
            MaxIterations = 100,
            CycleDetectionWindow = 3
        };
        var loopController = new LoopController(config);

        // Setup provider to return same tool call repeatedly
        var callCount = 0;
        providerMock
            .Setup(p => p.CompleteStreamingAsync(
                It.IsAny<IReadOnlyList<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                return new List<StreamChunk>
                {
                    new(null, null, new ToolCallDelta($"call_{callCount}", "test_tool", null), "tool_use", null),
                    new(null, null, new ToolCallDelta(null, null, "{\"arg\":\"same_value\"}"), null, null)
                }.ToAsyncEnumerable();
            });

        var loop = new AgentLoop(
            providerMock.Object, tools, options, consoleMock.Object,
            null, null, null, null, loopController, null, null);

        // Act
        var result = await loop.SendMessageAsync("Test message");

        // Assert
        result.StopReason.Should().Be("cycle_detected");
        loopController.State.DetectedCycle.Should().BeTrue();
    }

    [Fact]
    public async Task AgentLoop_ShouldNotDetectCycle_WhenToolCallsAreDifferent()
    {
        // Arrange
        var providerMock = new Mock<IChatProvider>();
        providerMock.SetupGet(p => p.ProviderName).Returns("Test");

        var tools = CreateToolRegistry();
        var options = new ChatOptions();
        var consoleMock = new Mock<IConsoleIO>();

        // Create real LoopController
        var config = new LoopControlConfig
        {
            MaxIterations = 5,
            CycleDetectionWindow = 10
        };
        var loopController = new LoopController(config);

        // Setup provider to return different tool calls
        var callCount = 0;
        providerMock
            .Setup(p => p.CompleteStreamingAsync(
                It.IsAny<IReadOnlyList<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount >= 5)
                {
                    // End after 5 iterations
                    return new List<StreamChunk>
                    {
                        new("Done", null, null, "end_turn", null)
                    }.ToAsyncEnumerable();
                }
                return new List<StreamChunk>
                {
                    new(null, null, new ToolCallDelta($"call_{callCount}", "test_tool", null), "tool_use", null),
                    new(null, null, new ToolCallDelta(null, null, $"{{\"arg\":\"value_{callCount}\"}}"), null, null)
                }.ToAsyncEnumerable();
            });

        var loop = new AgentLoop(
            providerMock.Object, tools, options, consoleMock.Object,
            null, null, null, null, loopController, null, null);

        // Act
        var result = await loop.SendMessageAsync("Test message");

        // Assert - Should complete normally without cycle detection
        result.StopReason.Should().Be("end_turn");
        loopController.State.DetectedCycle.Should().BeFalse();
    }

    #endregion

    #region Token Limit Tests

    [Fact]
    public async Task AgentLoop_ShouldStop_WhenTokenLimitReached()
    {
        // Arrange
        var providerMock = new Mock<IChatProvider>();
        providerMock.SetupGet(p => p.ProviderName).Returns("Test");

        var tools = CreateToolRegistry();
        var options = new ChatOptions();
        var consoleMock = new Mock<IConsoleIO>();

        // Create real LoopController with token limit
        var config = new LoopControlConfig
        {
            MaxIterations = 100,
            TokenLimitRatio = 0.8 // 80% threshold
        };
        var maxTokens = 1000;
        var loopController = new LoopController(config, maxTokens);

        // Setup provider to return responses with high token usage
        var callCount = 0;
        providerMock
            .Setup(p => p.CompleteStreamingAsync(
                It.IsAny<IReadOnlyList<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                // Return high token usage to trigger limit
                return new List<StreamChunk>
                {
                    new(null, null, new ToolCallDelta($"call_{callCount}", "test_tool", null), "tool_use", new UsageInfo(450, 450)),
                    new(null, null, new ToolCallDelta(null, null, "{}"), null, null)
                }.ToAsyncEnumerable();
            });

        var loop = new AgentLoop(
            providerMock.Object, tools, options, consoleMock.Object,
            null, null, null, null, loopController, null, null);

        // Act
        var result = await loop.SendMessageAsync("Test message");

        // Assert
        result.StopReason.Should().Be("loop_control_stop");
        loopController.State.TokenLimitReached.Should().BeTrue();
    }

    [Fact]
    public async Task AgentLoop_ShouldContinue_WhenTokenUsageBelowThreshold()
    {
        // Arrange
        var providerMock = new Mock<IChatProvider>();
        providerMock.SetupGet(p => p.ProviderName).Returns("Test");

        var tools = CreateToolRegistry();
        var options = new ChatOptions();
        var consoleMock = new Mock<IConsoleIO>();

        // Create real LoopController with token limit
        var config = new LoopControlConfig
        {
            MaxIterations = 5,
            TokenLimitRatio = 0.8,
            CycleDetectionWindow = 100
        };
        var maxTokens = 10000; // High limit
        var loopController = new LoopController(config, maxTokens);

        // Setup provider to return responses with low token usage
        var callCount = 0;
        providerMock
            .Setup(p => p.CompleteStreamingAsync(
                It.IsAny<IReadOnlyList<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount >= 3)
                {
                    return new List<StreamChunk>
                    {
                        new("Done", null, null, "end_turn", new UsageInfo(50, 50))
                    }.ToAsyncEnumerable();
                }
                return new List<StreamChunk>
                {
                    new(null, null, new ToolCallDelta($"call_{callCount}", "test_tool", null), "tool_use", new UsageInfo(50, 50)),
                    new(null, null, new ToolCallDelta(null, null, $"{{\"step\":{callCount}}}"), null, null)
                }.ToAsyncEnumerable();
            });

        var loop = new AgentLoop(
            providerMock.Object, tools, options, consoleMock.Object,
            null, null, null, null, loopController, null, null);

        // Act
        var result = await loop.SendMessageAsync("Test message");

        // Assert - Should complete normally
        result.StopReason.Should().Be("end_turn");
        loopController.State.TokenLimitReached.Should().BeFalse();
    }

    #endregion

    #region Normal Flow Tests

    [Fact]
    public async Task AgentLoop_ShouldCompleteNormally_WithoutLoopController()
    {
        // Arrange
        var providerMock = new Mock<IChatProvider>();
        providerMock.SetupGet(p => p.ProviderName).Returns("Test");
        providerMock
            .Setup(p => p.CompleteStreamingAsync(
                It.IsAny<IReadOnlyList<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(new List<StreamChunk>
            {
                new("Hello response", null, null, "end_turn", null)
            }.ToAsyncEnumerable());

        var tools = new ToolRegistry();
        var options = new ChatOptions();
        var consoleMock = new Mock<IConsoleIO>();

        // Create AgentLoop without LoopController
        var loop = new AgentLoop(
            providerMock.Object, tools, options, consoleMock.Object,
            null, null, null, null, null, null, null);

        // Act
        var result = await loop.SendMessageAsync("Hello");

        // Assert
        result.StopReason.Should().Be("end_turn");
        result.Content.Should().HaveCount(1);
    }

    [Fact]
    public async Task AgentLoop_ShouldCompleteNormally_WithLoopController_WhenNoLimitsHit()
    {
        // Arrange
        var providerMock = new Mock<IChatProvider>();
        providerMock.SetupGet(p => p.ProviderName).Returns("Test");
        providerMock
            .Setup(p => p.CompleteStreamingAsync(
                It.IsAny<IReadOnlyList<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(new List<StreamChunk>
            {
                new("Hello response", null, null, "end_turn", new UsageInfo(100, 50))
            }.ToAsyncEnumerable());

        var tools = new ToolRegistry();
        var options = new ChatOptions();
        var consoleMock = new Mock<IConsoleIO>();

        // Create real LoopController with generous limits
        var config = new LoopControlConfig
        {
            MaxIterations = 100,
            TokenLimitRatio = 0.9,
            CycleDetectionWindow = 20
        };
        var loopController = new LoopController(config, 100000);

        var loop = new AgentLoop(
            providerMock.Object, tools, options, consoleMock.Object,
            null, null, null, null, loopController, null, null);

        // Act
        var result = await loop.SendMessageAsync("Hello");

        // Assert
        result.StopReason.Should().Be("end_turn");
        result.Content.Should().HaveCount(1);
        loopController.State.IterationLimitReached.Should().BeFalse();
        loopController.State.TokenLimitReached.Should().BeFalse();
        loopController.State.DetectedCycle.Should().BeFalse();
    }

    [Fact]
    public async Task AgentLoop_ShouldHandleMultipleToolCalls_WithoutHittingLimits()
    {
        // Arrange
        var providerMock = new Mock<IChatProvider>();
        providerMock.SetupGet(p => p.ProviderName).Returns("Test");

        var tools = CreateToolRegistry();
        var options = new ChatOptions();
        var consoleMock = new Mock<IConsoleIO>();

        // Create real LoopController with generous limits
        var config = new LoopControlConfig
        {
            MaxIterations = 20,
            TokenLimitRatio = 0.9,
            CycleDetectionWindow = 20
        };
        var loopController = new LoopController(config, 100000);

        // Setup provider to return 5 tool calls then end
        var callCount = 0;
        providerMock
            .Setup(p => p.CompleteStreamingAsync(
                It.IsAny<IReadOnlyList<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount >= 5)
                {
                    return new List<StreamChunk>
                    {
                        new("All done", null, null, "end_turn", new UsageInfo(100, 50))
                    }.ToAsyncEnumerable();
                }
                return new List<StreamChunk>
                {
                    new(null, null, new ToolCallDelta($"call_{callCount}", "test_tool", null), "tool_use", new UsageInfo(100, 50)),
                    new(null, null, new ToolCallDelta(null, null, $"{{\"step\":{callCount}}}"), null, null)
                }.ToAsyncEnumerable();
            });

        var loop = new AgentLoop(
            providerMock.Object, tools, options, consoleMock.Object,
            null, null, null, null, loopController, null, null);

        // Act
        var result = await loop.SendMessageAsync("Do something");

        // Assert
        result.StopReason.Should().Be("end_turn");
        loopController.State.IterationCount.Should().Be(5);
        loopController.State.IterationLimitReached.Should().BeFalse();
        loopController.State.TokenLimitReached.Should().BeFalse();
        loopController.State.DetectedCycle.Should().BeFalse();
    }

    #endregion

    #region Reset Behavior Tests

    [Fact]
    public async Task LoopController_ShouldReset_OnNewMessage()
    {
        // Arrange
        var providerMock = new Mock<IChatProvider>();
        providerMock.SetupGet(p => p.ProviderName).Returns("Test");
        providerMock
            .Setup(p => p.CompleteStreamingAsync(
                It.IsAny<IReadOnlyList<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(new List<StreamChunk>
            {
                new("Response", null, null, "end_turn", new UsageInfo(100, 50))
            }.ToAsyncEnumerable());

        var tools = new ToolRegistry();
        var options = new ChatOptions();
        var consoleMock = new Mock<IConsoleIO>();

        var config = new LoopControlConfig { MaxIterations = 5 };
        var loopController = new LoopController(config);

        var loop = new AgentLoop(
            providerMock.Object, tools, options, consoleMock.Object,
            null, null, null, null, loopController, null, null);

        // Act - First message
        await loop.SendMessageAsync("First message");
        var firstIterationCount = loopController.State.IterationCount;

        // Act - Second message
        await loop.SendMessageAsync("Second message");
        var secondIterationCount = loopController.State.IterationCount;

        // Assert - State should be reset between messages
        firstIterationCount.Should().Be(1);
        secondIterationCount.Should().Be(1);
    }

    #endregion

    #region Helper Methods

    private static Mock<IChatProvider> CreateProviderMockWithToolCalls(int maxCalls)
    {
        var providerMock = new Mock<IChatProvider>();
        providerMock.SetupGet(p => p.ProviderName).Returns("Test");

        var callCount = 0;
        providerMock
            .Setup(p => p.CompleteStreamingAsync(
                It.IsAny<IReadOnlyList<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount >= maxCalls)
                {
                    // End after maxCalls
                    return new List<StreamChunk>
                    {
                        new("Done", null, null, "end_turn", null)
                    }.ToAsyncEnumerable();
                }
                // Use different arguments for each call to avoid cycle detection
                return new List<StreamChunk>
                {
                    new(null, null, new ToolCallDelta($"call_{callCount}", "test_tool", null), "tool_use", null),
                    new(null, null, new ToolCallDelta(null, null, $"{{\"iteration\":{callCount}}}"), null, null)
                }.ToAsyncEnumerable();
            });

        return providerMock;
    }

    private static ToolRegistry CreateToolRegistry()
    {
        var tools = new ToolRegistry();
        var toolMock = new Mock<ITool>();
        toolMock.SetupGet(t => t.Name).Returns("test_tool");
        toolMock.SetupGet(t => t.Description).Returns("Test tool for integration tests");
        toolMock.SetupGet(t => t.InputSchema).Returns(JsonDocument.Parse("{\"type\":\"object\"}").RootElement);
        toolMock.Setup(t => t.ExecuteAsync(It.IsAny<JsonElement>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResult(true, "Tool executed successfully"));
        toolMock.Setup(t => t.RequiresConfirmation(It.IsAny<JsonElement>()))
            .Returns(false);
        tools.Register(toolMock.Object);
        return tools;
    }

    #endregion
}