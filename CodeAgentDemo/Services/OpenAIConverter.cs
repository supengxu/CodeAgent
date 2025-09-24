using System.Reflection;
using System.Text.Json;
using CodeAgentDemo.Models;

namespace CodeAgentDemo.Services;

public class OpenAIConverter : IOpenAIConverter
{
    private readonly bool _enableThinking;

    public OpenAIConverter(bool enableThinking = false)
    {
        _enableThinking = enableThinking;
    }
    public List<OpenAI.Chat.ChatMessage> ToOpenAIMessages(IEnumerable<ChatMessage> messages, string? systemPrompt)
    {
        var result = new List<OpenAI.Chat.ChatMessage>();
        
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            result.Add(new OpenAI.Chat.SystemChatMessage(systemPrompt));
        }
        
        foreach (var msg in messages)
        {
            if (msg.Role == ChatRole.System) continue;
            
            var chatMessage = msg.Role switch
            {
                ChatRole.User => (OpenAI.Chat.ChatMessage)new OpenAI.Chat.UserChatMessage(ConvertContent(msg.Content)),
                ChatRole.Assistant => new OpenAI.Chat.AssistantChatMessage(ConvertContent(msg.Content)),
                ChatRole.Tool => CreateToolResultMessage(msg),
                _ => new OpenAI.Chat.UserChatMessage(ConvertContent(msg.Content))
            };
            
            result.Add(chatMessage);
        }
        
        return result;
    }
    
    private List<OpenAI.Chat.ChatMessageContentPart> ConvertContent(IEnumerable<ContentBlock> content)
    {
        return content.Select(block => block switch
        {
            TextBlock t => OpenAI.Chat.ChatMessageContentPart.CreateTextPart(t.Text),
            ThinkingBlock th => OpenAI.Chat.ChatMessageContentPart.CreateTextPart(th.Thinking),
            ToolUseBlock tu => OpenAI.Chat.ChatMessageContentPart.CreateTextPart($"[ToolUse: {tu.Name}]"),
            ToolResultBlock tr => OpenAI.Chat.ChatMessageContentPart.CreateTextPart(tr.Content),
            _ => OpenAI.Chat.ChatMessageContentPart.CreateTextPart("")
        }).ToList();
    }
    
    private OpenAI.Chat.ChatMessage CreateToolResultMessage(ChatMessage msg)
    {
        var toolResults = msg.Content.OfType<ToolResultBlock>().ToList();
        if (toolResults.Count == 0)
        {
            return new OpenAI.Chat.ToolChatMessage("unknown", "");
        }
        
        var firstResult = toolResults[0];
        return new OpenAI.Chat.ToolChatMessage(firstResult.ToolUseId, firstResult.Content);
    }
    
    public OpenAI.Chat.ChatTool ToOpenAITool(ToolDefinition tool)
    {
        var jsonBytes = System.Text.Encoding.UTF8.GetBytes(tool.InputSchema.GetRawText());
        return OpenAI.Chat.ChatTool.CreateFunctionTool(
            functionName: tool.Name,
            functionDescription: tool.Description,
            functionParameters: new System.BinaryData(jsonBytes)
        );
    }
    
    public StreamChunk FromOpenAIUpdate(OpenAI.Chat.StreamingChatCompletionUpdate update)
    {
        string? textDelta = null;
        string? thinkingDelta = null;
        ToolCallDelta? toolCallDelta = null;
        UsageInfo? usage = null;
        
        // 提取 reasoning_content (仅在启用 thinking 时)
        if (_enableThinking)
        {
            thinkingDelta = ExtractReasoningContent(update);
        }
        
        if (update.ContentUpdate.Count > 0)
        {
            textDelta = update.ContentUpdate[0].Text;
        }
        
        if (update.ToolCallUpdates.Count > 0)
        {
            var tc = update.ToolCallUpdates[0];
            toolCallDelta = new ToolCallDelta(
                tc.ToolCallId,
                tc.FunctionName,
                tc.FunctionArgumentsUpdate?.ToString()
            );
        }
        
        usage = ExtractUsage(update);
        
        return new StreamChunk(textDelta, thinkingDelta, toolCallDelta, GetFinishReason(update), usage);
    }
    
    private UsageInfo? ExtractUsage(OpenAI.Chat.StreamingChatCompletionUpdate update)
    {
        try
        {
            var usage = update.Usage;
            if (usage != null)
            {
                return new UsageInfo(
                    usage.InputTokenCount,
                    usage.OutputTokenCount
                );
            }
        }
        catch
        {
            // 部分 API 端点不支持 usage
        }
        
        return null;
    }
    
    /// <summary>
    /// 使用反射从 StreamingChatCompletionUpdate 提取 reasoning_content 字段
    /// OpenAI reasoning models (o1, o3-mini) 在 delta.reasoning_content 中返回思考过程
    /// </summary>
    private string? ExtractReasoningContent(OpenAI.Chat.StreamingChatCompletionUpdate update)
    {
        try
        {
            // 尝试通过反射获取 ReasoningContent 属性
            var updateType = update.GetType();
            
            // 首先尝试公共属性 ReasoningContent
            var reasoningProperty = updateType.GetProperty("ReasoningContent", 
                BindingFlags.Public | BindingFlags.Instance);
            
            if (reasoningProperty == null)
            {
                // 尝试非公共属性
                reasoningProperty = updateType.GetProperty("ReasoningContent", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
            }
            
            if (reasoningProperty != null)
            {
                var value = reasoningProperty.GetValue(update) as string;
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }
            
            // 如果属性不存在，尝试通过 ContentUpdate 的内部字段获取
            // OpenAI SDK 可能将 reasoning_content 放在内部
            if (update.ContentUpdate.Count > 0)
            {
                var contentPart = update.ContentUpdate[0];
                var contentType = contentPart.GetType();
                
                // 检查是否有 ReasoningText 或类似属性
                var reasonTextProp = contentType.GetProperty("ReasoningText",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (reasonTextProp != null)
                {
                    return reasonTextProp.GetValue(contentPart) as string;
                }
            }
        }
        catch
        {
            // 反射失败时静默返回 null，不影响正常功能
        }
        
        return null;
    }
    
    public string? GetFinishReason(OpenAI.Chat.StreamingChatCompletionUpdate update)
    {
        return update.FinishReason switch
        {
            OpenAI.Chat.ChatFinishReason.Stop => "end_turn",
            OpenAI.Chat.ChatFinishReason.ToolCalls => "tool_use",
            OpenAI.Chat.ChatFinishReason.Length => "max_tokens",
            _ => null
        };
    }
}