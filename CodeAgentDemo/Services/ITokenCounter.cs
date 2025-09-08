using CodeAgentDemo.Models;

namespace CodeAgentDemo.Services;

public interface ITokenCounter
{
    int EstimateTokenCount(string text);
    int EstimateMessageTokens(ChatMessage message);
}