namespace CodeAgentDemo.Models;

public class AgentConfig
{
    public ChatConfig Chat { get; set; } = new();
    public LoopControlConfig LoopControl { get; set; } = new();
    public PlanningConfig Planning { get; set; } = new();
    public ProviderConfig Provider { get; set; } = new();
    public SessionConfig Session { get; set; } = new();
    public ContextConfig Context { get; set; } = new();
}

public class ChatConfig
{
    public string SystemPrompt { get; set; } = "You are a helpful coding assistant.";
    public int MaxTokens { get; set; } = 4096;
    public bool EnableThinking { get; set; }
}

public class ProviderConfig
{
    public string Name { get; set; } = "OpenAI";
    public string? ApiKey { get; set; }
    public string? Model { get; set; }
    public string? ApiUrl { get; set; }
}

public class SessionConfig
{
    public string SessionsDirectory { get; set; } = "sessions";
}