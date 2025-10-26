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

public class ReflectionEngineTests
{
    private readonly Mock<IChatProvider> _chatProviderMock;
    private readonly ReflectionConfig _config;
    private readonly ReflectionEngine _engine;

    public ReflectionEngineTests()
    {
        _chatProviderMock = new Mock<IChatProvider>();
        _config = new ReflectionConfig();
        _engine = new ReflectionEngine(_chatProviderMock.Object, _config, 100000);
    }

    #region ShouldReflectAsync Tests

    [Fact]
    public async Task ShouldReflectAsync_WithFailedResult_ShouldReturnTrue()
    {
        // Arrange
        var failedResult = new ToolResult(false, "Error occurred");

        // Act
        var result = await _engine.ShouldReflectAsync(failedResult, 1);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ShouldReflectAsync_WithSuccessResult_ShouldReturnFalse()
    {
        // Arrange
        var successResult = new ToolResult(true, "Success");

        // Act
        var result = await _engine.ShouldReflectAsync(successResult, 1);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ShouldReflectAsync_WhenMaxAttemptsReached_ShouldReturnFalse()
    {
        // Arrange
        var failedResult = new ToolResult(false, "Error occurred");
        var maxAttempts = 3;

        // Act
        var result = await _engine.ShouldReflectAsync(failedResult, maxAttempts);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ShouldReflectAsync_WhenAttemptExceedsMaxAttempts_ShouldReturnFalse()
    {
        // Arrange
        var failedResult = new ToolResult(false, "Error occurred");

        // Act
        var result = await _engine.ShouldReflectAsync(failedResult, 4);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ShouldReflectAsync_WhenTokenBudgetExceeded_ShouldReturnFalse()
    {
        // Arrange
        // Use totalTokenBudget of 600, which gives _tokenBudget = 120 (600 * 0.2)
        // This is enough for ReflectAsync to proceed (MinReflectionTokens = 100)
        var smallBudgetEngine = new ReflectionEngine(_chatProviderMock.Object, _config, 600);
        var failedResult = new ToolResult(false, "Error occurred");

        // Simulate token usage by calling ReflectAsync first
        // UsageInfo(100, 50) = 150 tokens, which exceeds _tokenBudget of 120
        SetupChatProviderResponse(@"{""analysis"": ""Test"", ""suggestions"": [], ""shouldRetry"": true}");
        var context = new ReflectionContext
        {
            ToolName = "test_tool",
            Arguments = JsonDocument.Parse("{}").RootElement,
            ErrorMessage = "Error",
            AttemptNumber = 1
        };
        await smallBudgetEngine.ReflectAsync(context);

        // Act - Now token budget should be exceeded (150 tokens used out of 120 budget)
        var result = await smallBudgetEngine.ShouldReflectAsync(failedResult, 1);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region ReflectAsync Tests

    [Fact]
    public async Task ReflectAsync_ShouldReturnAnalysisAndSuggestions()
    {
        // Arrange
        var context = new ReflectionContext
        {
            ToolName = "test_tool",
            Arguments = JsonDocument.Parse("{}").RootElement,
            ErrorMessage = "Tool execution failed",
            AttemptNumber = 1
        };
        SetupChatProviderResponse(@"{
            ""analysis"": ""The tool failed due to invalid arguments"",
            ""suggestions"": [""Check argument format"", ""Verify input data""],
            ""shouldRetry"": true
        }");

        // Act
        var result = await _engine.ReflectAsync(context);

        // Assert
        result.Analysis.Should().NotBeNullOrEmpty();
        result.Suggestions.Should().NotBeEmpty();
        result.ShouldRetry.Should().BeTrue();
    }

    [Fact]
    public async Task ReflectAsync_WithUnrecoverableError_ShouldReturnShouldRetryFalse()
    {
        // Arrange
        var context = new ReflectionContext
        {
            ToolName = "test_tool",
            Arguments = JsonDocument.Parse("{}").RootElement,
            ErrorMessage = "Fatal error: resource not found",
            AttemptNumber = 3
        };
        SetupChatProviderResponse(@"{
            ""analysis"": ""Resource does not exist and cannot be recovered"",
            ""suggestions"": [],
            ""shouldRetry"": false
        }");

        // Act
        var result = await _engine.ReflectAsync(context);

        // Assert
        result.ShouldRetry.Should().BeFalse();
    }

    [Fact]
    public async Task ReflectAsync_WhenMaxAttemptsReached_ShouldReturnShouldRetryFalse()
    {
        // Arrange
        var context = new ReflectionContext
        {
            ToolName = "test_tool",
            Arguments = JsonDocument.Parse("{}").RootElement,
            ErrorMessage = "Error",
            AttemptNumber = 3
        };
        SetupChatProviderResponse(@"{
            ""analysis"": ""Test analysis"",
            ""suggestions"": [],
            ""shouldRetry"": true
        }");

        // Act
        var result = await _engine.ReflectAsync(context);

        // Assert - ShouldRetry should be false because max attempts reached
        result.ShouldRetry.Should().BeFalse();
    }

    [Fact]
    public async Task ReflectAsync_WhenTokenBudgetExceeded_ShouldReturnShouldRetryFalse()
    {
        // Arrange
        var smallBudgetEngine = new ReflectionEngine(_chatProviderMock.Object, _config, 10);
        var context = new ReflectionContext
        {
            ToolName = "test_tool",
            Arguments = JsonDocument.Parse("{}").RootElement,
            ErrorMessage = "Error",
            AttemptNumber = 1
        };

        // Act
        var result = await smallBudgetEngine.ReflectAsync(context);

        // Assert
        result.ShouldRetry.Should().BeFalse();
        result.Analysis.Should().Contain("Token budget exceeded");
    }

    [Fact]
    public async Task ReflectAsync_ShouldCallChatProviderWithCorrectMessages()
    {
        // Arrange
        var context = new ReflectionContext
        {
            ToolName = "read_file",
            Arguments = JsonDocument.Parse("{\"path\": \"/test/file.txt\"}").RootElement,
            ErrorMessage = "File not found",
            AttemptNumber = 1
        };
        SetupChatProviderResponse(@"{""analysis"": ""Test"", ""suggestions"": [], ""shouldRetry"": false}");

        // Act
        await _engine.ReflectAsync(context);

        // Assert
        _chatProviderMock.Verify(
            p => p.CompleteStreamingAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ReflectAsync_WhenCancelled_ShouldReturnCancellationResult()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var context = new ReflectionContext
        {
            ToolName = "test_tool",
            Arguments = JsonDocument.Parse("{}").RootElement,
            ErrorMessage = "Error",
            AttemptNumber = 1
        };

        // Act
        var result = await _engine.ReflectAsync(context, cts.Token);

        // Assert
        result.ShouldRetry.Should().BeFalse();
        result.Analysis.Should().Contain("cancelled");
    }

    #endregion

    #region ReflectionConfig Tests

    [Fact]
    public void ReflectionConfig_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var config = new ReflectionConfig();

        // Assert
        config.MaxRetryAttempts.Should().Be(3);
        config.TokenBudgetRatio.Should().Be(0.2);
    }

    [Fact]
    public void ReflectionConfig_ShouldAllowCustomValues()
    {
        // Arrange & Act
        var config = new ReflectionConfig
        {
            MaxRetryAttempts = 5,
            TokenBudgetRatio = 0.3
        };

        // Assert
        config.MaxRetryAttempts.Should().Be(5);
        config.TokenBudgetRatio.Should().Be(0.3);
    }

    #endregion

    #region ReflectionResult Tests

    [Fact]
    public void ReflectionResult_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var result = new ReflectionResult();

        // Assert
        result.Analysis.Should().BeEmpty();
        result.Suggestions.Should().BeEmpty();
        result.ShouldRetry.Should().BeFalse();
    }

    [Fact]
    public void ReflectionResult_ShouldAllowInit()
    {
        // Arrange & Act
        var result = new ReflectionResult
        {
            Analysis = "Test analysis",
            Suggestions = ["Suggestion 1", "Suggestion 2"],
            ShouldRetry = true
        };

        // Assert
        result.Analysis.Should().Be("Test analysis");
        result.Suggestions.Should().HaveCount(2);
        result.ShouldRetry.Should().BeTrue();
    }

    #endregion

    #region ReflectionContext Tests

    [Fact]
    public void ReflectionContext_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var context = new ReflectionContext();

        // Assert
        context.ToolName.Should().BeEmpty();
        context.ErrorMessage.Should().BeEmpty();
        context.AttemptNumber.Should().Be(0);
    }

    [Fact]
    public void ReflectionContext_ShouldAllowInit()
    {
        // Arrange & Act
        var context = new ReflectionContext
        {
            ToolName = "test_tool",
            Arguments = JsonDocument.Parse("{\"key\": \"value\"}").RootElement,
            ErrorMessage = "Test error",
            AttemptNumber = 2
        };

        // Assert
        context.ToolName.Should().Be("test_tool");
        context.ErrorMessage.Should().Be("Test error");
        context.AttemptNumber.Should().Be(2);
    }

    #endregion

    #region Token Budget Tests

    [Fact]
    public void ReflectionEngine_ShouldCalculateTokenBudgetFromRatio()
    {
        // Arrange
        var config = new ReflectionConfig { TokenBudgetRatio = 0.2 };
        var engine = new ReflectionEngine(_chatProviderMock.Object, config, 100000);

        // Assert
        engine.TokenBudget.Should().Be(20000); // 20% of 100000
    }

    [Fact]
    public async Task ReflectionEngine_ResetTokenUsage_ShouldResetCounter()
    {
        // Arrange
        SetupChatProviderResponse(@"{""analysis"": ""Test"", ""suggestions"": [], ""shouldRetry"": true}");
        var context = new ReflectionContext
        {
            ToolName = "test_tool",
            Arguments = JsonDocument.Parse("{}").RootElement,
            ErrorMessage = "Error",
            AttemptNumber = 1
        };
        _ = await _engine.ReflectAsync(context);

        // Act
        _engine.ResetTokenUsage();

        // Assert
        _engine.TokenUsage.Should().Be(0);
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
}