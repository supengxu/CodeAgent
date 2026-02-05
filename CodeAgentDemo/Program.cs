using CodeAgentDemo.Cli;
using CodeAgentDemo.Core;
using CodeAgentDemo.Models;
using CodeAgentDemo.Providers;
using CodeAgentDemo.Services;
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

    var enableThinking = bool.TryParse(Environment.GetEnvironmentVariable("ENABLE_THINKING"), out var et) && et;

    services.AddSingleton(new ChatOptions
    {
        SystemPrompt = "You are a helpful coding assistant.",
        MaxTokens = 4096,
        EnableThinking = enableThinking
    });

    services.AddSingleton<IOpenAIConverter, OpenAIConverter>();
    services.AddSingleton<IChatProvider, OpenAIProvider>();
    services.AddSingleton<IToolRegistry, ToolRegistry>();
    services.AddSingleton<ISessionCli>(sp => new SessionCli(sessionsDir));
    services.AddSingleton<IConsoleUI, SpectreConsoleUI>();
    services.AddSingleton<ILayoutRenderer, SplitLayoutRenderer>();
    services.AddSingleton<IConsoleIO, SpectreConsoleIO>();
    services.AddSingleton<IAgentLoop, AgentLoop>();

    services.AddHttpClient("WebSearch");
    services.AddHttpClient("CodeSearch");

    services.AddSingleton<IToolFactory>(sp =>
    {
        var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
        return new ToolFactory(
            workDir,
            httpClientFactory.CreateClient("WebSearch"),
            httpClientFactory.CreateClient("CodeSearch")
        );
    });

    var serviceProvider = services.BuildServiceProvider();

    var tools = serviceProvider.GetRequiredService<IToolRegistry>();
    var toolFactory = serviceProvider.GetRequiredService<IToolFactory>();
    foreach (var tool in toolFactory.CreateTools())
    {
        tools.Register(tool);
    }

    var agent = serviceProvider.GetRequiredService<IAgentLoop>();
    await agent.RunAsync();
}
catch (Exception ex)
{
    var ui = new ConsoleUI();
    ui.PrintError(ex.Message);
    Environment.Exit(1);
}