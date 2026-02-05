using CodeAgentDemo.Core;
using CodeAgentDemo.Models;
using FluentAssertions;
using Xunit;

namespace CodeAgentDemo.Tests.Core;

public class SpectreConsoleUITests
{
    private readonly SpectreConsoleUI _ui;

    public SpectreConsoleUITests()
    {
        _ui = new SpectreConsoleUI();
    }

    [Fact]
    public void ImplementsIConsoleUI()
    {
        _ui.Should().BeAssignableTo<IConsoleUI>();
    }

    [Fact]
    public void PrintBanner_ShouldNotThrow()
    {
        var act = () => _ui.PrintBanner();
        act.Should().NotThrow();
    }

    [Fact]
    public void PrintPrompt_ShouldNotThrow()
    {
        var act = () => _ui.PrintPrompt();
        act.Should().NotThrow();
    }

    [Fact]
    public void PrintSeparator_ShouldNotThrow()
    {
        var act = () => _ui.PrintSeparator();
        act.Should().NotThrow();
    }

    [Fact]
    public void PrintError_ShouldNotThrow()
    {
        var act = () => _ui.PrintError("test error");
        act.Should().NotThrow();
    }

    [Fact]
    public void PrintWarning_ShouldNotThrow()
    {
        var act = () => _ui.PrintWarning("test warning");
        act.Should().NotThrow();
    }

    [Fact]
    public void PrintInfo_ShouldNotThrow()
    {
        var act = () => _ui.PrintInfo("test info");
        act.Should().NotThrow();
    }

    [Fact]
    public void PrintGoodbye_ShouldNotThrow()
    {
        var act = () => _ui.PrintGoodbye();
        act.Should().NotThrow();
    }

    [Fact]
    public void BeginStream_ShouldNotThrow()
    {
        var act = () => _ui.BeginStream();
        act.Should().NotThrow();
    }

    [Fact]
    public void EndStream_ShouldNotThrow()
    {
        var act = () => _ui.EndStream();
        act.Should().NotThrow();
    }

    [Fact]
    public void StreamText_ShouldNotThrow()
    {
        var act = () => _ui.StreamText("hello");
        act.Should().NotThrow();
    }

    [Fact]
    public void StreamText_WithNull_ShouldNotThrow()
    {
        var act = () => _ui.StreamText(null!);
        act.Should().NotThrow();
    }

    [Fact]
    public void StreamThinking_ShouldNotThrow()
    {
        var act = () => _ui.StreamThinking("thinking content");
        act.Should().NotThrow();
    }

    [Fact]
    public void StreamThinking_WithNull_ShouldNotThrow()
    {
        var act = () => _ui.StreamThinking(null!);
        act.Should().NotThrow();
    }

    [Fact]
    public void PrintToolCallStart_ShouldNotThrow()
    {
        var act = () => _ui.PrintToolCallStart("read_file", "{\"path\": \"/test\"}");
        act.Should().NotThrow();
    }

    [Fact]
    public void PrintToolCallDetected_ShouldNotThrow()
    {
        var act = () => _ui.PrintToolCallDetected();
        act.Should().NotThrow();
    }

    [Fact]
    public void PrintToolConfirmation_ShouldNotThrow()
    {
        var act = () => _ui.PrintToolConfirmation("bash");
        act.Should().NotThrow();
    }

    [Fact]
    public void PrintToolExecuting_ShouldNotThrow()
    {
        var act = () => _ui.PrintToolExecuting("bash");
        act.Should().NotThrow();
    }

    [Fact]
    public void PrintToolResult_ShouldNotThrow()
    {
        var act = () => _ui.PrintToolResult("read_file", "file content", true);
        act.Should().NotThrow();
    }

    [Fact]
    public void PrintToolResult_WithFailure_ShouldNotThrow()
    {
        var act = () => _ui.PrintToolResult("bash", "error", false);
        act.Should().NotThrow();
    }

    [Fact]
    public void PrintStats_ShouldNotThrow()
    {
        var act = () => _ui.PrintStats(10, 5);
        act.Should().NotThrow();
    }

    [Fact]
    public void PrintResponseUsage_ShouldNotThrow()
    {
        var start = DateTime.Now;
        var end = start.AddSeconds(2);
        var act = () => _ui.PrintResponseUsage(start, end, 100, 200);
        act.Should().NotThrow();
    }

    [Fact]
    public void PrintResponseUsage_WithoutTokens_ShouldNotThrow()
    {
        var start = DateTime.Now;
        var end = start.AddSeconds(2);
        var act = () => _ui.PrintResponseUsage(start, end, null, null);
        act.Should().NotThrow();
    }

    [Fact]
    public void DisplaySessionHistory_WithEmptyList_ShouldNotThrow()
    {
        var act = () => _ui.DisplaySessionHistory(new List<ChatMessage>());
        act.Should().NotThrow();
    }

    [Fact]
    public void DisplaySessionHistory_WithMessages_ShouldNotThrow()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, new[] { new TextBlock("Hello") }),
            new(ChatRole.Assistant, new[] { new TextBlock("Hi there!") })
        };

        var act = () => _ui.DisplaySessionHistory(messages);
        act.Should().NotThrow();
    }
}