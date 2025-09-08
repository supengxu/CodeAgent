using System.Text.Json;
using CodeAgentDemo.Tools;
using FluentAssertions;
using Xunit;

namespace CodeAgentDemo.Tests.Tools;

public class BashToolTests
{
    private readonly BashTool _tool = new(Path.GetTempPath());

    [Fact]
    public void Name_ShouldBeBash()
    {
        _tool.Name.Should().Be("bash");
    }

    [Fact]
    public void Description_ShouldBeSet()
    {
        _tool.Description.Should().Contain("bash command");
    }

    [Fact]
    public void InputSchema_ShouldBeValidJson()
    {
        var schema = _tool.InputSchema;

        schema.ValueKind.Should().Be(JsonValueKind.Object);
        schema.TryGetProperty("properties", out _).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithValidCommand_ShouldReturnSuccess()
    {
        var args = JsonDocument.Parse("{\"command\":\"echo hello\"}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeTrue();
        result.Output.Should().Contain("hello");
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyCommand_ShouldReturnFailure()
    {
        var args = JsonDocument.Parse("{\"command\":\"\"}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeFalse();
        result.Output.Should().Contain("required");
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingCommand_ShouldReturnFailure()
    {
        var args = JsonDocument.Parse("{}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeFalse();
        result.Output.Should().Contain("required");
    }

    [Fact]
    public async Task ExecuteAsync_WithFailingCommand_ShouldReturnSuccess()
    {
        var args = JsonDocument.Parse("{\"command\":\"exit 0\"}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeTrue();
        result.Output.Should().Be("(no output)");
    }

    [Fact]
    public async Task ExecuteAsync_WithStderr_ShouldIncludeError()
    {
        var args = JsonDocument.Parse("{\"command\":\"ls /nonexistent_directory_12345\"}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeFalse();
        result.Output.Should().Contain("Error:");
    }

    [Fact]
    public async Task ExecuteAsync_WithBothStdoutAndStderr_ShouldIncludeBoth()
    {
        var args = JsonDocument.Parse("{\"command\":\"echo output; ls /nonexistent_12345\"}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeFalse();
        result.Output.Should().Contain("output");
        result.Output.Should().Contain("Error:");
    }

    [Fact]
    public async Task ExecuteAsync_WithQuotesInCommand_ShouldWork()
    {
        var args = JsonDocument.Parse("{\"command\":\"echo \\\"hello world\\\"\"}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeTrue();
        result.Output.Should().Contain("hello world");
    }

    [Fact]
    public async Task ExecuteAsync_WithTooLongCommand_ShouldReturnFailure()
    {
        var longCommand = new string('a', 11000);
        var args = JsonDocument.Parse($"{{\"command\":\"{longCommand}\"}}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeFalse();
        result.Output.Should().Contain("too long");
    }

    [Fact]
    public async Task ExecuteAsync_WithDangerousPattern_ShouldReturnFailure()
    {
        var args = JsonDocument.Parse("{\"command\":\"rm -rf /\"}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeFalse();
        result.Output.Should().Contain("blocked pattern");
    }

    [Fact]
    public void Constructor_WithInvalidWorkDir_ShouldThrow()
    {
        var act = () => new BashTool("/nonexistent/path");

        act.Should().Throw<DirectoryNotFoundException>();
    }

    [Fact]
    public void Constructor_WithNullWorkDir_ShouldThrow()
    {
        var act = () => new BashTool(null!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithEmptyWorkDir_ShouldThrow()
    {
        var act = () => new BashTool("");

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("ls", false)]
    [InlineData("cat file.txt", false)]
    [InlineData("rm file.txt", false)]
    [InlineData("mkdir newdir", false)]
    [InlineData("touch newfile", false)]
    [InlineData("echo hello", false)]
    [InlineData("git status", false)]
    [InlineData("dotnet build", false)]
    [InlineData("vim file.txt", false)]
    [InlineData("ls -la", false)]
    public void RequiresConfirmation_WithSafeCommands_ShouldReturnFalse(string command, bool expected)
    {
        var args = JsonDocument.Parse($"{{\"command\":\"{command}\"}}").RootElement;

        var result = _tool.RequiresConfirmation(args);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("rm -rf /", true)]
    [InlineData("cat /etc/passwd", true)]
    [InlineData("ls ..", true)]
    [InlineData("ls /tmp", true)]
    [InlineData("rm -rf *", true)]
    [InlineData("sudo apt install", true)]
    [InlineData("curl http://example.com", true)]
    public void RequiresConfirmation_WithUnsafeCommands_ShouldReturnTrue(string command, bool expected)
    {
        var args = JsonDocument.Parse($"{{\"command\":\"{command.Replace("\"", "\\\"")}\"}}").RootElement;

        var result = _tool.RequiresConfirmation(args);

        result.Should().Be(expected);
    }

    [Fact]
    public void RequiresConfirmation_WithMissingCommand_ShouldReturnTrue()
    {
        var args = JsonDocument.Parse("{}").RootElement;

        var result = _tool.RequiresConfirmation(args);

        result.Should().BeTrue();
    }

    [Fact]
    public void RequiresConfirmation_WithEmptyCommand_ShouldReturnTrue()
    {
        var args = JsonDocument.Parse("{\"command\":\"\"}").RootElement;

        var result = _tool.RequiresConfirmation(args);

        result.Should().BeTrue();
    }
}