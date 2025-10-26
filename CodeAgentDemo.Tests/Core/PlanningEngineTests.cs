using System.Runtime.CompilerServices;
using CodeAgentDemo.Core;
using CodeAgentDemo.Models;
using CodeAgentDemo.Providers;
using FluentAssertions;
using Moq;
using Xunit;
using CodeAgentDemo.Tests;

namespace CodeAgentDemo.Tests.Core;

public class PlanningEngineTests
{
    private readonly Mock<IChatProvider> _chatProviderMock;
    private readonly PlanningConfig _config;
    private readonly PlanningEngine _engine;

    public PlanningEngineTests()
    {
        _chatProviderMock = new Mock<IChatProvider>();
        _config = new PlanningConfig();
        _engine = new PlanningEngine(_chatProviderMock.Object, _config);
    }

    #region AssessComplexityAsync Tests

    [Fact]
    public async Task AssessComplexityAsync_WithShortTask_ShouldReturnSimple()
    {
        // Arrange - Task with less than 500 tokens (less than 2000 chars)
        var shortTask = "Read a file";

        // Act
        var result = await _engine.AssessComplexityAsync(shortTask);

        // Assert
        result.Should().Be(ComplexityLevel.Simple);
    }

    [Fact]
    public async Task AssessComplexityAsync_WithLongTask_ShouldReturnComplex()
    {
        // Arrange - Task with more than 500 tokens (more than 2000 chars)
        var longTask = new string('a', 2500); // 2500 chars = ~625 tokens

        // Act
        var result = await _engine.AssessComplexityAsync(longTask);

        // Assert
        result.Should().Be(ComplexityLevel.Complex);
    }

    [Fact]
    public async Task AssessComplexityAsync_WithMultipleToolKeywords_ShouldReturnComplex()
    {
        // Arrange - Task with 3+ tool keywords
        var taskWithTools = "Read the file, then edit it, and finally run the test";

        // Act
        var result = await _engine.AssessComplexityAsync(taskWithTools);

        // Assert
        result.Should().Be(ComplexityLevel.Complex);
    }

    [Fact]
    public async Task AssessComplexityAsync_WithFewToolKeywords_ShouldReturnSimple()
    {
        // Arrange - Task with less than 3 tool keywords
        var taskWithFewTools = "Read the file and show its content";

        // Act
        var result = await _engine.AssessComplexityAsync(taskWithFewTools);

        // Assert
        result.Should().Be(ComplexityLevel.Simple);
    }

