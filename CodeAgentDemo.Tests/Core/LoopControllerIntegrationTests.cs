using System.Runtime.CompilerServices;
using System.Text.Json;
using CodeAgentDemo.Cli;
using CodeAgentDemo.Core;
using CodeAgentDemo.Models;
using CodeAgentDemo.Providers;
using CodeAgentDemo.Tools;
using FluentAssertions;
using Moq;
using Xunit;
using CodeAgentDemo.Tests;

namespace CodeAgentDemo.Tests.Core;

public class LoopControllerIntegrationTests
{
    #region Max Iteration Tests

    [Fact]
    public async Task AgentLoop_ShouldStop_WhenMaxIterationsReached()
    {
        var providerMock = CreateProviderMockWithToolCalls(20);
        var consoleMock = new Mock<IConsoleIO>();
        var config = new LoopControlConfig { MaxIterations = 15, CycleDetectionWindow = 100 };
        var loopManager = TestHelper.CreateLoopManager(config);

        var loop = TestHelper.CreateAgentLoop(
            provider: providerMock.Object,
            console: consoleMock.Object,
            loopManager: loopManager
        );

        var result = await loop.SendMessageAsync("Test message");

        result.StopReason.Should().Be("iteration_limit");
    }

    [Fact]
    public async Task AgentLoop_ShouldStop_WhenCustomMaxIterationsReached()
    {
        var providerMock = CreateProviderMockWithToolCalls(10);
        var consoleMock = new Mock<IConsoleIO>();
        var config = new LoopControlConfig { MaxIterations = 3, CycleDetectionWindow = 100 };
        var loopManager = TestHelper.CreateLoopManager(config);

        var loop = TestHelper.CreateAgentLoop(
            provider: providerMock.Object,
            console: consoleMock.Object,
            loopManager: loopManager
        );

        var result = await loop.SendMessageAsync("Test message");

        result.StopReason.Should().Be("iteration_limit");
    }

    #endregion

    #region Cycle Detection Tests

    [Fact]
    public async Task AgentLoop_ShouldStop_WhenCycleDetected()
    {
        var providerMock = new Mock<IChatProvider>();
        providerMock.SetupGet(p => p.ProviderName).Returns("Test");

        var consoleMock = new Mock<IConsoleIO>();
        var config = new LoopControlConfig { MaxIterations = 100, CycleDetectionWindow = 3 };
        var loopManager = TestHelper.CreateLoopManager(config);

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

        var loop = TestHelper.CreateAgentLoop(
            provider: providerMock.Object,
            console: consoleMock.Object,
            loopManager: loopManager
        );

        var result = await loop.SendMessageAsync("Test message");

        result.StopReason.Should().Be("cycle_detected");
    }

