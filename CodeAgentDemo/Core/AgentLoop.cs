using System.Text;
using CodeAgentDemo.Cli;
using CodeAgentDemo.Models;
using CodeAgentDemo.Providers;
using CodeAgentDemo.Tools;


namespace CodeAgentDemo.Core;

public interface IConsoleIO
{
    void Write(string value);
    void WriteLine(string? value = null);
    string? ReadLine();
}

public class DefaultConsoleIO : IConsoleIO
{
    public void Write(string value) => Console.Write(value);
    public void WriteLine(string? value = null) => Console.WriteLine(value ?? string.Empty);

    public string? ReadLine()
    {
        var input = new StringBuilder();
        var history = new List<string>();
        var historyIndex = -1;

        while (true)
        {
            var key = Console.ReadKey(true);

            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    Console.WriteLine();
                    if (input.Length > 0)
                    {
                        history.Add(input.ToString());
                        historyIndex = history.Count;
                    }
                    return input.ToString();

                case ConsoleKey.Backspace:
                    if (input.Length > 0)
                    {
                        var lastChar = input[^1];
                        input.Remove(input.Length - 1, 1);
                        var displayWidth = GetDisplayWidth(lastChar);
                        Console.Write(new string('\b', displayWidth) + new string(' ', displayWidth) + new string('\b', displayWidth));
                    }
                    break;

                case ConsoleKey.UpArrow:
                    if (history.Count > 0 && historyIndex > 0)
                    {
                        historyIndex--;
                        ClearLine(input);
                        input.Clear();
                        input.Append(history[historyIndex]);
                        Console.Write(input.ToString());
                    }
                    break;

                case ConsoleKey.DownArrow:
                    if (historyIndex < history.Count - 1)
                    {
                        historyIndex++;
                        ClearLine(input);
                        input.Clear();
                        input.Append(history[historyIndex]);
                        Console.Write(input.ToString());
                    }
                    break;

                case ConsoleKey.Escape:
                    ClearLine(input);
                    input.Clear();
                    break;

                default:
                    if (!char.IsControl(key.KeyChar))
                    {
                        input.Append(key.KeyChar);
                        Console.Write(key.KeyChar);
                    }
                    break;
            }
        }
    }

    private void ClearLine(StringBuilder input)
    {
        var totalWidth = 0;
        foreach (var c in input.ToString())
            totalWidth += GetDisplayWidth(c);

        if (totalWidth > 0)
            Console.Write(new string('\b', totalWidth) + new string(' ', totalWidth) + new string('\b', totalWidth));
    }

    private static int GetDisplayWidth(char c)
    {
        if (c >= 0x4E00 && c <= 0x9FFF ||
            c >= 0x3400 && c <= 0x4DBF ||
            c >= 0x3000 && c <= 0x303F ||
            c >= 0xFF00 && c <= 0xFFEF)
            return 2;
        return 1;
    }
}

public record AgentResponse(IEnumerable<ContentBlock> Content, string StopReason);

public interface IAgentLoop
{
    IReadOnlyList<ChatMessage> History { get; }
    Task<AgentResponse> SendMessageAsync(string message, CancellationToken cancellationToken = default);
    void RegisterTool(ITool tool);
}

public class AgentLoop : IAgentLoop
{
    private readonly IChatProvider _provider;
    private readonly SessionStore? _session;
    private readonly ToolRegistry _tools;
    private readonly ChatOptions _options;
    private readonly IConsoleIO _console;
    private readonly ConsoleUI _ui;
    private readonly Func<ToolCall, Task<bool>>? _toolConfirmationHandler;
    private readonly SessionPersistence? _sessionPersistence;
    private readonly string? _sessionFilePath;
    private readonly SessionCli? _sessionCli;
    private int _toolCallCount;

    public IReadOnlyList<ChatMessage> History => _sessionCli?.CurrentSession.Messages ?? _session!.Messages;

