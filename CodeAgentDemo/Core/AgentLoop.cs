using System.Text;
using CodeAgentDemo.Models;
using CodeAgentDemo.Providers;
using CodeAgentDemo.Tools;

namespace CodeAgentDemo.Core;

/// <summary>
/// Main agent loop that handles user input, streaming responses, and tool execution.
/// </summary>
public class AgentLoop
{
    private readonly IChatProvider _provider;
    private readonly SessionStore _session;
    private readonly ToolRegistry _tools;
    private readonly ChatOptions _options;

    public AgentLoop(IChatProvider provider, ToolRegistry tools, ChatOptions options)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _tools = tools ?? throw new ArgumentNullException(nameof(tools));
        _options = options ?? new ChatOptions();
        _session = new SessionStore();
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Console.Write("\n> ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input)) continue;
            if (input.Trim().ToLower() is "quit" or "exit") break;

            _session.AddMessage(ChatMessage.CreateText(ChatRole.User, input));

            // Inner loop for handling tool calls
            while (true)
            {
                try
                {
                    var response = await CollectStreamingResponseAsync(cancellationToken);

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
                        continue; // Re-call LLM with tool results
                    }

                    break; // Exit inner loop
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\n[Error] {ex.Message}");
                    break;
                }
            }
        }
    }

    private async Task<StreamResponse> CollectStreamingResponseAsync(CancellationToken cancellationToken)
    {
        var textBuilder = new StringBuilder();
        var thinkingBuilder = new StringBuilder();
        var toolCallBuilders = new List<ToolCallBuilder>();
        string? stopReason = null;

        await foreach (var chunk in _provider.CompleteStreamingAsync(_session.Messages, _options, cancellationToken))
        {
            if (!string.IsNullOrEmpty(chunk.TextDelta))
            {
                Console.Write(chunk.TextDelta);
                textBuilder.Append(chunk.TextDelta);
            }

            if (!string.IsNullOrEmpty(chunk.ThinkingDelta))
            {
                Console.WriteLine($"[思考] {chunk.ThinkingDelta}");
                thinkingBuilder.Append(chunk.ThinkingDelta);
            }

            if (chunk.ToolCallDelta != null)
            {
                AccumulateToolCall(toolCallBuilders, chunk.ToolCallDelta);
            }
        }

        Console.WriteLine(); // Newline after streaming

        var contentBlocks = BuildContentBlocks(textBuilder, thinkingBuilder);
        var toolCalls = BuildToolCalls(toolCallBuilders);

        return new StreamResponse(contentBlocks, toolCalls, stopReason ?? "end_turn");
    }

    private void AccumulateToolCall(List<ToolCallBuilder> builders, ToolCallDelta delta)
    {
        if (!string.IsNullOrEmpty(delta.Id))
        {
            builders.Add(new ToolCallBuilder { Id = delta.Id, Name = delta.Name });
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
            return new ToolResult(false, $"Tool '{toolCall.Name}' not found.");
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMinutes(5));
            return await tool.ExecuteAsync(toolCall.Arguments);
        }
        catch (OperationCanceledException)
        {
            return new ToolResult(false, "Tool execution timed out.");
        }
        catch (Exception ex)
        {
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
