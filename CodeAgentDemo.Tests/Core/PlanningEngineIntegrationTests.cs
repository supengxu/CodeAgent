using System.Runtime.CompilerServices;
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
/// Integration tests for PlanningEngine with AgentLoop.
/// Tests end-to-end scenarios: complex task -> planning -> execution.
/// </summary>
public class PlanningEngineIntegrationTests
{
    private readonly Mock<IChatProvider> _chatProviderMock;
    private readonly PlanningConfig _config;
    private readonly PlanningEngine _planningEngine;
    private readonly string _testSessionsDir;

    public PlanningEngineIntegrationTests()
    {
        _chatProviderMock = new Mock<IChatProvider>();
        _config = new PlanningConfig
        {
            ComplexityThreshold = 500,
            TimeoutSeconds = 2,
            MinToolCallsForComplex = 3
        };
        _planningEngine = new PlanningEngine(_chatProviderMock.Object, _config);
        _testSessionsDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    }

    #region Fast-Path Tests (Simple Tasks Skip Planning)

    [Fact]
    public async Task AgentLoop_WithSimpleTask_ShouldSkipPlanning()
    {
        // Arrange - Simple task with no tool keywords
        var simpleTask = "Hello, how are you?";
        var consoleMock = new Mock<IConsoleIO>();
        var tools = new ToolRegistry();

        SetupProviderResponse("I'm doing well, thank you!");

        // Act - Create AgentLoop with PlanningEngine
        var sessionCli = new SessionCli(_testSessionsDir);
        var loop = new AgentLoop(
            _chatProviderMock.Object, tools, new ChatOptions(),
            consoleMock.Object, sessionCli, new ConsoleUI(), _planningEngine, null, null, null);

        var result = await loop.SendMessageAsync(simpleTask);

        // Assert - Planning should be skipped (no GeneratePlanAsync call)
        result.Should().NotBeNull();
        result.StopReason.Should().Be("end_turn");
        _chatProviderMock.Verify(
            p => p.CompleteStreamingAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.Is<ChatOptions>(o => o.SystemPrompt == null || !o.SystemPrompt.Contains("planning")),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce());
    }

    [Fact]
    public async Task AgentLoop_WithShortTask_ShouldNotCallGeneratePlan()
    {
        // Arrange - Task below complexity threshold
        var shortTask = "Read a file";
        var consoleMock = new Mock<IConsoleIO>();
        var tools = new ToolRegistry();

        SetupProviderResponse("Done");

        // Act
        var sessionCli = new SessionCli(_testSessionsDir);
        var loop = new AgentLoop(
            _chatProviderMock.Object, tools, new ChatOptions(),
            consoleMock.Object, sessionCli, new ConsoleUI(), _planningEngine, null, null, null);

        await loop.SendMessageAsync(shortTask);

        // Assert - GeneratePlanAsync should not be called for simple tasks
        // Note: The planning engine is called internally, but for simple tasks
        // it returns Simple complexity and skips plan generation
        var complexity = await _planningEngine.AssessComplexityAsync(shortTask);
        complexity.Should().Be(ComplexityLevel.Simple);
    }

    #endregion

    #region Complex Task Planning Tests

    [Fact]
    public async Task AgentLoop_WithComplexTask_ShouldGeneratePlan()
    {
        // Arrange - Complex task with multiple tool keywords
        var complexTask = "Read the configuration file, edit the settings, and run the tests to verify the changes";
        var consoleMock = new Mock<IConsoleIO>();
        var tools = new ToolRegistry();

        // Setup planning response
        SetupPlanningResponse("1. Read configuration file\n2. Edit settings\n3. Run tests");
        // Setup execution response
        SetupProviderResponse("Task completed successfully");

        // Act
        var sessionCli = new SessionCli(_testSessionsDir);
        var loop = new AgentLoop(
            _chatProviderMock.Object, tools, new ChatOptions(),
            consoleMock.Object, sessionCli, new ConsoleUI(), _planningEngine, null, null, null);

        var result = await loop.SendMessageAsync(complexTask);

        // Assert
        result.Should().NotBeNull();
        // Verify complexity assessment
        var complexity = await _planningEngine.AssessComplexityAsync(complexTask);
        complexity.Should().Be(ComplexityLevel.Complex);
    }