    public AgentLoop(IChatProvider provider, ToolRegistry tools, ChatOptions options)
        : this(provider, tools, options, new DefaultConsoleIO(), null, null, null)
    {
    }

    public AgentLoop(IChatProvider provider, ToolRegistry tools, ChatOptions options,
        Func<ToolCall, Task<bool>>? toolConfirmationHandler)
        : this(provider, tools, options, new DefaultConsoleIO(), toolConfirmationHandler, null, null)
    {
    }

    public AgentLoop(IChatProvider provider, ToolRegistry tools, ChatOptions options, string? sessionFilePath = null)
        : this(provider, tools, options, new DefaultConsoleIO(), null, sessionFilePath, null)
    {
    }

    public AgentLoop(IChatProvider provider, ToolRegistry tools, ChatOptions options, SessionCli sessionCli)
        : this(provider, tools, options, new DefaultConsoleIO(), null, null, sessionCli)
    {
    }

    internal AgentLoop(IChatProvider provider, ToolRegistry tools, ChatOptions options,
        IConsoleIO console, Func<ToolCall, Task<bool>>? toolConfirmationHandler = null,
        string? sessionFilePath = null, SessionCli? sessionCli = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _tools = tools ?? throw new ArgumentNullException(nameof(tools));
        _options = options ?? new ChatOptions();
        _console = console ?? new DefaultConsoleIO();
        _toolConfirmationHandler = toolConfirmationHandler;
        _sessionFilePath = sessionFilePath;
        _sessionCli = sessionCli;
        _ui = new ConsoleUI();
        _toolCallCount = 0;

        if (sessionCli != null)
        {
            sessionCli.InitializeAsync().GetAwaiter().GetResult();
            _session = sessionCli.CurrentSession;
        }
        else
        {
            _session = new SessionStore();
            if (_sessionFilePath != null)
            {
                _sessionPersistence = new SessionPersistence();
                var loadedSession = _sessionPersistence.LoadSessionAsync(_sessionFilePath).GetAwaiter().GetResult();
                foreach (var msg in loadedSession.Messages)
                {
                    _session.AddMessage(msg);
                }
            }
        }
    }

    public void RegisterTool(ITool tool)
    {
        _tools.Register(tool);
    }

    public async Task<AgentResponse> SendMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be empty", nameof(message));

        var userMessage = ChatMessage.CreateText(ChatRole.User, message);

        if (_sessionCli != null)
        {
            await _sessionCli.AppendMessageAsync(userMessage);
        }
        else if (_session != null)
        {
            _session.AddMessage(userMessage);
            if (_sessionPersistence != null && _sessionFilePath != null)
            {
                await _sessionPersistence.AppendToSessionAsync(userMessage, _sessionFilePath, cancellationToken);
            }
        }

        var startTime = DateTime.Now;
        var totalInputTokens = 0;
        var totalOutputTokens = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var response = await CollectStreamingResponseAsync(cancellationToken);

            if (response.Usage != null)
            {
                totalInputTokens += response.Usage.InputTokens;
                totalOutputTokens += response.Usage.OutputTokens;
            }

            if (response.Content.Any())
            {
                var assistantMessage = new ChatMessage(ChatRole.Assistant, response.Content);

                if (_sessionCli != null)
                {
                    await _sessionCli.AppendMessageAsync(assistantMessage);
                }
                else if (_session != null)
                {
                    _session.AddMessage(assistantMessage);
                    if (_sessionPersistence != null && _sessionFilePath != null)
                    {
                        await _sessionPersistence.AppendToSessionAsync(assistantMessage, _sessionFilePath, cancellationToken);
                    }
                }
            }

