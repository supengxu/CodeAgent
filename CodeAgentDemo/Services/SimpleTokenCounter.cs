using CodeAgentDemo.Models;

namespace CodeAgentDemo.Services;

public class SimpleTokenCounter : ITokenCounter
{
    private const int CharsPerToken = 4;
    
    public int EstimateTokenCount(string text)
    {
        return string.IsNullOrEmpty(text) ? 0 : (text.Length / CharsPerToken) + 1;
    }
    
    public int EstimateMessageTokens(ChatMessage message)
    {
        return message.Content.Sum(block => block switch
        {
            TextBlock t => EstimateTokenCount(t.Text),
            ThinkingBlock th => EstimateTokenCount(th.Thinking),
            ToolUseBlock tu => EstimateTokenCount(tu.Input.GetRawText()),
            ToolResultBlock tr => EstimateTokenCount(tr.Content),
            _ => 0
        });
    }
}