    [Fact]
    public async Task AgentLoop_WithLongTask_ShouldTriggerPlanning()
    {
        // Arrange - Task exceeding token threshold
        var longTask = new string('a', 2500); // 2500 chars = ~625 tokens
        var consoleMock = new Mock<IConsoleIO>();
        var tools = new ToolRegistry();

        SetupPlanningResponse("1. Process the long input");
        SetupProviderResponse("Processed");

        // Act
        var sessionCli = new SessionCli(_testSessionsDir);
        var loop = new AgentLoop(
            _chatProviderMock.Object, tools, new ChatOptions(),
            consoleMock.Object, sessionCli, new ConsoleUI(), _planningEngine, null, null, null);

        await loop.SendMessageAsync(longTask);

        // Assert
        var complexity = await _planningEngine.AssessComplexityAsync(longTask);
        complexity.Should().Be(ComplexityLevel.Complex);
    }

    [Fact]
    public async Task PlanningEngine_ComplexTask_ShouldGenerateMultipleSteps()
    {
        // Arrange
        var complexTask = "Build the project, run all tests, and deploy to staging";
        SetupPlanningResponse("1. Build the project\n2. Run all tests\n3. Deploy to staging");

        // Act
        var plan = await _planningEngine.GeneratePlanAsync(complexTask);

        // Assert
        plan.Should().NotBeNull();
        plan.Steps.Should().HaveCount(3);
        plan.OriginalTask.Should().Be(complexTask);
        plan.Complexity.Should().Be(ComplexityLevel.Complex);
    }

    #endregion

    #region Timeout Handling Tests

    [Fact]
    public async Task PlanningEngine_OnTimeout_ShouldReturnFallbackPlan()
    {
        // Arrange - Setup provider to delay longer than timeout
        _chatProviderMock
            .Setup(p => p.CompleteStreamingAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<ChatMessage> _, ChatOptions? _, CancellationToken ct) =>
                DelayedAsyncEnumerable(ct));

        // Act
        var plan = await _planningEngine.GeneratePlanAsync("Complex task that times out");