            if (response.StopReason == "tool_use" && response.ToolCalls.Count > 0)
            {
                foreach (var toolCall in response.ToolCalls)
                {
                    var result = await ExecuteToolAsync(toolCall, cancellationToken);

                    var toolResultMessage = new ChatMessage(ChatRole.Tool, new[]
                    {
                        new ToolResultBlock(toolCall.Id, result.Output, !result.Success)
                    });

                    if (_sessionCli != null)
                    {
                        await _sessionCli.AppendMessageAsync(toolResultMessage);
                    }
                    else if (_session != null)
                    {
                        _session.AddMessage(toolResultMessage);
                        if (_sessionPersistence != null && _sessionFilePath != null)
                        {
                            await _sessionPersistence.AppendToSessionAsync(toolResultMessage, _sessionFilePath, cancellationToken);
                        }
                    }
                }
                continue;
            }

            var endTime = DateTime.Now;
            _ui.PrintResponseUsage(startTime, endTime,
                totalInputTokens > 0 ? totalInputTokens : null,
                totalOutputTokens > 0 ? totalOutputTokens : null);

            return new AgentResponse(response.Content, response.StopReason);
        }
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _ui.PrintBanner();

        while (!cancellationToken.IsCancellationRequested)
        {
            _ui.PrintPrompt();
            var input = _console.ReadLine();

            if (input == null) break;
            if (string.IsNullOrWhiteSpace(input)) continue;

            if (input.Trim().ToLower() is "quit" or "exit")
            {
                await SaveAndExitAsync(cancellationToken);
                break;
            }

            if (_sessionCli != null && input.StartsWith("/"))
            {
                var result = await _sessionCli.TryExecuteCommandAsync(input);
                if (result.IsCommand)
                {
                    if (result.Success)
                    {
                        _console.WriteLine(result.Message);
                    }
                    else
                    {
                        _ui.PrintError(result.Message);
                    }
                    continue;
                }
            }

            try
            {
                await SendMessageAsync(input, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _ui.PrintError(ex.Message);
            }
        }
    }

    private async Task SaveAndExitAsync(CancellationToken cancellationToken)
    {
        if (_sessionCli != null)
        {
            await _sessionCli.SaveCurrentSessionAsync();
            _console.WriteLine($"\nSession saved.");
            _ui.PrintStats(_sessionCli.CurrentSession.Count, _toolCallCount);
        }
        else if (_sessionPersistence != null && _sessionFilePath != null && _session != null)
        {
            try
            {
                await _sessionPersistence.SaveSessionAsync(_session, _sessionFilePath, cancellationToken);
                _console.WriteLine($"\nSession saved to {_sessionFilePath}");
                _ui.PrintStats(_session.Messages.Count, _toolCallCount);
            }
            catch (Exception ex)
            {
                _ui.PrintError($"Failed to save session: {ex.Message}");
            }
        }
        else if (_session != null)
        {
            _ui.PrintStats(_session.Messages.Count, _toolCallCount);
        }

        _ui.PrintGoodbye();
    }

    private async Task<StreamResponse> CollectStreamingResponseAsync(CancellationToken cancellationToken)
    {
        _options.Tools = _tools.GetAllTools()
            .Select(t => new ToolDefinition(t.Name, t.Description, t.InputSchema))
            .ToList();

        var textBuilder = new StringBuilder();
        var thinkingBuilder = new StringBuilder();
        var toolCallBuilders = new List<ToolCallBuilder>();
        string? stopReason = null;
        bool toolCallDetected = false;
        UsageInfo? usage = null;

        _ui.BeginStream();

        await foreach (var chunk in _provider.CompleteStreamingAsync(History, _options, cancellationToken))
        {
            if (!string.IsNullOrEmpty(chunk.TextDelta))
            {
                _ui.StreamText(chunk.TextDelta);
                textBuilder.Append(chunk.TextDelta);
            }

            if (!string.IsNullOrEmpty(chunk.ThinkingDelta))
            {
                _ui.StreamThinking(chunk.ThinkingDelta);
                thinkingBuilder.Append(chunk.ThinkingDelta);
            }

            if (chunk.ToolCallDelta != null)
            {
                if (!toolCallDetected)
                {
                    _ui.PrintToolCallDetected();
                    toolCallDetected = true;
                }
                AccumulateToolCall(toolCallBuilders, chunk.ToolCallDelta);
            }

            if (!string.IsNullOrEmpty(chunk.StopReason))
            {
                stopReason = chunk.StopReason;
            }

            if (chunk.Usage != null)
            {
                usage = chunk.Usage;
            }
        }

        _ui.EndStream();

        var contentBlocks = BuildContentBlocks(textBuilder, thinkingBuilder);
        var toolCalls = BuildToolCalls(toolCallBuilders);

        if (stopReason == null && toolCalls.Count > 0)
        {
            stopReason = "tool_use";
        }

        return new StreamResponse(contentBlocks, toolCalls, stopReason ?? "end_turn", usage);
    }

    private void AccumulateToolCall(List<ToolCallBuilder> builders, ToolCallDelta delta)
    {
        if (!string.IsNullOrEmpty(delta.Id))
        {
            builders.Add(new ToolCallBuilder { Id = delta.Id, Name = delta.Name });
        }
        else if (builders.Count > 0 && !string.IsNullOrEmpty(delta.Name))
        {
            builders.Last().Name = delta.Name;
        }

        if (builders.Count > 0 && !string.IsNullOrEmpty(delta.ArgumentsDelta))
        {
            builders.Last().ArgumentsBuilder.Append(delta.ArgumentsDelta);
        }
    }

    private IEnumerable<ContentBlock> BuildContentBlocks(StringBuilder text, StringBuilder thinking)
    {
        var blocks = new List<ContentBlock>();
        if (thinking.Length > 0) blocks.Add(new ThinkingBlock(thinking.ToString()));
        if (text.Length > 0) blocks.Add(new TextBlock(text.ToString()));
        return blocks;
    }

    private IReadOnlyList<ToolCall> BuildToolCalls(List<ToolCallBuilder> builders)
    {
        return builders.Select(b => new ToolCall(
            b.Id ?? throw new InvalidOperationException("Tool call ID is null"),
            b.Name ?? throw new InvalidOperationException("Tool call name is null"),
            System.Text.Json.JsonDocument.Parse(b.ArgumentsBuilder.ToString()).RootElement
        )).ToList();
    }

    private async Task<ToolResult> ExecuteToolAsync(ToolCall toolCall, CancellationToken cancellationToken)
    {
        var tool = _tools.GetTool(toolCall.Name);
        if (tool == null)
        {
            _ui.PrintError($"工具 '{toolCall.Name}' 未找到");
            return new ToolResult(false, $"Tool '{toolCall.Name}' not found.");
        }

        _ui.PrintToolCallStart(toolCall.Name, toolCall.Arguments.ToString());

        bool confirmed;
        if (_toolConfirmationHandler != null)
        {
            confirmed = await _toolConfirmationHandler(toolCall);
        }
        else
        {
            if (!tool.RequiresConfirmation(toolCall.Arguments))
            {
                confirmed = true;
            }
            else
            {
                _ui.PrintToolConfirmation(toolCall.Name);
                _console.Write("    执行? [y/N]: ");

                var confirmation = _console.ReadLine();
                confirmed = confirmation?.ToLower() == "y";
            }
        }

        if (!confirmed)
        {
            _ui.PrintWarning("工具执行已取消");
            return new ToolResult(false, "Tool execution cancelled by user");
        }

        try
        {
            _ui.PrintToolExecuting(toolCall.Name);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMinutes(5));
            var result = await tool.ExecuteAsync(toolCall.Arguments, cts.Token);
            _toolCallCount++;
            _ui.PrintToolResult(toolCall.Name, result.Output, result.Success);
            return result;
        }
        catch (OperationCanceledException)
        {
            _ui.PrintToolResult(toolCall.Name, "执行超时", false);
            return new ToolResult(false, "Tool execution timed out.");
        }
        catch (Exception ex)
        {
            _ui.PrintToolResult(toolCall.Name, ex.Message, false);
            return new ToolResult(false, $"Error: {ex.Message}");
        }
    }

    private class ToolCallBuilder
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public StringBuilder ArgumentsBuilder { get; } = new();
    }
}
