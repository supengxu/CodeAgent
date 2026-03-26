using System.Text.Json;
using CodeAgentDemo.Core;
using CodeAgentDemo.Models;

namespace CodeAgentDemo.Examples;

/// <summary>
/// 演示 JSONL 会话持久化的使用示例。</summary>
public class JsonlExample
{
    private readonly SessionPersistence _sessionPersistence;
    private readonly JsonlStreamProcessor _streamProcessor;

    public JsonlExample()
    {
        _sessionPersistence = new SessionPersistence();
        _streamProcessor = new JsonlStreamProcessor();
    }

    public async Task DemonstratePersistenceAsync()
    {
        var sessionStore = new SessionStore();

        // 添加示例消息到会话存储
        sessionStore.AddMessage(ChatRole.User, "Hello world");
        sessionStore.AddMessage(ChatRole.Assistant, "Greetings, user!");
        sessionStore.AddMessage(ChatRole.System, [
            new TextBlock("System initialized"),
            new ThinkingBlock("Processing user request")
        ]);

        var sampleFilePath = Path.Join(Path.GetTempPath(), "session.jsonl");
        Console.WriteLine($"Saving session to: {sampleFilePath}");

        // 将会话保存到 JSONL 文件
        await _sessionPersistence.SaveSessionAsync(sessionStore, sampleFilePath);
        Console.WriteLine("Session saved successfully");

        // 加载会话
        var loadedSession = await _sessionPersistence.LoadSessionAsync(sampleFilePath);
        Console.WriteLine($"Loaded session with {loadedSession.Count} messages:");

        // 显示加载的消息
        foreach (var message in loadedSession.Messages)
        {
            Console.WriteLine($"  Role: {message.Role}, Content blocks: {message.Content.Count()}");
        }

        // 现在使用流式处理器处理大数据集
        Console.WriteLine("\nUsing streaming processor to append additional messages...");

        // 添加更多消息并追加
        var newMessage = ChatMessage.CreateText(ChatRole.User, "Additional message after persistence");
        await _sessionPersistence.AppendToSessionAsync(newMessage, sampleFilePath);

        Console.WriteLine("Additional message appended.");

        // 演示流式处理文件
        Console.WriteLine("\nStreaming through all messages:");
        await foreach (var message in _streamProcessor.ReadJsonlFileAsync(sampleFilePath))
        {
            Console.WriteLine($"  Streaming message - Role: {message.Role}, Content blocks: {message.Content.Count()}");
        }

        // 通过简单方法重新加载验证
        Console.WriteLine("\nReloading file to verify appended message:");
        var finalSession = await _sessionPersistence.LoadSessionAsync(sampleFilePath);
        Console.WriteLine($"Final session has {finalSession.Count} messages");

        // 清理临时文件
        if (File.Exists(sampleFilePath))
        {
            File.Delete(sampleFilePath);
            Console.WriteLine("Temporary file cleaned up.");
        }
    }
}