    [Fact]
    public async Task AgentLoop_ShouldNotDetectCycle_WhenToolCallsAreDifferent()
    {
        var providerMock = new Mock<IChatProvider>();
        providerMock.SetupGet(p => p.ProviderName).Returns("Test");

        var consoleMock = new Mock<IConsoleIO>();
        var config = new LoopControlConfig { MaxIterations = 5, CycleDetectionWindow = 10 };
        var loopManager = TestHelper.CreateLoopManager(config);

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
                        new("Done", null, null, "end_turn", null)
                    }.ToAsyncEnumerable();
                }
                return new List<StreamChunk>
                {
                    new(null, null, new ToolCallDelta($"call_{callCount}", "test_tool", null), "tool_use", null),
                    new(null, null, new ToolCallDelta(null, null, $"{{\"arg\":\"value_{callCount}\"}}"), null, null)
                }.ToAsyncEnumerable();
            });

        var loop = TestHelper.CreateAgentLoop(
            provider: providerMock.Object,
            console: consoleMock.Object,
            loopManager: loopManager
        );

        var result = await loop.SendMessageAsync("Test message");

        result.StopReason.Should().Be("end_turn");
    }

    #endregion

    #region Token Limit Tests

    [Fact]
    public async Task AgentLoop_ShouldStop_WhenTokenLimitReached()
    {
        var providerMock = new Mock<IChatProvider>();
        providerMock.SetupGet(p => p.ProviderName).Returns("Test");

        var consoleMock = new Mock<IConsoleIO>();
        var config = new LoopControlConfig { MaxIterations = 100, TokenLimitRatio = 0.8 };
        var loopManager = TestHelper.CreateLoopManager(config, maxTokens: 1000);

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
                    new(null, null, new ToolCallDelta($"call_{callCount}", "test_tool", null), "tool_use", new UsageInfo(450, 450)),
                    new(null, null, new ToolCallDelta(null, null, "{}"), null, null)
                }.ToAsyncEnumerable();
            });

        var loop = TestHelper.CreateAgentLoop(
            provider: providerMock.Object,
            console: consoleMock.Object,
            loopManager: loopManager
        );

        var result = await loop.SendMessageAsync("Test message");

        result.StopReason.Should().Be("token_limit");
    }

    [Fact]
    public async Task AgentLoop_ShouldContinue_WhenTokenUsageBelowThreshold()
    {
        var providerMock = new Mock<IChatProvider>();
        providerMock.SetupGet(p => p.ProviderName).Returns("Test");

        var consoleMock = new Mock<IConsoleIO>();
        var config = new LoopControlConfig { MaxIterations = 5, TokenLimitRatio = 0.8, CycleDetectionWindow = 100 };
        var loopManager = TestHelper.CreateLoopManager(config, maxTokens: 10000);

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

        var loop = TestHelper.CreateAgentLoop(
            provider: providerMock.Object,
            console: consoleMock.Object,
            loopManager: loopManager
        );

        var result = await loop.SendMessageAsync("Test message");

        result.StopReason.Should().Be("end_turn");
    }

    #endregion

    #region Normal Flow Tests

    [Fact]
    public async Task AgentLoop_ShouldCompleteNormally_WithoutLoopController()
    {
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

        var loop = TestHelper.CreateAgentLoop(provider: providerMock.Object);

        var result = await loop.SendMessageAsync("Hello");

        result.StopReason.Should().Be("end_turn");
        result.Content.Should().HaveCount(1);
    }

    [Fact]
    public async Task AgentLoop_ShouldCompleteNormally_WithLoopController_WhenNoLimitsHit()
    {
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

        var config = new LoopControlConfig { MaxIterations = 100, TokenLimitRatio = 0.9, CycleDetectionWindow = 20 };
        var loopManager = TestHelper.CreateLoopManager(config, maxTokens: 100000);

        var loop = TestHelper.CreateAgentLoop(
            provider: providerMock.Object,
            loopManager: loopManager
        );

        var result = await loop.SendMessageAsync("Hello");

        result.StopReason.Should().Be("end_turn");
        result.Content.Should().HaveCount(1);
    }

    [Fact]
    public async Task AgentLoop_ShouldHandleMultipleToolCalls_WithoutHittingLimits()
    {
        var providerMock = new Mock<IChatProvider>();
        providerMock.SetupGet(p => p.ProviderName).Returns("Test");

        var config = new LoopControlConfig { MaxIterations = 20, TokenLimitRatio = 0.9, CycleDetectionWindow = 20 };
        var loopManager = TestHelper.CreateLoopManager(config, maxTokens: 100000);

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

        var loop = TestHelper.CreateAgentLoop(
            provider: providerMock.Object,
            loopManager: loopManager
        );

        var result = await loop.SendMessageAsync("Do something");

        result.StopReason.Should().Be("end_turn");
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
                    return new List<StreamChunk>
                    {
                        new("Done", null, null, "end_turn", null)
                    }.ToAsyncEnumerable();
                }
                return new List<StreamChunk>
                {
                    new(null, null, new ToolCallDelta($"call_{callCount}", "test_tool", null), "tool_use", null),
                    new(null, null, new ToolCallDelta(null, null, $"{{\"iteration\":{callCount}}}"), null, null)
                }.ToAsyncEnumerable();
            });

        return providerMock;
    }

    #endregion
}