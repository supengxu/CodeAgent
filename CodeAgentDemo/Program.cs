using CodeAgentDemo.Core;
using CodeAgentDemo.Models;
using CodeAgentDemo.Providers;
using CodeAgentDemo.Tools;
using DotNetEnv;

Env.Load();

try
{
    var provider = ChatProviderFactory.Create();
    var tools = new ToolRegistry();
    tools.Register(new BashTool(Directory.GetCurrentDirectory()));

    var options = new ChatOptions
    {
        SystemPrompt = "You are a helpful coding assistant.",
        MaxTokens = 4096,
        EnableThinking = bool.TryParse(Environment.GetEnvironmentVariable("ENABLE_THINKING"), out var et) && et
    };

    var agent = new AgentLoop(provider, tools, options);
    await agent.RunAsync();
}
catch (Exception ex)
{
    var ui = new ConsoleUI();
    ui.PrintError(ex.Message);
    Environment.Exit(1);
}
