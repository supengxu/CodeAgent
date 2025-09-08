using System.Runtime.CompilerServices;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using CodeAgentDemo.Models;

namespace CodeAgentDemo.Providers;

[Obsolete("不知道怎么支持先不整这个了")]
public class AnthropicProvider : IChatProvider
{
    private readonly AnthropicClient _client;
    private readonly string _model;
    private readonly bool _enableThinking;
    private readonly int _thinkingBudget;

    public AnthropicProvider(
        string apiKey, 
        string model, 
        bool enableThinking = false, 
        int thinkingBudget = 0,
        string? baseUrl = null)
    {
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", apiKey);
        if (!string.IsNullOrEmpty(baseUrl))
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_BASE_URL", baseUrl);
        }
        
        // Disable response validation for compatibility with third-party endpoints (e.g., DashScope)
        _client = new AnthropicClient(new Anthropic.Core.ClientOptions
        {
            ResponseValidation = false
        });
        _model = model;
        _enableThinking = enableThinking;
        _thinkingBudget = thinkingBudget;
    }

    public string ProviderName => "Anthropic";

    public async IAsyncEnumerable<StreamChunk> CompleteStreamingAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var parameters = BuildParameters(messages, options);
        
        await foreach (var rawEvent in _client.Messages.CreateStreaming(parameters, cancellationToken))
        {
            var chunk = ParseStreamEvent(rawEvent);
            if (chunk != null)
            {
                if (chunk.ToolCallDelta != null 
                    && string.IsNullOrEmpty(chunk.ToolCallDelta.Id) 
                    && string.IsNullOrEmpty(chunk.ToolCallDelta.Name) 
                    && string.IsNullOrEmpty(chunk.ToolCallDelta.ArgumentsDelta))
                {
                    continue;
                }
                yield return chunk;
            }
        }
    }

    private MessageCreateParams BuildParameters(IEnumerable<ChatMessage> messages, ChatOptions? options)
    {
        var msgList = messages.ToList();
        var thinking = _enableThinking 
            ? new ThinkingConfigEnabled { BudgetTokens = _thinkingBudget > 0 ? _thinkingBudget : 1024 }
            : null;
        
        return new MessageCreateParams
        {
            Model = _model,
            MaxTokens = options?.MaxTokens ?? 4096,
            Messages = ConvertMessages(msgList),
            Tools = ConvertTools(options?.Tools),
            System = options?.SystemPrompt,
            Thinking = thinking
        };
    }

    private static List<MessageParam> ConvertMessages(List<ChatMessage> messages)
    {
        return messages.Select(msg => new MessageParam
        {
            Role = msg.Role switch
            {
                ChatRole.User => Role.User,
                ChatRole.Assistant => Role.Assistant,
                _ => Role.User
            },
            Content = ConvertContent(msg.Content)
        }).ToList();
    }

    private static List<ContentBlockParam> ConvertContent(IEnumerable<Models.ContentBlock> content)
    {
        return content.Select(block => block switch
        {
            Models.TextBlock t => (ContentBlockParam)new TextBlockParam { Text = t.Text },
            Models.ThinkingBlock th => new ThinkingBlockParam { Thinking = th.Thinking, Signature = "" },
            Models.ToolUseBlock tu => new ToolUseBlockParam
            {
                ID = tu.Id,
                Name = tu.Name,
                Input = ConvertToDictionary(tu.Input)
            },
            Models.ToolResultBlock tr => new ToolResultBlockParam
            {
                ToolUseID = tr.ToolUseId,
                Content = new ToolResultBlockParamContent(tr.Content),
                IsError = tr.IsError
            },
            _ => new TextBlockParam { Text = "" }
        }).ToList();
    }

    private static Dictionary<string, JsonElement> ConvertToDictionary(JsonElement element)
    {
        var result = new Dictionary<string, JsonElement>();
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                result[prop.Name] = prop.Value.Clone();
            }
        }
        return result;
    }

    private static List<ToolUnion>? ConvertTools(IEnumerable<ToolDefinition>? tools)
    {
        if (tools == null) return null;
        
        return tools.Select(t => 
        {
            var props = new Dictionary<string, JsonElement>();
            var required = new List<string>();
            
            if (t.InputSchema.TryGetProperty("properties", out var propsElement))
            {
                foreach (var prop in propsElement.EnumerateObject())
                {
                    props[prop.Name] = JsonSerializer.SerializeToElement(prop.Value);
                }
            }
            
            if (t.InputSchema.TryGetProperty("required", out var requiredElement))
            {
                required = JsonSerializer.Deserialize<List<string>>(requiredElement.GetRawText()) ?? new List<string>();
            }
            
            var schema = new InputSchema
            {
                Properties = props,
                Required = required
            };
            
            return new ToolUnion(new Tool
            {
                Name = t.Name,
                Description = t.Description,
                InputSchema = schema
            });
        }).ToList();
    }

    private static StreamChunk? ParseStreamEvent(RawMessageStreamEvent rawEvent)
    {
        if (rawEvent.TryPickContentBlockStart(out var start))
        {
            return ParseContentBlockStart(start);
        }
        
        if (rawEvent.TryPickContentBlockDelta(out var delta))
        {
            return ParseContentBlockDelta(delta);
        }
        
        if (rawEvent.TryPickDelta(out var msgDelta))
        {
            return new StreamChunk(null, null, null, msgDelta.Delta.StopReason);
        }

        return null;
    }

    private static StreamChunk? ParseContentBlockStart(RawContentBlockStartEvent start)
    {
        if (start.ContentBlock.TryPickToolUse(out var toolUse) 
            && !string.IsNullOrEmpty(toolUse.ID) 
            && !string.IsNullOrEmpty(toolUse.Name))
        {
            return new StreamChunk(
                null, 
                null, 
                new ToolCallDelta(toolUse.ID, toolUse.Name, null),
                null
            );
        }
        
        if (start.ContentBlock.TryPickThinking(out var thinking) && !string.IsNullOrEmpty(thinking.Thinking))
        {
            return new StreamChunk(null, thinking.Thinking, null, null);
        }

        return null;
    }

    private static StreamChunk? ParseContentBlockDelta(RawContentBlockDeltaEvent delta)
    {
        if (delta.Delta.TryPickText(out var text) && !string.IsNullOrEmpty(text.Text))
        {
            return new StreamChunk(text.Text, null, null, null);
        }
        
        if (delta.Delta.TryPickThinking(out var thinking) && !string.IsNullOrEmpty(thinking.Thinking))
        {
            return new StreamChunk(null, thinking.Thinking, null, null);
        }
        
        if (delta.Delta.Value is InputJsonDelta inputJson && !string.IsNullOrEmpty(inputJson.PartialJson))
        {
            return new StreamChunk(
                null, 
                null, 
                new ToolCallDelta(null, null, inputJson.PartialJson),
                null
            );
        }

        return null;
    }
}