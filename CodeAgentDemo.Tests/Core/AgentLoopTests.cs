using System.Text.Json;
using CodeAgentDemo.Cli;
using CodeAgentDemo.Core;
using CodeAgentDemo.Models;
using CodeAgentDemo.Providers;
using CodeAgentDemo.Tools;
using FluentAssertions;
using Moq;
using Xunit;

namespace CodeAgentDemo.Tests.Core;

public class AgentLoopTests
{
    private static IToolRegistry CreateToolRegistry() => new ToolRegistry();
    private static IConsoleUI CreateConsoleUI() => new ConsoleUI();
    private static ISessionCli CreateSessionCli(string sessionsDir)
    {
        var cli = new SessionCli(sessionsDir);
        cli.InitializeAsync().GetAwaiter().GetResult();
        return cli;
    }

    [Fact]
    public void Constructor_WithNullProvider_ShouldThrowArgumentNullException()
    {
        var tools = CreateToolRegistry();
        var options = new ChatOptions();
        var console = new Mock<IConsoleIO>().Object;
        var sessionsDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        var act = () => new AgentLoop(null!, tools, options, CreateSessionCli(sessionsDir), CreateConsoleUI());

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("provider");
    }

    [Fact]
    public void Constructor_WithNullTools_ShouldThrowArgumentNullException()
    {
        var provider = new Mock<IChatProvider>().Object;
        var options = new ChatOptions();
        var sessionsDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        var act = () => new AgentLoop(provider, null!, options, CreateSessionCli(sessionsDir), CreateConsoleUI());

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("tools");
    }

    [Fact]
    public void Constructor_WithNullOptions_ShouldUseDefaultOptions()
    {
        var provider = new Mock<IChatProvider>().Object;
        var tools = CreateToolRegistry();
        var sessionsDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        var loop = new AgentLoop(provider, tools, null!, CreateSessionCli(sessionsDir), CreateConsoleUI());

        loop.Should().NotBeNull();
    }

