using CodeAgentDemo.Cli;
using CodeAgentDemo.Core;
using CodeAgentDemo.Models;
using CodeAgentDemo.Providers;
using CodeAgentDemo.Services;
using CodeAgentDemo.Tools;
using DotNetEnv;
using Microsoft.Extensions.Configuration;
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
    var configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: true)
        .AddJsonFile("appsettings.Development.json", optional: true)
        .Build();

    var services = new ServiceCollection();

    services.Configure<AgentConfig>(configuration.GetSection("Agent"));

    var agentConfig = new AgentConfig();
    configuration.GetSection("Agent").Bind(agentConfig);

    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ENABLE_THINKING")))
    {
        bool.TryParse(Environment.GetEnvironmentVariable("ENABLE_THINKING"), out var enableThinking);
        agentConfig.Chat.EnableThinking = enableThinking;
    }

    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AI_PROVIDER")))
    {
        agentConfig.Provider.Name = Environment.GetEnvironmentVariable("AI_PROVIDER")!;
    }

    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_KEY")))
    {
        agentConfig.Provider.ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    }

    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_MODEL")))
    {
        agentConfig.Provider.Model = Environment.GetEnvironmentVariable("OPENAI_MODEL");
    }

    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_URL")))
    {
        agentConfig.Provider.ApiUrl = Environment.GetEnvironmentVariable("OPENAI_API_URL");
    }

    var workDir = Directory.GetCurrentDirectory();
    var sessionsDir = Path.Combine(workDir, agentConfig.Session.SessionsDirectory);

    services.AddSingleton(agentConfig);
    services.AddSingleton(new ChatOptions
    {
        SystemPrompt = agentConfig.Chat.SystemPrompt,
        MaxTokens = agentConfig.Chat.MaxTokens,
        EnableThinking = agentConfig.Chat.EnableThinking
    });
    services.AddSingleton(agentConfig.LoopControl);
    services.AddSingleton(agentConfig.Planning);

    services.AddSingleton<IOpenAIConverter, OpenAIConverter>();
    services.AddSingleton<IChatProvider, OpenAIProvider>();
    services.AddSingleton<IToolRegistry, ToolRegistry>();
    services.AddSingleton<ISessionCli>(sp => new SessionCli(sessionsDir));
    services.AddSingleton<IConsoleUI, SpectreConsoleUI>();
    services.AddSingleton<ILayoutRenderer, SplitLayoutRenderer>();
    services.AddSingleton<IConsoleIO, SpectreConsoleIO>();

    services.AddSingleton<IMessageHandler, MessageHandler>();
    services.AddSingleton<IToolExecutor, ToolExecutor>();
    services.AddSingleton<IStreamProcessor, StreamProcessor>();
    services.AddSingleton<ILoopController, LoopController>();
    services.AddSingleton<ILoopManager, LoopManager>();
    services.AddSingleton<TodoManager>();
    services.AddSingleton<TodoTool>();

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

    services.AddSingleton<IAgentLoop, AgentLoop>();

    var serviceProvider = services.BuildServiceProvider();

    var tools = serviceProvider.GetRequiredService<IToolRegistry>();
    var toolFactory = serviceProvider.GetRequiredService<IToolFactory>();
    var todoTool = serviceProvider.GetRequiredService<TodoTool>();

    foreach (var tool in toolFactory.CreateTools())
    {
        tools.Register(tool);
    }
    tools.Register(todoTool);

    var agent = serviceProvider.GetRequiredService<IAgentLoop>();
    await agent.RunAsync();
}
catch (Exception ex)
{
    var ui = new ConsoleUI();
    ui.PrintError(ex.Message);
    Environment.Exit(1);
}