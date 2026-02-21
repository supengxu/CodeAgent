using System.Text.Json;
using CodeAgentDemo.Cli;
using CodeAgentDemo.Core;
using CodeAgentDemo.Models;
using CodeAgentDemo.Providers;
using CodeAgentDemo.Tools;
using Moq;

namespace CodeAgentDemo.Tests;

public static class TestHelper
{
    public static IToolRegistry CreateToolRegistry()
    {
        var tools = new ToolRegistry();
        var toolMock = new Mock<ITool>();
        toolMock.SetupGet(t => t.Name).Returns("test_tool");
        toolMock.SetupGet(t => t.Description).Returns("Test tool for tests");
        toolMock.SetupGet(t => t.InputSchema).Returns(JsonDocument.Parse("{\"type\":\"object\"}").RootElement);
        toolMock.Setup(t => t.ExecuteAsync(It.IsAny<JsonElement>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResult(true, "Tool executed successfully"));
        toolMock.Setup(t => t.RequiresConfirmation(It.IsAny<JsonElement>()))
            .Returns(false);
        tools.Register(toolMock.Object);
        return tools;
    }

    public static IConsoleUI CreateConsoleUI() => new ConsoleUI();
    public static IConsoleIO CreateConsoleIO() => new Mock<IConsoleIO>().Object;

    public static ISessionCli CreateSessionCli(string? sessionsDir = null)
    {
        var dir = sessionsDir ?? Path.Combine(Path.GetTempPath(), $"test_sessions_{Guid.NewGuid()}");
        var cli = new SessionCli(dir);
        cli.InitializeAsync().GetAwaiter().GetResult();
        return cli;
    }

    public static IMessageHandler CreateMessageHandler(ISessionCli sessionCli) => new MessageHandler(sessionCli);

    public static IToolExecutor CreateToolExecutor(IToolRegistry tools, IConsoleUI ui, IConsoleIO console) =>
        new ToolExecutor(tools, ui, console);

    public static IStreamProcessor CreateStreamProcessor(IConsoleUI ui) => new StreamProcessor(ui);

    public static ILoopController CreateLoopController(LoopControlConfig? config = null, int maxTokens = 0) =>
        new LoopController(config ?? new LoopControlConfig(), maxTokens);

    public static ILoopManager CreateLoopManager(LoopControlConfig? config = null, int maxTokens = 0)
    {
        var controller = CreateLoopController(config, maxTokens);
        return new LoopManager(controller, CreateConsoleUI());
    }

    public static AgentLoop CreateAgentLoop(
        IChatProvider? provider = null,
        IToolRegistry? tools = null,
        ChatOptions? options = null,
        IConsoleIO? console = null,
        ISessionCli? sessionCli = null,
        IConsoleUI? ui = null,
        ILoopManager? loopManager = null,
        IPlanningEngine? planningEngine = null,
        IContextManager? contextManager = null,
        string? sessionsDir = null)
    {
        var actualTools = tools ?? CreateToolRegistry();
        var actualConsole = console ?? CreateConsoleIO();
        var actualSessionCli = sessionCli ?? CreateSessionCli(sessionsDir);
        var actualUi = ui ?? CreateConsoleUI();

        return new AgentLoop(
            provider ?? new Mock<IChatProvider>().Object,
            actualTools,
            options ?? new ChatOptions(),
            actualConsole,
            actualSessionCli,
            actualUi,
            CreateMessageHandler(actualSessionCli),
            CreateToolExecutor(actualTools, actualUi, actualConsole),
            CreateStreamProcessor(actualUi),
            loopManager,
            planningEngine,
            contextManager
        );
    }
}