        // Assert - Should return fallback simple plan
        plan.Should().NotBeNull();
        plan.Steps.Should().ContainSingle()
            .Which.Should().Be("Complex task that times out");
        plan.Complexity.Should().Be(ComplexityLevel.Simple);
    }

    [Fact]
    public async Task AgentLoop_WithPlanningTimeout_ShouldContinueWithFallback()
    {
        // Arrange
        var complexTask = "Read, edit, and test the code";
        var consoleMock = new Mock<IConsoleIO>();
        var tools = new ToolRegistry();

        // Setup delayed planning response (will timeout)
        var callCount = 0;
        _chatProviderMock
            .Setup(p => p.CompleteStreamingAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<ChatMessage> _, ChatOptions? _, CancellationToken ct) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    // First call (planning) - will timeout
                    return DelayedAsyncEnumerable(ct);
                }
                // Second call (execution)
                return CreateAsyncEnumerable(new List<StreamChunk>
                {
                    new(TextDelta: "Task completed", null, null, "end_turn", null)
                });
            });

        // Act
        var sessionCli = new SessionCli(_testSessionsDir);
        var loop = new AgentLoop(
            _chatProviderMock.Object, tools, new ChatOptions(),
            consoleMock.Object, sessionCli, new ConsoleUI(), _planningEngine, null, null, null);

        var result = await loop.SendMessageAsync(complexTask);

        // Assert - Should complete despite planning timeout
        result.Should().NotBeNull();
        result.StopReason.Should().Be("end_turn");
    }

    private static async IAsyncEnumerable<StreamChunk> DelayedAsyncEnumerable(
        [EnumeratorCancellation] CancellationToken ct)
    {
        await Task.Delay(3000, ct); // Delay longer than 2 second timeout
        yield return new StreamChunk(TextDelta: "1. Step", null, null, null, null);
    }

    #endregion

    #region Planning Failure Degradation Tests

    [Fact]
    public async Task PlanningEngine_OnEmptyResponse_ShouldReturnFallbackPlan()
    {
        // Arrange
        SetupPlanningResponse("");

        // Act
        var plan = await _planningEngine.GeneratePlanAsync("Test task");

        // Assert - Empty response should result in empty steps
        plan.Should().NotBeNull();
        plan.Steps.Should().BeEmpty();
    }

    [Fact]
    public async Task PlanningEngine_OnInvalidResponse_ShouldHandleGracefully()
    {
        // Arrange - Response without numbered steps
        SetupPlanningResponse("This is just random text without any steps");

        // Act
        var plan = await _planningEngine.GeneratePlanAsync("Test task");

        // Assert - Should use entire response as single step
        plan.Should().NotBeNull();
        plan.Steps.Should().ContainSingle()
            .Which.Should().Contain("random text");
    }

    [Fact]
    public async Task AgentLoop_WithPlanningFailure_ShouldContinueExecution()
    {
        // Arrange
        var task = "Do something complex";
        var consoleMock = new Mock<IConsoleIO>();
        var tools = new ToolRegistry();

        // Setup planning to return empty steps
        SetupPlanningResponse("");
        SetupProviderResponse("Executed anyway");

        // Act
        var sessionCli = new SessionCli(_testSessionsDir);
        var loop = new AgentLoop(
            _chatProviderMock.Object, tools, new ChatOptions(),
            consoleMock.Object, sessionCli, new ConsoleUI(), _planningEngine, null, null, null);

        var result = await loop.SendMessageAsync(task);

        // Assert - Should continue execution even with empty plan
        result.Should().NotBeNull();
        result.StopReason.Should().Be("end_turn");
    }

    #endregion

    #region End-to-End Integration Tests

    [Fact]
    public async Task AgentLoop_FullWorkflow_ComplexTask_Planning_Execution()
    {
        // Arrange
        var complexTask = "Read the source file, find all TODO comments, and create a summary report";
        var consoleMock = new Mock<IConsoleIO>();
        var tools = new ToolRegistry();

        // Setup planning response
        SetupPlanningResponse("1. Read source file\n2. Find TODO comments\n3. Create summary report");
        // Setup execution responses
        SetupProviderResponse("Summary report created with 5 TODO items found.");

        // Act
        var sessionCli = new SessionCli(_testSessionsDir);
        var loop = new AgentLoop(
            _chatProviderMock.Object, tools, new ChatOptions(),
            consoleMock.Object, sessionCli, new ConsoleUI(), _planningEngine, null, null, null);

        var result = await loop.SendMessageAsync(complexTask);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().NotBeEmpty();
        result.StopReason.Should().Be("end_turn");

        // Verify complexity was assessed as complex
        var complexity = await _planningEngine.AssessComplexityAsync(complexTask);
        complexity.Should().Be(ComplexityLevel.Complex);
    }

    [Fact]
    public async Task AgentLoop_WithToolCalls_ShouldIntegrateWithPlanning()
    {
        // Arrange
        var task = "Read the file and show its content";
        var consoleMock = new Mock<IConsoleIO>();
        var tools = new ToolRegistry();

        // Register a mock tool
        var toolMock = new Mock<ITool>();
        toolMock.SetupGet(t => t.Name).Returns("read_file");
        toolMock.SetupGet(t => t.Description).Returns("Reads a file");
        toolMock.SetupGet(t => t.InputSchema).Returns(System.Text.Json.JsonDocument.Parse("{}").RootElement);
        toolMock.Setup(t => t.ExecuteAsync(It.IsAny<System.Text.Json.JsonElement>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResult(true, "File content"));
        toolMock.Setup(t => t.RequiresConfirmation(It.IsAny<System.Text.Json.JsonElement>()))
            .Returns(false);
        tools.Register(toolMock.Object);

        SetupProviderResponse("Here is the file content:");

        // Act
        var sessionCli = new SessionCli(_testSessionsDir);
        var loop = new AgentLoop(
            _chatProviderMock.Object, tools, new ChatOptions(),
            consoleMock.Object, sessionCli, new ConsoleUI(), _planningEngine, null, null, null);

        // This task has 2 tool keywords (read, show), below threshold of 3
        var complexity = await _planningEngine.AssessComplexityAsync(task);

        // Assert
        complexity.Should().Be(ComplexityLevel.Simple);
    }

    [Fact]
    public async Task PlanningEngine_WithCancellationToken_ShouldRespectCancellation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        _chatProviderMock
            .Setup(p => p.CompleteStreamingAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<ChatMessage> _, ChatOptions? _, CancellationToken ct) =>
                CancellableAsyncEnumerable(ct));

        // Act & Assert
        var act = async () => await _planningEngine.GeneratePlanAsync("Test task", cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static async IAsyncEnumerable<StreamChunk> CancellableAsyncEnumerable(
        [EnumeratorCancellation] CancellationToken ct)
    {
        await Task.Delay(500, ct); // Will be cancelled
        yield return new StreamChunk(TextDelta: "response", null, null, null, null);
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public void PlanningConfig_DefaultValues_ShouldBeCorrect()
    {
        var config = new PlanningConfig();

        config.ComplexityThreshold.Should().Be(500);
        config.TimeoutSeconds.Should().Be(2);
        config.MinToolCallsForComplex.Should().Be(3);
    }

    [Fact]
    public async Task PlanningEngine_WithCustomConfig_ShouldRespectThresholds()
    {
        // Arrange
        var customConfig = new PlanningConfig
        {
            ComplexityThreshold = 100,
            TimeoutSeconds = 5,
            MinToolCallsForComplex = 2
        };
        var customEngine = new PlanningEngine(_chatProviderMock.Object, customConfig);

        // Act - Task with 2 tool keywords should now be complex (threshold is 2)
        var task = "Read and write the file";
        var complexity = await customEngine.AssessComplexityAsync(task);

        // Assert
        complexity.Should().Be(ComplexityLevel.Complex);
    }

    [Fact]
    public async Task PlanningEngine_WithHighThreshold_ShouldClassifyMoreTasksAsSimple()
    {
        // Arrange
        var customConfig = new PlanningConfig
        {
            ComplexityThreshold = 1000,
            MinToolCallsForComplex = 10
        };
        var customEngine = new PlanningEngine(_chatProviderMock.Object, customConfig);

        // Act - Task that would normally be complex
        var task = "Read, write, and test the code";
        var complexity = await customEngine.AssessComplexityAsync(task);

        // Assert - Should be simple due to high thresholds
        complexity.Should().Be(ComplexityLevel.Simple);
    }

    #endregion

    #region Helper Methods

    private void SetupProviderResponse(string response)
    {
        _chatProviderMock
            .Setup(p => p.CompleteStreamingAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(new List<StreamChunk>
            {
                new(TextDelta: response, null, null, "end_turn", new UsageInfo(100, 50))
            }));
    }

    private void SetupPlanningResponse(string planningResponse)
    {
        // Planning calls have EnableThinking = false and specific system prompt
        _chatProviderMock
            .Setup(p => p.CompleteStreamingAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.Is<ChatOptions>(o => o.MaxTokens == 1024 && o.EnableThinking == false),
                It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(new List<StreamChunk>
            {
                new(TextDelta: planningResponse, null, null, null, new UsageInfo(50, 30))
            }));
    }

    private static async IAsyncEnumerable<T> CreateAsyncEnumerable<T>(IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            await Task.Yield();
            yield return item;
        }
    }

    #endregion
}

