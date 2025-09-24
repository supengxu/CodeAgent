using System.Text;
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
    public string? ReadLine() => Console.ReadLine();
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
    private readonly SessionStore _session;
    private readonly ToolRegistry _tools;
    private readonly ChatOptions _options;
    private readonly IConsoleIO _console;
    private readonly ConsoleUI _ui;
    private readonly Func<ToolCall, Task<bool>>? _toolConfirmationHandler;
    private int _toolCallCount;

    public IReadOnlyList<ChatMessage> History => _session.Messages;

    public AgentLoop(IChatProvider provider, ToolRegistry tools, ChatOptions options)
        : this(provider, tools, options, new DefaultConsoleIO(), null)
    {
    }

    public AgentLoop(IChatProvider provider, ToolRegistry tools, ChatOptions options, 
        Func<ToolCall, Task<bool>>? toolConfirmationHandler)
        : this(provider, tools, options, new DefaultConsoleIO(), toolConfirmationHandler)
    {
    }

    internal AgentLoop(IChatProvider provider, ToolRegistry tools, ChatOptions options, 
        IConsoleIO console, Func<ToolCall, Task<bool>>? toolConfirmationHandler = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _tools = tools ?? throw new ArgumentNullException(nameof(tools));
        _options = options ?? new ChatOptions();
        _console = console ?? new DefaultConsoleIO();
        _toolConfirmationHandler = toolConfirmationHandler;
        _session = new SessionStore();
        _ui = new ConsoleUI();
        _toolCallCount = 0;
    }

    public void RegisterTool(ITool tool)
    {
        _tools.Register(tool);
    }

    public async Task<AgentResponse> SendMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be empty", nameof(message));

        _session.AddMessage(ChatMessage.CreateText(ChatRole.User, message));

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
                _session.AddMessage(new ChatMessage(ChatRole.Assistant, response.Content));
            }

            if (response.StopReason == "tool_use" && response.ToolCalls.Count > 0)
            {
                foreach (var toolCall in response.ToolCalls)
                {
                    var result = await ExecuteToolAsync(toolCall, cancellationToken);
                    _session.AddMessage(ChatRole.Tool, new[]
                    {
                        new ToolResultBlock(toolCall.Id, result.Output, !result.Success)
                    });
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
                _ui.PrintStats(_session.Messages.Count, _toolCallCount);
                _ui.PrintGoodbye();
                break;
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

        await foreach (var chunk in _provider.CompleteStreamingAsync(_session.Messages, _options, cancellationToken))
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