    [Fact]
    public async Task AssessComplexityAsync_WithNullTask_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _engine.AssessComplexityAsync(null!));
    }

    [Fact]
    public async Task AssessComplexityAsync_WithCustomThreshold_ShouldRespectConfig()
    {
        // Arrange
        var customConfig = new PlanningConfig { ComplexityThreshold = 100 };
        var customEngine = new PlanningEngine(_chatProviderMock.Object, customConfig);
        var task = new string('a', 300); // 300 chars = ~75 tokens, below threshold of 100

        // Act
        var result = await customEngine.AssessComplexityAsync(task);

        // Assert
        result.Should().Be(ComplexityLevel.Simple);
    }

    #endregion

    #region GeneratePlanAsync Tests

    [Fact]
    public async Task GeneratePlanAsync_WithNullTask_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _engine.GeneratePlanAsync(null!));
    }

    [Fact]
    public async Task GeneratePlanAsync_ShouldCallChatProvider()
    {
        // Arrange
        SetupChatProviderResponse("1. Step one\n2. Step two\n3. Step three");

        // Act
        var result = await _engine.GeneratePlanAsync("Test task");

        // Assert
        _chatProviderMock.Verify(
            p => p.CompleteStreamingAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GeneratePlanAsync_ShouldParseNumberedSteps()
    {
        // Arrange
        SetupChatProviderResponse("1. First step\n2. Second step\n3. Third step");

        // Act
        var result = await _engine.GeneratePlanAsync("Test task");

        // Assert
        result.Steps.Should().HaveCount(3);
        result.Steps[0].Should().Be("First step");
        result.Steps[1].Should().Be("Second step");
        result.Steps[2].Should().Be("Third step");
    }

    [Fact]
    public async Task GeneratePlanAsync_ShouldSetOriginalTask()
    {
        // Arrange
        SetupChatProviderResponse("1. Step one");

        // Act
        var result = await _engine.GeneratePlanAsync("Original task description");

        // Assert
        result.OriginalTask.Should().Be("Original task description");
    }

    [Fact]
    public async Task GeneratePlanAsync_ShouldSetComplexityToComplex()
    {
        // Arrange
        SetupChatProviderResponse("1. Step one");

        // Act
        var result = await _engine.GeneratePlanAsync("Test task");

        // Assert
        result.Complexity.Should().Be(ComplexityLevel.Complex);
    }

    [Fact]
    public async Task GeneratePlanAsync_ShouldHandleEmptyResponse()
    {
        // Arrange
        SetupChatProviderResponse("");

        // Act
        var result = await _engine.GeneratePlanAsync("Test task");

        // Assert
        result.Steps.Should().BeEmpty();
    }

    [Fact]
    public async Task GeneratePlanAsync_ShouldHandleNonNumberedResponse()
    {
        // Arrange
        SetupChatProviderResponse("Just some text without numbers");

        // Act
        var result = await _engine.GeneratePlanAsync("Test task");

        // Assert
        result.Steps.Should().ContainSingle()
            .Which.Should().Be("Just some text without numbers");
    }

    #endregion

    #region Timeout Tests

    [Fact]
    public async Task GeneratePlanAsync_OnTimeout_ShouldReturnSimplePlan()
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
        var result = await _engine.GeneratePlanAsync("Test task");

        // Assert - Should return fallback simple plan
        result.Steps.Should().ContainSingle()
            .Which.Should().Be("Test task");
        result.Complexity.Should().Be(ComplexityLevel.Simple);
    }

    private static async IAsyncEnumerable<StreamChunk> DelayedAsyncEnumerable([EnumeratorCancellation] CancellationToken ct)
    {
        await Task.Delay(3000, ct); // Delay longer than 2 second timeout
        yield return new StreamChunk(TextDelta: "1. Step", null, null, null, null);
    }

    #endregion

    #region Helper Methods

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

    #region Model Tests

    [Fact]
    public void PlanningConfig_ShouldHaveDefaultValues()
    {
        var config = new PlanningConfig();

        config.ComplexityThreshold.Should().Be(500);
        config.TimeoutSeconds.Should().Be(2);
        config.MinToolCallsForComplex.Should().Be(3);
    }

    [Fact]
    public void TaskPlan_ShouldInitializeWithEmptySteps()
    {
        var plan = new TaskPlan();

        plan.Steps.Should().BeEmpty();
        plan.CurrentStep.Should().Be(0);
        plan.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void TaskPlan_IsComplete_ShouldReturnTrueWhenCurrentStepExceedsStepsCount()
    {
        var plan = new TaskPlan
        {
            Steps = new List<string> { "Step 1", "Step 2" },
            CurrentStep = 2
        };

        plan.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void TaskPlan_IsComplete_ShouldReturnFalseWhenStepsRemain()
    {
        var plan = new TaskPlan
        {
            Steps = new List<string> { "Step 1", "Step 2" },
            CurrentStep = 0
        };

        plan.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void TaskPlan_AdvanceStep_ShouldIncrementCurrentStep()
    {
        var plan = new TaskPlan
        {
            Steps = new List<string> { "Step 1", "Step 2" },
            CurrentStep = 0
        };

        plan.AdvanceStep();

        plan.CurrentStep.Should().Be(1);
        plan.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void TaskPlan_AdvanceStep_ShouldNotExceedStepsCount()
    {
        var plan = new TaskPlan
        {
            Steps = new List<string> { "Step 1" },
            CurrentStep = 0
        };

        plan.AdvanceStep();
        plan.AdvanceStep(); // Should not throw

        plan.CurrentStep.Should().Be(1);
        plan.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void TaskPlan_GetCurrentStepDescription_ShouldReturnCurrentStep()
    {
        var plan = new TaskPlan
        {
            Steps = new List<string> { "Step 1", "Step 2" },
            CurrentStep = 1
        };

        var description = plan.GetCurrentStepDescription();

        description.Should().Be("Step 2");
    }

    [Fact]
    public void TaskPlan_GetCurrentStepDescription_ShouldReturnNullWhenComplete()
    {
        var plan = new TaskPlan
        {
            Steps = new List<string> { "Step 1" },
            CurrentStep = 1
        };

        var description = plan.GetCurrentStepDescription();

        description.Should().BeNull();
    }

    [Fact]
    public void ComplexityLevel_ShouldHaveSimpleAndComplexValues()
    {
        ((int)ComplexityLevel.Simple).Should().Be(0);
        ((int)ComplexityLevel.Complex).Should().Be(1);
    }

    // Interface contract tests - will fail until implementation exists

    [Fact]
    public async Task IPlanningEngine_ShouldHaveAssessComplexityAsyncMethod()
    {
        // This test verifies the interface contract
        var engineMock = new Mock<IPlanningEngine>();
        engineMock
            .Setup(e => e.AssessComplexityAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ComplexityLevel.Simple);

        var engine = engineMock.Object;
        var result = await engine.AssessComplexityAsync("test task");

        result.Should().Be(ComplexityLevel.Simple);
    }

    [Fact]
    public async Task IPlanningEngine_ShouldHaveGeneratePlanAsyncMethod()
    {
        // This test verifies the interface contract
        var engineMock = new Mock<IPlanningEngine>();
        var expectedPlan = new TaskPlan
        {
            Steps = new List<string> { "Step 1", "Step 2" },
            OriginalTask = "test task"
        };
        engineMock
            .Setup(e => e.GeneratePlanAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedPlan);

        var engine = engineMock.Object;
        var result = await engine.GeneratePlanAsync("test task");

        result.Should().NotBeNull();
        result.Steps.Should().HaveCount(2);
    }

    #endregion
}