    [Fact]
    public async Task SendMessageAsync_WithEmptyMessage_ShouldThrow()
    {
        var providerMock = new Mock<IChatProvider>();
        providerMock.SetupGet(p => p.ProviderName).Returns("Test");
        var tools = CreateToolRegistry();
        var options = new ChatOptions();
        var sessionsDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var loop = new AgentLoop(providerMock.Object, tools, options, CreateSessionCli(sessionsDir), CreateConsoleUI());

        var act = async () => await loop.SendMessageAsync("");
        await act.Should().ThrowAsync<ArgumentException>();

        var act2 = async () => await loop.SendMessageAsync("   ");
        await act2.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void RegisterTool_ShouldAddToRegistry()
    {
        var providerMock = new Mock<IChatProvider>();
        providerMock.SetupGet(p => p.ProviderName).Returns("Test");
        var toolMock = new Mock<ITool>();
        toolMock.SetupGet(t => t.Name).Returns("test");
        var tools = CreateToolRegistry();
        var options = new ChatOptions();
        var sessionsDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var loop = new AgentLoop(providerMock.Object, tools, options, CreateSessionCli(sessionsDir), CreateConsoleUI());

        loop.RegisterTool(toolMock.Object);

        tools.GetTool("test").Should().NotBeNull();
    }

    [Fact]
    public void History_ShouldReturnMessages()
    {
        var providerMock = new Mock<IChatProvider>();
        providerMock.SetupGet(p => p.ProviderName).Returns("Test");
        var tools = CreateToolRegistry();
        var options = new ChatOptions();
        var sessionsDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var loop = new AgentLoop(providerMock.Object, tools, options, CreateSessionCli(sessionsDir), CreateConsoleUI());

        loop.History.Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_WithQuitCommand_ShouldExit()
    {
        var providerMock = new Mock<IChatProvider>();
        providerMock.SetupGet(p => p.ProviderName).Returns("Test");
        var consoleMock = new Mock<IConsoleIO>();
        consoleMock.SetupSequence(c => c.ReadLine()).Returns("quit");

        var tools = CreateToolRegistry();
        var options = new ChatOptions();
        var sessionsDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var sessionCli = CreateSessionCli(sessionsDir);
        var loop = new AgentLoop(providerMock.Object, tools, options, consoleMock.Object, sessionCli, CreateConsoleUI());

        await loop.RunAsync();

        consoleMock.Verify(c => c.ReadLine(), Times.Once);
    }

    [Fact]
    public async Task RunAsync_WithExitCommand_ShouldExit()
    {
        var providerMock = new Mock<IChatProvider>();
        providerMock.SetupGet(p => p.ProviderName).Returns("Test");
        var consoleMock = new Mock<IConsoleIO>();
        consoleMock.SetupSequence(c => c.ReadLine()).Returns("exit");

        var tools = CreateToolRegistry();
        var options = new ChatOptions();
        var sessionsDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var sessionCli = CreateSessionCli(sessionsDir);
        var loop = new AgentLoop(providerMock.Object, tools, options, consoleMock.Object, sessionCli, CreateConsoleUI());

        await loop.RunAsync();

        consoleMock.Verify(c => c.ReadLine(), Times.Once);
    }

    [Fact]
    public async Task RunAsync_WithEmptyInput_ShouldContinue()
    {
        var providerMock = new Mock<IChatProvider>();
        providerMock.SetupGet(p => p.ProviderName).Returns("Test");
        var consoleMock = new Mock<IConsoleIO>();
        consoleMock.SetupSequence(c => c.ReadLine())
            .Returns("")
            .Returns("   ")
            .Returns("quit");

        var tools = CreateToolRegistry();
        var options = new ChatOptions();
        var sessionsDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var sessionCli = CreateSessionCli(sessionsDir);
        var loop = new AgentLoop(providerMock.Object, tools, options, consoleMock.Object, sessionCli, CreateConsoleUI());

        await loop.RunAsync();

        consoleMock.Verify(c => c.ReadLine(), Times.Exactly(3));
    }
}

public class AgentResponseTests
{
    [Fact]
    public void Constructor_ShouldSetProperties()
    {
        var content = new ContentBlock[] { new TextBlock("Hello") };

        var response = new AgentResponse(content, "end_turn");

        response.Content.Should().HaveCount(1);
        response.StopReason.Should().Be("end_turn");
    }

    [Fact]
    public void Constructor_WithEmptyContent_ShouldWork()
    {
        var response = new AgentResponse(Array.Empty<ContentBlock>(), "end_turn");

        response.Content.Should().BeEmpty();
    }
}

public class DefaultConsoleIOTests
{
    [Fact]
    public void Write_ShouldNotThrow()
    {
        var console = new DefaultConsoleIO();
        var act = () => console.Write("test");
        act.Should().NotThrow();
    }

    [Fact]
    public void WriteLine_WithNull_ShouldNotThrow()
    {
        var console = new DefaultConsoleIO();
        var act = () => console.WriteLine(null);
        act.Should().NotThrow();
    }

    [Fact]
    public void WriteLine_WithValue_ShouldNotThrow()
    {
        var console = new DefaultConsoleIO();
        var act = () => console.WriteLine("test");
        act.Should().NotThrow();
    }
}

public class IAgentLoopTests
{
    [Fact]
    public void IAgentLoop_ShouldHaveHistoryProperty()
    {
        typeof(IAgentLoop).GetProperty("History").Should().NotBeNull();
    }

    [Fact]
    public void IAgentLoop_ShouldHaveSendMessageAsyncMethod()
    {
        typeof(IAgentLoop).GetMethod("SendMessageAsync").Should().NotBeNull();
    }

    [Fact]
    public void IAgentLoop_ShouldHaveRegisterToolMethod()
    {
        typeof(IAgentLoop).GetMethod("RegisterTool").Should().NotBeNull();
    }
}

public class IConsoleIOTests
{
    [Fact]
    public void IConsoleIO_ShouldHaveWriteMethod()
    {
        typeof(IConsoleIO).GetMethod("Write").Should().NotBeNull();
    }

    [Fact]
    public void IConsoleIO_ShouldHaveWriteLineMethod()
    {
        typeof(IConsoleIO).GetMethod("WriteLine").Should().NotBeNull();
    }

    [Fact]
    public void IConsoleIO_ShouldHaveReadLineMethod()
    {
        typeof(IConsoleIO).GetMethod("ReadLine").Should().NotBeNull();
    }
}