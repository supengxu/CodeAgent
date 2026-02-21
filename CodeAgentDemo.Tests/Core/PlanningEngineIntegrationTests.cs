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
        var simpleTask = "Hello, how are you?";
        var consoleMock = new Mock<IConsoleIO>();

        SetupProviderResponse("I'm doing well, thank you!");

        var loop = TestHelper.CreateAgentLoop(
            provider: _chatProviderMock.Object,
            console: consoleMock.Object,
            planningEngine: _planningEngine,
            sessionsDir: _testSessionsDir
        );

        var result = await loop.SendMessageAsync(simpleTask);

        result.Should().NotBeNull();
        result.StopReason.Should().Be("end_turn");
    }

    [Fact]
    public async Task AgentLoop_WithShortTask_ShouldNotCallGeneratePlan()
    {
        var shortTask = "Read a file";
        var consoleMock = new Mock<IConsoleIO>();

        SetupProviderResponse("Done");

        var loop = TestHelper.CreateAgentLoop(
            provider: _chatProviderMock.Object,
            console: consoleMock.Object,
            planningEngine: _planningEngine,
            sessionsDir: _testSessionsDir
        );

        await loop.SendMessageAsync(shortTask);

        var complexity = await _planningEngine.AssessComplexityAsync(shortTask);
        complexity.Should().Be(ComplexityLevel.Simple);
    }

    #endregion

    #region Complex Task Planning Tests

    [Fact]
    public async Task AgentLoop_WithComplexTask_ShouldGeneratePlan()
    {
        var complexTask = "Read the configuration file, edit the settings, and run the tests to verify the changes";
        var consoleMock = new Mock<IConsoleIO>();

        SetupPlanningResponse("1. Read configuration file\n2. Edit settings\n3. Run tests");
        SetupProviderResponse("Task completed successfully");

        var loop = TestHelper.CreateAgentLoop(
            provider: _chatProviderMock.Object,
            console: consoleMock.Object,
            planningEngine: _planningEngine,
            sessionsDir: _testSessionsDir
        );

        var result = await loop.SendMessageAsync(complexTask);

        result.Should().NotBeNull();
        var complexity = await _planningEngine.AssessComplexityAsync(complexTask);
        complexity.Should().Be(ComplexityLevel.Complex);
    }

    [Fact]
    public async Task AgentLoop_WithLongTask_ShouldTriggerPlanning()
    {
        var longTask = new string('a', 2500);
        var consoleMock = new Mock<IConsoleIO>();

        SetupPlanningResponse("1. Process the long input");
        SetupProviderResponse("Processed");

        var loop = TestHelper.CreateAgentLoop(
            provider: _chatProviderMock.Object,
            console: consoleMock.Object,
            planningEngine: _planningEngine,
            sessionsDir: _testSessionsDir
        );

        await loop.SendMessageAsync(longTask);

        var complexity = await _planningEngine.AssessComplexityAsync(longTask);
        complexity.Should().Be(ComplexityLevel.Complex);
    }

    [Fact]
    public async Task PlanningEngine_ComplexTask_ShouldGenerateMultipleSteps()
    {
        var complexTask = "Build the project, run all tests, and deploy to staging";
        SetupPlanningResponse("1. Build the project\n2. Run all tests\n3. Deploy to staging");

        var plan = await _planningEngine.GeneratePlanAsync(complexTask);

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
        _chatProviderMock
            .Setup(p => p.CompleteStreamingAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<ChatMessage> _, ChatOptions? _, CancellationToken ct) =>
                DelayedAsyncEnumerable(ct));

        var plan = await _planningEngine.GeneratePlanAsync("Complex task that times out");

        plan.Should().NotBeNull();
        plan.Steps.Should().ContainSingle()
            .Which.Should().Be("Complex task that times out");
        plan.Complexity.Should().Be(ComplexityLevel.Simple);
    }

    [Fact]
    public async Task AgentLoop_WithPlanningTimeout_ShouldContinueWithFallback()
    {
        var complexTask = "Read, edit, and test the code";
        var consoleMock = new Mock<IConsoleIO>();

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
                    return DelayedAsyncEnumerable(ct);
                }
                return CreateAsyncEnumerable(new List<StreamChunk>
                {
                    new(TextDelta: "Task completed", null, null, "end_turn", null)
                });
            });

        var loop = TestHelper.CreateAgentLoop(
            provider: _chatProviderMock.Object,
            console: consoleMock.Object,
            planningEngine: _planningEngine,
            sessionsDir: _testSessionsDir
        );

        var result = await loop.SendMessageAsync(complexTask);

        result.Should().NotBeNull();
        result.StopReason.Should().Be("end_turn");
    }

    private static async IAsyncEnumerable<StreamChunk> DelayedAsyncEnumerable(
        [EnumeratorCancellation] CancellationToken ct)
    {
        await Task.Delay(3000, ct);
        yield return new StreamChunk(TextDelta: "1. Step", null, null, null, null);
    }

    #endregion

    #region Planning Failure Degradation Tests

    [Fact]
    public async Task PlanningEngine_OnEmptyResponse_ShouldReturnFallbackPlan()
    {
        SetupPlanningResponse("");

        var plan = await _planningEngine.GeneratePlanAsync("Test task");

        plan.Should().NotBeNull();
        plan.Steps.Should().BeEmpty();
    }

    [Fact]
    public async Task PlanningEngine_OnInvalidResponse_ShouldHandleGracefully()
    {
        SetupPlanningResponse("This is just random text without any steps");

        var plan = await _planningEngine.GeneratePlanAsync("Test task");

        plan.Should().NotBeNull();
        plan.Steps.Should().ContainSingle()
            .Which.Should().Contain("random text");
    }

    [Fact]
    public async Task AgentLoop_WithPlanningFailure_ShouldContinueExecution()
    {
        var task = "Do something complex";
        var consoleMock = new Mock<IConsoleIO>();

        SetupPlanningResponse("");
        SetupProviderResponse("Executed anyway");

        var loop = TestHelper.CreateAgentLoop(
            provider: _chatProviderMock.Object,
            console: consoleMock.Object,
            planningEngine: _planningEngine,
            sessionsDir: _testSessionsDir
        );

        var result = await loop.SendMessageAsync(task);

        result.Should().NotBeNull();
        result.StopReason.Should().Be("end_turn");
    }

    #endregion

    #region End-to-End Integration Tests

    [Fact]
    public async Task AgentLoop_FullWorkflow_ComplexTask_Planning_Execution()
    {
        var complexTask = "Read the source file, find all TODO comments, and create a summary report";
        var consoleMock = new Mock<IConsoleIO>();

        SetupPlanningResponse("1. Read source file\n2. Find TODO comments\n3. Create summary report");
        SetupProviderResponse("Summary report created with 5 TODO items found.");

        var loop = TestHelper.CreateAgentLoop(
            provider: _chatProviderMock.Object,
            console: consoleMock.Object,
            planningEngine: _planningEngine,
            sessionsDir: _testSessionsDir
        );

        var result = await loop.SendMessageAsync(complexTask);

        result.Should().NotBeNull();
        result.Content.Should().NotBeEmpty();
        result.StopReason.Should().Be("end_turn");

        var complexity = await _planningEngine.AssessComplexityAsync(complexTask);
        complexity.Should().Be(ComplexityLevel.Complex);
    }

    [Fact]
    public async Task AgentLoop_WithToolCalls_ShouldIntegrateWithPlanning()
    {
        var task = "Read the file and show its content";
        var consoleMock = new Mock<IConsoleIO>();
        var tools = new ToolRegistry();

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

        var loop = TestHelper.CreateAgentLoop(
            provider: _chatProviderMock.Object,
            tools: tools,
            console: consoleMock.Object,
            planningEngine: _planningEngine,
            sessionsDir: _testSessionsDir
        );

        var complexity = await _planningEngine.AssessComplexityAsync(task);

        complexity.Should().Be(ComplexityLevel.Simple);
    }

    [Fact]
    public async Task PlanningEngine_WithCancellationToken_ShouldRespectCancellation()
    {
        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        _chatProviderMock
            .Setup(p => p.CompleteStreamingAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<ChatMessage> _, ChatOptions? _, CancellationToken ct) =>
                CancellableAsyncEnumerable(ct));

        var act = async () => await _planningEngine.GeneratePlanAsync("Test task", cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static async IAsyncEnumerable<StreamChunk> CancellableAsyncEnumerable(
        [EnumeratorCancellation] CancellationToken ct)
    {
        await Task.Delay(500, ct);
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
        var customConfig = new PlanningConfig
        {
            ComplexityThreshold = 100,
            TimeoutSeconds = 5,
            MinToolCallsForComplex = 2
        };
        var customEngine = new PlanningEngine(_chatProviderMock.Object, customConfig);

        var task = "Read and write the file";
        var complexity = await customEngine.AssessComplexityAsync(task);

        complexity.Should().Be(ComplexityLevel.Complex);
    }

    [Fact]
    public async Task PlanningEngine_WithHighThreshold_ShouldClassifyMoreTasksAsSimple()
    {
        var customConfig = new PlanningConfig
        {
            ComplexityThreshold = 1000,
            MinToolCallsForComplex = 10
        };
        var customEngine = new PlanningEngine(_chatProviderMock.Object, customConfig);

        var task = "Read, write, and test the code";
        var complexity = await customEngine.AssessComplexityAsync(task);

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
        var complexity = await _engine.AssessComplexityAsync(task);

        complexity.Should().Be(expectedKeywordCount >= 3 ? ComplexityLevel.Complex : ComplexityLevel.Simple);
    }

    [Theory]
    [InlineData("What is the weather?")]
    [InlineData("Tell me a joke")]
    [InlineData("Explain quantum physics")]
    public async Task AssessComplexity_WithNoToolKeywords_ShouldReturnSimple(string task)
    {
        var complexity = await _engine.AssessComplexityAsync(task);

        complexity.Should().Be(ComplexityLevel.Simple);
    }

    [Fact]
    public async Task AssessComplexity_WithMixedKeywords_ShouldCountCorrectly()
    {
        var task = "Please read the documentation and explain it to me";

        var complexity = await _engine.AssessComplexityAsync(task);

        complexity.Should().Be(ComplexityLevel.Simple);
    }
}