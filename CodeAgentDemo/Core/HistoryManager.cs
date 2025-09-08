using CodeAgentDemo.Models;
using CodeAgentDemo.Services;

namespace CodeAgentDemo.Core;

public class HistoryManager
{
    private readonly ITokenCounter _tokenCounter;
    private readonly int _maxTokens;
    private readonly int _preserveRecentPairs;

    public HistoryManager(ITokenCounter tokenCounter, int maxTokens = 8000, int preserveRecentPairs = 2)
    {
        _tokenCounter = tokenCounter;
        _maxTokens = maxTokens;
        _preserveRecentPairs = preserveRecentPairs;
    }

    public IEnumerable<ChatMessage> Trim(IEnumerable<ChatMessage> messages)
    {
        var msgList = messages.ToList();
        
        if (msgList.Count == 0) return msgList;
        
        var systemMessages = msgList.Where(m => m.Role == ChatRole.System).ToList();
        var otherMessages = msgList.Where(m => m.Role != ChatRole.System).ToList();
        
        int systemTokens = systemMessages.Sum(m => _tokenCounter.EstimateMessageTokens(m));
        
        var recentMessages = otherMessages.TakeLast(_preserveRecentPairs * 2).ToList();
        int recentTokens = recentMessages.Sum(m => _tokenCounter.EstimateMessageTokens(m));
        
        int availableTokens = _maxTokens - systemTokens - recentTokens;
        
        if (availableTokens <= 0)
        {
            var result = new List<ChatMessage>(systemMessages);
            result.AddRange(recentMessages);
            return result;
        }
        
        var olderMessages = otherMessages.SkipLast(_preserveRecentPairs * 2).ToList();
        var includedOlder = new List<ChatMessage>();
        int olderTokens = 0;
        
        foreach (var msg in olderMessages)
        {
            int msgTokens = _tokenCounter.EstimateMessageTokens(msg);
            if (olderTokens + msgTokens <= availableTokens)
            {
                includedOlder.Add(msg);
                olderTokens += msgTokens;
            }
            else
            {
                break;
            }
        }
        
        var finalResult = new List<ChatMessage>(systemMessages);
        finalResult.AddRange(includedOlder);
        finalResult.AddRange(recentMessages);
        
        return finalResult;
    }
    
    public int EstimateTotalTokens(IEnumerable<ChatMessage> messages)
    {
        return messages.Sum(m => _tokenCounter.EstimateMessageTokens(m));
    }
    
    public bool NeedsTrimming(IEnumerable<ChatMessage> messages)
    {
        return EstimateTotalTokens(messages) > _maxTokens;
    }
}