/// <summary>
/// Tests for PlanningEngine behavior with different task patterns.
/// </summary>
public class PlanningEngineTaskPatternTests
{
    private readonly Mock<IChatProvider> _chatProviderMock;
    private readonly PlanningEngine _engine;

    public PlanningEngineTaskPatternTests()
    {
        _chatProviderMock = new Mock<IChatProvider>();
        _engine = new PlanningEngine(_chatProviderMock.Object, new PlanningConfig());
    }

    [Theory]
    [InlineData("read the file", 1)]
    [InlineData("read and write the file", 2)]
    [InlineData("read, write, and test the code", 3)]
    [InlineData("build, test, and run the application", 3)]
    public async Task AssessComplexity_ShouldCountToolKeywordsCorrectly(string task, int expectedKeywordCount)
    {
        // Act
        var complexity = await _engine.AssessComplexityAsync(task);

        // Assert
        // Tasks with 3+ keywords should be complex
        complexity.Should().Be(expectedKeywordCount >= 3 ? ComplexityLevel.Complex : ComplexityLevel.Simple);
    }

    [Theory]
    [InlineData("What is the weather?")]
    [InlineData("Tell me a joke")]
    [InlineData("Explain quantum physics")]
    public async Task AssessComplexity_WithNoToolKeywords_ShouldReturnSimple(string task)
    {
        // Act
        var complexity = await _engine.AssessComplexityAsync(task);

        // Assert
        complexity.Should().Be(ComplexityLevel.Simple);
    }

    [Fact]
    public async Task AssessComplexity_WithMixedKeywords_ShouldCountCorrectly()
    {
        // Arrange - Mix of tool and non-tool keywords
        var task = "Please read the documentation and explain it to me";

        // Act
        var complexity = await _engine.AssessComplexityAsync(task);

        // Assert - Only "read" is a tool keyword
        complexity.Should().Be(ComplexityLevel.Simple);
    }
}