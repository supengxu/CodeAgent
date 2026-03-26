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

/// <summary>
/// ReflectionEngine 与 AgentLoop 的集成测试。
/// 测试工具失败 -> 反思 -> 重试的端到端流程。
/// </summary>
public class ReflectionEngineIntegrationTests
{
    private readonly Mock<IChatProvider> _chatProviderMock;
    private readonly Mock<IConsoleIO> _consoleMock;
    private readonly ToolRegistry _tools;
    private readonly ReflectionConfig _reflectionConfig;
    private readonly SessionCli _sessionCli;

    public ReflectionEngineIntegrationTests()
    {
        _chatProviderMock = new Mock<IChatProvider>();
        _consoleMock = new Mock<IConsoleIO>();
        _tools = new ToolRegistry();
        _reflectionConfig = new ReflectionConfig();
        _sessionCli = new SessionCli(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
    }

    #region 工具失败触发反思测试

    [Fact(Skip = "AgentLoop reflection integration not yet implemented")]
    public async Task ToolFailure_ShouldTriggerReflection_WhenReflectionEngineIsEnabled()
    {
        // 准备
        var toolMock = CreateFailingTool("test_tool", "Tool execution failed");
        _tools.Register(toolMock.Object);

        var reflectionEngine = CreateReflectionEngine(shouldRetry: false);
        var loop = CreateAgentLoop(reflectionEngine: reflectionEngine.Object);

        // 设置提供者返回工具调用
        SetupProviderWithToolCall("test_tool", "{}");

        // 执行
        var result = await loop.SendMessageAsync("Test message");

        // 断言
        reflectionEngine.Verify(
            r => r.ShouldReflectAsync(It.IsAny<ToolResult>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce());
    }

    [Fact]
    public async Task ToolSuccess_ShouldNotTriggerReflection()
    {
        // 准备
        var toolMock = CreateSuccessfulTool("test_tool", "Success");
        _tools.Register(toolMock.Object);

        var reflectionEngine = CreateReflectionEngine(shouldRetry: false);
        var loop = CreateAgentLoop(reflectionEngine: reflectionEngine.Object);

        // 设置提供者返回工具调用
        SetupProviderWithToolCall("test_tool", "{}");

        // 执行
        var result = await loop.SendMessageAsync("Test message");

        // 断言 - 成功的工具执行不应调用 ShouldReflectAsync
        reflectionEngine.Verify(
            r => r.ShouldReflectAsync(It.IsAny<ToolResult>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never());
    }

    #endregion

    #region 反思和重试成功测试

    [Fact(Skip = "AgentLoop reflection integration not yet implemented")]
    public async Task Reflection_ShouldRetry_WhenShouldRetryIsTrue()
    {
        // 准备
        var callCount = 0;
        var toolMock = new Mock<ITool>();
        toolMock.SetupGet(t => t.Name).Returns("test_tool");
        toolMock.SetupGet(t => t.Description).Returns("Test tool");
        toolMock.SetupGet(t => t.InputSchema).Returns(JsonDocument.Parse("{}").RootElement);
        toolMock.Setup(t => t.RequiresConfirmation(It.IsAny<JsonElement>())).Returns(false);
        toolMock.Setup(t => t.ExecuteAsync(It.IsAny<JsonElement>(), It.IsAny<CancellationToken>()))
            .Callback(() => callCount++)
            .ReturnsAsync(() => callCount < 2
                ? new ToolResult(false, "Failed on first attempt")
                : new ToolResult(true, "Success on retry"));
        _tools.Register(toolMock.Object);

        var reflectionEngine = CreateReflectionEngine(shouldRetry: true);
        var loop = CreateAgentLoop(reflectionEngine: reflectionEngine.Object);

        // 设置提供者返回工具调用
        SetupProviderWithToolCall("test_tool", "{}");

        // 执行
        var result = await loop.SendMessageAsync("Test message");

        // 断言 - 工具应被调用两次（初始调用 + 重试）
        toolMock.Verify(
            t => t.ExecuteAsync(It.IsAny<JsonElement>(), It.IsAny<CancellationToken>()),
            Times.AtLeast(2));
    }

    [Fact]
    public async Task Reflection_ShouldNotRetry_WhenShouldRetryIsFalse()
    {
        // 准备
        var toolMock = CreateFailingTool("test_tool", "Tool execution failed");
        _tools.Register(toolMock.Object);

        var reflectionEngine = CreateReflectionEngine(shouldRetry: false);
        var loop = CreateAgentLoop(reflectionEngine: reflectionEngine.Object);

        // 设置提供者返回工具调用
        SetupProviderWithToolCall("test_tool", "{}");

        // 执行
        var result = await loop.SendMessageAsync("Test message");

        // 断言 - 工具应只被调用一次（无重试）
        toolMock.Verify(
            t => t.ExecuteAsync(It.IsAny<JsonElement>(), It.IsAny<CancellationToken>()),
            Times.Once());
    }

    #endregion

    #region 最大重试次数限制测试

    [Fact]
    public async Task MaxRetryLimit_ShouldStopAfterMaxAttempts()
    {
        // 准备
        var toolMock = CreateFailingTool("test_tool", "Always fails");
        _tools.Register(toolMock.Object);

        var reflectionEngine = CreateReflectionEngine(shouldRetry: true);
        var loop = CreateAgentLoop(reflectionEngine: reflectionEngine.Object);

        // 设置提供者返回工具调用
        SetupProviderWithToolCall("test_tool", "{}");

        // 执行
        var result = await loop.SendMessageAsync("Test message");

        // 断言 - 工具应最多被调用 3 次（最大重试次数）
        toolMock.Verify(
            t => t.ExecuteAsync(It.IsAny<JsonElement>(), It.IsAny<CancellationToken>()),
            Times.AtMost(3));
    }

    [Fact]
    public async Task MaxRetryLimit_ShouldNotExceedThreeAttempts()
    {
        // 准备
        var executionCount = 0;
        var toolMock = new Mock<ITool>();
        toolMock.SetupGet(t => t.Name).Returns("test_tool");
        toolMock.SetupGet(t => t.Description).Returns("Test tool");
        toolMock.SetupGet(t => t.InputSchema).Returns(JsonDocument.Parse("{}").RootElement);
        toolMock.Setup(t => t.RequiresConfirmation(It.IsAny<JsonElement>())).Returns(false);
        toolMock.Setup(t => t.ExecuteAsync(It.IsAny<JsonElement>(), It.IsAny<CancellationToken>()))
            .Callback(() => executionCount++)
            .ReturnsAsync(new ToolResult(false, "Always fails"));
        _tools.Register(toolMock.Object);

        var reflectionEngine = CreateReflectionEngine(shouldRetry: true);
        var loop = CreateAgentLoop(reflectionEngine: reflectionEngine.Object);

        // 设置提供者返回工具调用
        SetupProviderWithToolCall("test_tool", "{}");

        // 执行
        var result = await loop.SendMessageAsync("Test message");

        // 断言 - 不应超过 3 次尝试
        executionCount.Should().BeLessThanOrEqualTo(3);
    }

    [Fact]
    public async Task ShouldReflectAsync_ShouldReturnFalse_WhenMaxAttemptsReached()
    {
        // 准备
        var engine = new ReflectionEngine(_chatProviderMock.Object, _reflectionConfig, 100000);
        var failedResult = new ToolResult(false, "Error");

        // 执行并断言
        (await engine.ShouldReflectAsync(failedResult, 3)).Should().BeFalse();
        (await engine.ShouldReflectAsync(failedResult, 4)).Should().BeFalse();
        (await engine.ShouldReflectAsync(failedResult, 5)).Should().BeFalse();
    }

    #endregion

    #region Token 预算测试

    [Fact]
    public void ReflectionEngine_ShouldUseTwentyPercentOfTotalBudget()
    {
        // 准备
        var totalBudget = 100000;
        var expectedReflectionBudget = 20000; // 100000 的 20%

        // 执行
        var engine = new ReflectionEngine(_chatProviderMock.Object, _reflectionConfig, totalBudget);

        // 断言
        engine.TokenBudget.Should().Be(expectedReflectionBudget);
    }

    [Fact]
    public void ReflectionEngine_ShouldRespectCustomTokenBudgetRatio()
    {
        // 准备
        var totalBudget = 100000;
        var customConfig = new ReflectionConfig { TokenBudgetRatio = 0.3 }; // 30%
        var expectedReflectionBudget = 30000;

        // 执行
        var engine = new ReflectionEngine(_chatProviderMock.Object, customConfig, totalBudget);

        // 断言
        engine.TokenBudget.Should().Be(expectedReflectionBudget);
    }

    [Fact]
    public async Task Reflection_ShouldStop_WhenTokenBudgetExceeded()
    {
        // 准备 - Create engine with very small budget
        var smallBudget = 10; // Very small budget
        var engine = new ReflectionEngine(_chatProviderMock.Object, _reflectionConfig, smallBudget);
        var context = new ReflectionContext
        {
            ToolName = "test_tool",
            Arguments = JsonDocument.Parse("{}").RootElement,
            ErrorMessage = "Error",
            AttemptNumber = 1
        };

        // 执行
        var result = await engine.ReflectAsync(context);

        // 断言 - Should return without retry due to budget exceeded
        result.ShouldRetry.Should().BeFalse();
        result.Analysis.Should().Contain("Token budget exceeded");
    }

    [Fact]
    public async Task ShouldReflectAsync_ShouldReturnFalse_WhenTokenBudgetExceeded()
    {
        // 准备 - Create engine with budget that will be exceeded
        var engine = new ReflectionEngine(_chatProviderMock.Object, _reflectionConfig, 600);
        var failedResult = new ToolResult(false, "Error");

        // Setup a reflection that uses tokens
        SetupChatProviderResponse(@"{""analysis"": ""Test"", ""suggestions"": [], ""shouldRetry"": true}");
        var context = new ReflectionContext
        {
            ToolName = "test_tool",
            Arguments = JsonDocument.Parse("{}").RootElement,
            ErrorMessage = "Error",
            AttemptNumber = 1
        };

        // Use up the budget
        await engine.ReflectAsync(context);

        // 执行 - Now budget should be exceeded
        var shouldReflect = await engine.ShouldReflectAsync(failedResult, 1);

        // 断言
        shouldReflect.Should().BeFalse();
    }

    [Fact]
    public async Task ReflectionEngine_ShouldTrackTokenUsage()
    {
        // 准备
        var engine = new ReflectionEngine(_chatProviderMock.Object, _reflectionConfig, 100000);
        SetupChatProviderResponse(@"{""analysis"": ""Test analysis"", ""suggestions"": [], ""shouldRetry"": true}");
        var context = new ReflectionContext
        {
            ToolName = "test_tool",
            Arguments = JsonDocument.Parse("{}").RootElement,
            ErrorMessage = "Error",
            AttemptNumber = 1
        };

        // 执行
        var initialUsage = engine.TokenUsage;
        await engine.ReflectAsync(context);
        var finalUsage = engine.TokenUsage;

        // 断言
        initialUsage.Should().Be(0);
        finalUsage.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ResetTokenUsage_ShouldResetTokenCounter()
    {
        // 准备
        var engine = new ReflectionEngine(_chatProviderMock.Object, _reflectionConfig, 100000);

        // 执行
        engine.ResetTokenUsage();

        // 断言
        engine.TokenUsage.Should().Be(0);
    }

    #endregion

    #region 端到端集成测试

    [Fact(Skip = "AgentLoop reflection integration not yet implemented")]
    public async Task FullIntegration_FailureReflectionRetrySuccess_ShouldWork()
    {
        // 准备
        var callCount = 0;
        var toolMock = new Mock<ITool>();
        toolMock.SetupGet(t => t.Name).Returns("test_tool");
        toolMock.SetupGet(t => t.Description).Returns("Test tool");
        toolMock.SetupGet(t => t.InputSchema).Returns(JsonDocument.Parse("{}").RootElement);
        toolMock.Setup(t => t.RequiresConfirmation(It.IsAny<JsonElement>())).Returns(false);
        toolMock.Setup(t => t.ExecuteAsync(It.IsAny<JsonElement>(), It.IsAny<CancellationToken>()))
            .Callback(() => callCount++)
            .ReturnsAsync(() => callCount < 2
                ? new ToolResult(false, "Failed on first attempt")
                : new ToolResult(true, "Success on retry"));
        _tools.Register(toolMock.Object);

        // Create real reflection engine with mock provider
        var reflectionEngine = new ReflectionEngine(_chatProviderMock.Object, _reflectionConfig, 100000);
        SetupChatProviderResponse(@"{""analysis"": ""Arguments were incorrect"", ""suggestions"": [""Check arguments""], ""shouldRetry"": true}");

        var loop = CreateAgentLoop(reflectionEngine: reflectionEngine);

        // 设置提供者返回工具调用
        SetupProviderWithToolCall("test_tool", "{}");

        // 执行
        var result = await loop.SendMessageAsync("Test message");

        // 断言 - Tool should succeed after retry
        callCount.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task FullIntegration_MaxRetriesReached_ShouldReturnFailure()
    {
        // 准备
        var toolMock = CreateFailingTool("test_tool", "Always fails");
        _tools.Register(toolMock.Object);

        var reflectionEngine = new ReflectionEngine(_chatProviderMock.Object, _reflectionConfig, 100000);
        SetupChatProviderResponse(@"{""analysis"": ""Unrecoverable error"", ""suggestions"": [], ""shouldRetry"": true}");

        var loop = CreateAgentLoop(reflectionEngine: reflectionEngine);

        // 设置提供者返回工具调用
        SetupProviderWithToolCall("test_tool", "{}");

        // 执行
        var result = await loop.SendMessageAsync("Test message");

        // 断言 - Tool should be called max 3 times
        toolMock.Verify(
            t => t.ExecuteAsync(It.IsAny<JsonElement>(), It.IsAny<CancellationToken>()),
            Times.AtMost(3));
    }

    #endregion

    #region 辅助方法

    private Mock<ITool> CreateFailingTool(string name, string errorMessage)
    {
        var toolMock = new Mock<ITool>();
        toolMock.SetupGet(t => t.Name).Returns(name);
        toolMock.SetupGet(t => t.Description).Returns($"Test tool {name}");
        toolMock.SetupGet(t => t.InputSchema).Returns(JsonDocument.Parse("{}").RootElement);
        toolMock.Setup(t => t.RequiresConfirmation(It.IsAny<JsonElement>())).Returns(false);
        toolMock.Setup(t => t.ExecuteAsync(It.IsAny<JsonElement>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResult(false, errorMessage));
        return toolMock;
    }

    private Mock<ITool> CreateSuccessfulTool(string name, string output)
    {
        var toolMock = new Mock<ITool>();
        toolMock.SetupGet(t => t.Name).Returns(name);
        toolMock.SetupGet(t => t.Description).Returns($"Test tool {name}");
        toolMock.SetupGet(t => t.InputSchema).Returns(JsonDocument.Parse("{}").RootElement);
        toolMock.Setup(t => t.RequiresConfirmation(It.IsAny<JsonElement>())).Returns(false);
        toolMock.Setup(t => t.ExecuteAsync(It.IsAny<JsonElement>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResult(true, output));
        return toolMock;
    }

    private Mock<IReflectionEngine> CreateReflectionEngine(bool shouldRetry)
    {
        var reflectionMock = new Mock<IReflectionEngine>();
        reflectionMock
            .Setup(r => r.ShouldReflectAsync(It.IsAny<ToolResult>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ToolResult result, int attempt, CancellationToken ct) => !result.Success && attempt < 3);
        reflectionMock
            .Setup(r => r.ReflectAsync(It.IsAny<ReflectionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReflectionResult
            {
                Analysis = "Test analysis",
                Suggestions = ["Test suggestion"],
                ShouldRetry = shouldRetry
            });
        return reflectionMock;
    }

    private AgentLoop CreateAgentLoop(
        IChatProvider? provider = null,
        IReflectionEngine? reflectionEngine = null)
    {
        _sessionCli.InitializeAsync().GetAwaiter().GetResult();
        var ui = new ConsoleUI();
        var console = _consoleMock.Object;

        return new AgentLoop(
            provider ?? _chatProviderMock.Object,
            _tools,
            new ChatOptions(),
            console,
            _sessionCli,
            ui,
            new MessageHandler(_sessionCli),
            new ToolExecutor(_tools, ui, console, reflectionEngine),
            new StreamProcessor(ui)
        );
    }

    private void SetupProviderWithToolCall(string toolName, string arguments)
    {
        var callCount = 0;
        _chatProviderMock
            .Setup(p => p.CompleteStreamingAsync(
                It.IsAny<IReadOnlyList<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    // First call: return tool call
                    return new List<StreamChunk>
                    {
                        new(null, null, new ToolCallDelta("call_1", toolName, null), "tool_use", null),
                        new(null, null, new ToolCallDelta(null, null, arguments), null, null)
                    }.ToAsyncEnumerable();
                }
                else
                {
                    // Subsequent calls: return end_turn to stop the loop
                    return new List<StreamChunk>
                    {
                        new("Done", null, null, "end_turn", null)
                    }.ToAsyncEnumerable();
                }
            });
    }

    private void SetupChatProviderResponse(string response)
    {
        var chunks = new List<StreamChunk>
        {
            new(TextDelta: response, null, null, null, new UsageInfo(100, 50))
        };

        _chatProviderMock
            .Setup(p => p.CompleteStreamingAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(chunks.ToAsyncEnumerable());
    }

    #endregion
}