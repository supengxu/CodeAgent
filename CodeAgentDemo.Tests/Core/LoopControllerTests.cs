using CodeAgentDemo.Core;
using CodeAgentDemo.Models;
using FluentAssertions;
using Xunit;

namespace CodeAgentDemo.Tests.Core;

public class LoopControllerTests
{
    // These tests will fail until LoopController is implemented (TDD: RED phase)

    [Fact]
    public void State_ShouldHaveDefaultValues()
    {
        // Arrange
        var config = new LoopControlConfig();
        var controller = new LoopController(config);

        // Assert
        controller.State.IterationCount.Should().Be(0);
        controller.State.TokenUsage.Should().Be(0);
        controller.State.DetectedCycle.Should().BeFalse();
        controller.State.IterationLimitReached.Should().BeFalse();
        controller.State.TokenLimitReached.Should().BeFalse();
    }

    [Fact]
    public async Task CheckIterationAsync_ShouldIncrementIterationCount()
    {
        // Arrange
        var config = new LoopControlConfig { MaxIterations = 5 };
        var controller = new LoopController(config);

        // Act
        await controller.CheckIterationAsync();

        // Assert
        controller.State.IterationCount.Should().Be(1);
    }

    [Fact]
    public async Task CheckIterationAsync_ShouldReturnTrue_WhenUnderLimit()
    {
        // Arrange
        var config = new LoopControlConfig { MaxIterations = 5 };
        var controller = new LoopController(config);

        // Act
        var result = await controller.CheckIterationAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CheckIterationAsync_ShouldReturnFalse_WhenLimitReached()
    {
        // Arrange
        var config = new LoopControlConfig { MaxIterations = 2 };
        var controller = new LoopController(config);

        // Act - First iteration
        await controller.CheckIterationAsync();
        // Act - Second iteration
        await controller.CheckIterationAsync();
        // Act - Third iteration (should fail)
        var result = await controller.CheckIterationAsync();

        // Assert
        result.Should().BeFalse();
        controller.State.IterationLimitReached.Should().BeTrue();
    }

    [Fact]
    public void DetectCycle_ShouldReturnFalse_ForFirstOccurrence()
    {
        // Arrange
        var config = new LoopControlConfig { CycleDetectionWindow = 5 };
        var controller = new LoopController(config);
        var hash = controller.GetStateHash("state1");

        // Act
        var result = controller.DetectCycle(hash);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void DetectCycle_ShouldReturnTrue_WhenSameStateRepeats()
    {
        // Arrange
        var config = new LoopControlConfig { CycleDetectionWindow = 5 };
        var controller = new LoopController(config);
        var hash = controller.GetStateHash("same_state");

        // Act
        controller.DetectCycle(hash);
        var result = controller.DetectCycle(hash);

        // Assert
        result.Should().BeTrue();
        controller.State.DetectedCycle.Should().BeTrue();
    }

    [Fact]
    public void DetectCycle_ShouldRespectWindowSize()
    {
        // Arrange
        var config = new LoopControlConfig { CycleDetectionWindow = 3 };
        var controller = new LoopController(config);

        // Act - Add 4 different states (window size is 3)
        controller.DetectCycle(controller.GetStateHash("state1"));
        controller.DetectCycle(controller.GetStateHash("state2"));
        controller.DetectCycle(controller.GetStateHash("state3"));
        controller.DetectCycle(controller.GetStateHash("state4"));

        // Now check if state1 (outside window) causes cycle detection
        var result = controller.DetectCycle(controller.GetStateHash("state1"));

        // Assert - Should not detect cycle because state1 is outside the window
        result.Should().BeFalse();
    }

    [Fact]
    public void GetStateHash_ShouldReturnConsistentHash_ForSameInput()
    {
        // Arrange
        var config = new LoopControlConfig();
        var controller = new LoopController(config);

        // Act
        var hash1 = controller.GetStateHash("test_state");
        var hash2 = controller.GetStateHash("test_state");

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void GetStateHash_ShouldReturnDifferentHash_ForDifferentInput()
    {
        // Arrange
        var config = new LoopControlConfig();
        var controller = new LoopController(config);

        // Act
        var hash1 = controller.GetStateHash("state1");
        var hash2 = controller.GetStateHash("state2");

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void UpdateTokenUsage_ShouldUpdateState()
    {
        // Arrange
        var config = new LoopControlConfig();
        var controller = new LoopController(config);

        // Act
        controller.UpdateTokenUsage(1000);

        // Assert
        controller.State.TokenUsage.Should().Be(1000);
    }

    [Fact]
    public async Task CheckIterationAsync_ShouldCheckTokenLimit()
    {
        // Arrange
        var config = new LoopControlConfig { TokenLimitRatio = 0.8 };
        var controller = new LoopController(config, maxTokens: 1000);

        // Act - Set token usage to 90% (above 80% threshold)
        controller.UpdateTokenUsage(900);
        var result = await controller.CheckIterationAsync();

        // Assert
        controller.State.TokenLimitReached.Should().BeTrue();
    }

    [Fact]
    public void Reset_ShouldClearAllState()
    {
        // Arrange
        var config = new LoopControlConfig();
        var controller = new LoopController(config);
        controller.UpdateTokenUsage(500);
        controller.DetectCycle(controller.GetStateHash("test"));

        // Act
        controller.Reset();

        // Assert
        controller.State.IterationCount.Should().Be(0);
        controller.State.TokenUsage.Should().Be(0);
        controller.State.DetectedCycle.Should().BeFalse();
        controller.State.IterationLimitReached.Should().BeFalse();
        controller.State.TokenLimitReached.Should().BeFalse();
    }
}