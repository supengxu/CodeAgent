using CodeAgentDemo.Cli;
using CodeAgentDemo.Core;
using CodeAgentDemo.Models;
using FluentAssertions;
using System.IO;
using Xunit;

namespace CodeAgentDemo.Tests.Cli;

public class SessionCliTests : IDisposable
{
    private readonly string _testDir;
    private readonly SessionCli _sessionCli;

    public SessionCliTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"session-cli-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _sessionCli = new SessionCli(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    [Fact]
    public async Task InitializeAsync_ShouldCreateDefaultSession()
    {
        await _sessionCli.InitializeAsync();
        
        _sessionCli.CurrentSession.Should().NotBeNull();
        _sessionCli.CurrentSessionId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task TryExecuteCommandAsync_WithNonSlashInput_ShouldReturnNotACommand()
    {
        var result = await _sessionCli.TryExecuteCommandAsync("hello world");
        
        result.IsCommand.Should().BeFalse();
    }

    [Fact]
    public async Task TryExecuteCommandAsync_WithSlashInput_ShouldReturnIsCommand()
    {
        var result = await _sessionCli.TryExecuteCommandAsync("/help");
        
        result.IsCommand.Should().BeTrue();
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task NewCommand_ShouldCreateNewSession()
    {
        await _sessionCli.InitializeAsync();
        var oldSessionId = _sessionCli.CurrentSessionId;
        
        var result = await _sessionCli.TryExecuteCommandAsync("/new test-session");
        
        result.IsCommand.Should().BeTrue();
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("test-session");
        _sessionCli.CurrentSessionId.Should().NotBe(oldSessionId);
    }

    [Fact]
    public async Task ListCommand_ShouldReturnSessionList()
    {
        await _sessionCli.InitializeAsync();
        
        var result = await _sessionCli.TryExecuteCommandAsync("/list");
        
        result.IsCommand.Should().BeTrue();
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Available sessions");
    }

    [Fact]
    public async Task ContextCommand_ShouldReturnSessionStats()
    {
        await _sessionCli.InitializeAsync();
        
        var result = await _sessionCli.TryExecuteCommandAsync("/context");
        
        result.IsCommand.Should().BeTrue();
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Current Session:");
        result.Message.Should().Contain("Messages:");
    }

    [Fact]
    public async Task HelpCommand_ShouldReturnHelpText()
    {
        var result = await _sessionCli.TryExecuteCommandAsync("/help");
        
        result.IsCommand.Should().BeTrue();
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("/new");
        result.Message.Should().Contain("/switch");
        result.Message.Should().Contain("/list");
    }

    [Fact]
    public async Task SwitchCommand_WithInvalidId_ShouldReturnError()
    {
        await _sessionCli.InitializeAsync();
        
        var result = await _sessionCli.TryExecuteCommandAsync("/switch nonexistent");
        
        result.IsCommand.Should().BeTrue();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task UnknownCommand_ShouldReturnError()
    {
        var result = await _sessionCli.TryExecuteCommandAsync("/unknown");
        
        result.IsCommand.Should().BeTrue();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Unknown command");
    }

    [Fact]
    public async Task ClearCommand_WithoutForce_ShouldAskForConfirmation()
    {
        await _sessionCli.InitializeAsync();
        
        var result = await _sessionCli.TryExecuteCommandAsync("/clear");
        
        result.IsCommand.Should().BeTrue();
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("--force");
    }

    [Fact]
    public async Task AppendMessageAsync_ShouldAddMessageToSession()
    {
        await _sessionCli.InitializeAsync();
        var message = ChatMessage.CreateText(ChatRole.User, "Hello");
        
        await _sessionCli.AppendMessageAsync(message);
        
        _sessionCli.CurrentSession.Count.Should().Be(1);
    }

    [Fact]
    public async Task SwitchCommand_WithPrefixMatch_ShouldFindSession()
    {
        await _sessionCli.InitializeAsync();
        await _sessionCli.TryExecuteCommandAsync("/new my-test-session");
        
        var result = await _sessionCli.TryExecuteCommandAsync("/switch my-test");
        
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task LsAlias_ShouldWorkSameAsList()
    {
        await _sessionCli.InitializeAsync();
        
        var result = await _sessionCli.TryExecuteCommandAsync("/ls");
        
        result.IsCommand.Should().BeTrue();
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task CtxAlias_ShouldWorkSameAsContext()
    {
        await _sessionCli.InitializeAsync();
        
        var result = await _sessionCli.TryExecuteCommandAsync("/ctx");
        
        result.IsCommand.Should().BeTrue();
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Current Session:");
    }
}