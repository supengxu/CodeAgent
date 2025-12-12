using CodeAgentDemo.Cli;
using CodeAgentDemo.Core;
using CodeAgentDemo.Models;
using CodeAgentDemo.Providers;
using CodeAgentDemo.Tools;
using DotNetEnv;
using Microsoft.Extensions.DependencyInjection;

var envPaths = new[]
{
    Path.Combine(Directory.GetCurrentDirectory(), "CodeAgentDemo", ".env"),
    Path.Combine(Directory.GetCurrentDirectory(), ".env"),
    Path.Combine(AppContext.BaseDirectory, ".env")
};

foreach (var envPath in envPaths)
{
    if (File.Exists(envPath))
    {
        Env.Load(envPath);
        break;
    }
}

try
{
    var services = new ServiceCollection();

    var workDir = Directory.GetCurrentDirectory();
    var sessionsDir = Path.Combine(workDir, "sessions");

    services.AddSingleton<IChatProvider>(ChatProviderFactory.Create());
    services.AddSingleton<IToolRegistry, ToolRegistry>();
    services.AddSingleton<ISessionCli>(new SessionCli(sessionsDir));
    services.AddSingleton<IConsoleUI, ConsoleUI>();
    services.AddSingleton<ChatOptions>(new ChatOptions
    {
        SystemPrompt = "You are a helpful coding assistant.",
        MaxTokens = 4096,
        EnableThinking = bool.TryParse(Environment.GetEnvironmentVariable("ENABLE_THINKING"), out var et) && et
    });
    services.AddSingleton<IAgentLoop, AgentLoop>();

    var serviceProvider = services.BuildServiceProvider();

    var tools = serviceProvider.GetRequiredService<IToolRegistry>();
    tools.Register(new ReadTool(workDir));
    tools.Register(new WriteTool(workDir));
    tools.Register(new EditTool(workDir));
    tools.Register(new GlobTool(workDir));
    tools.Register(new GrepTool(workDir));
    tools.Register(new BashTool(workDir));
    tools.Register(new WebSearchTool());
    tools.Register(new CodeSearchTool());

    var agent = serviceProvider.GetRequiredService<IAgentLoop>();
    await agent.RunAsync();
}
catch (Exception ex)
{
    var ui = new ConsoleUI();
    ui.PrintError(ex.Message);
    Environment.Exit(1);
}