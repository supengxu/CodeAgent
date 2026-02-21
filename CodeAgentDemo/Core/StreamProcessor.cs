using System.Text;
using System.Text.Json;
using CodeAgentDemo.Models;

namespace CodeAgentDemo.Core;

/// <summary>
/// Implementation of stream processor that collects and builds response objects from streaming chunks.
/// </summary>
public class StreamProcessor : IStreamProcessor
{
    private readonly IConsoleUI _ui;

    /// <summary>
    /// Initializes a new instance of the StreamProcessor class.
    /// </summary>
    /// <param name="ui">The console UI for streaming output.</param>
    public StreamProcessor(IConsoleUI ui)
    {
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
    }

    /// <inheritdoc />
    public async Task<StreamResponse> ProcessAsync(IAsyncEnumerable<StreamChunk> stream, CancellationToken cancellationToken = default)
    {
        var textBuilder = new StringBuilder();
        var thinkingBuilder = new StringBuilder();
        var toolCallBuilders = new List<ToolCallBuilder>();
        string? stopReason = null;
        bool toolCallDetected = false;
        UsageInfo? usage = null;

        _ui.BeginStream();

        await foreach (var chunk in stream.WithCancellation(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

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

    private static void AccumulateToolCall(List<ToolCallBuilder> builders, ToolCallDelta delta)
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

    private static IEnumerable<ContentBlock> BuildContentBlocks(StringBuilder text, StringBuilder thinking)
    {
        var blocks = new List<ContentBlock>();
        if (thinking.Length > 0) blocks.Add(new ThinkingBlock(thinking.ToString()));
        if (text.Length > 0) blocks.Add(new TextBlock(text.ToString()));
        return blocks;
    }

    private static IReadOnlyList<ToolCall> BuildToolCalls(List<ToolCallBuilder> builders)
    {
        return builders.Select(b => new ToolCall(
            b.Id ?? throw new InvalidOperationException("Tool call ID is null"),
            b.Name ?? throw new InvalidOperationException("Tool call name is null"),
            JsonDocument.Parse(b.ArgumentsBuilder.ToString()).RootElement
        )).ToList();
    }

    private class ToolCallBuilder
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public StringBuilder ArgumentsBuilder { get; } = new();
    }
}