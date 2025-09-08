using CodeAgentDemo.Models;

namespace CodeAgentDemo.Services;

public interface IOpenAIConverter
{
    List<OpenAI.Chat.ChatMessage> ToOpenAIMessages(IEnumerable<ChatMessage> messages, string? systemPrompt);
    
    OpenAI.Chat.ChatTool ToOpenAITool(ToolDefinition tool);
    
    StreamChunk FromOpenAIUpdate(OpenAI.Chat.StreamingChatCompletionUpdate update);
    
    string? GetFinishReason(OpenAI.Chat.StreamingChatCompletionUpdate update);
}