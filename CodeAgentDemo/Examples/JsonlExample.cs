using System.Text.Json;
using CodeAgentDemo.Core;
using CodeAgentDemo.Models;

namespace CodeAgentDemo.Examples;

/// <summary>
/// Example demonstrating JSONL session persistence usage.
/// </summary>
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
        
        // Add sample messages to the session store
        sessionStore.AddMessage(ChatRole.User, "Hello world");
        sessionStore.AddMessage(ChatRole.Assistant, "Greetings, user!");
        sessionStore.AddMessage(ChatRole.System, [
            new TextBlock("System initialized"),
            new ThinkingBlock("Processing user request")
        ]);
        
        var sampleFilePath = Path.Join(Path.GetTempPath(), "session.jsonl");
        Console.WriteLine($"Saving session to: {sampleFilePath}");
        
        // Save the session to JSONL file
        await _sessionPersistence.SaveSessionAsync(sessionStore, sampleFilePath);
        Console.WriteLine("Session saved successfully");

        // Load the session back
        var loadedSession = await _sessionPersistence.LoadSessionAsync(sampleFilePath);
        Console.WriteLine($"Loaded session with {loadedSession.Count} messages:");
        
        // Display the loaded messages
        foreach (var message in loadedSession.Messages)
        {
            Console.WriteLine($"  Role: {message.Role}, Content blocks: {message.Content.Count()}");
        }

        // Now use streaming processor for large sets of data
        Console.WriteLine("\nUsing streaming processor to append additional messages...");
        
        // Add a few more messages and append them
        var newMessage = ChatMessage.CreateText(ChatRole.User, "Additional message after persistence");
        await _sessionPersistence.AppendToSessionAsync(newMessage, sampleFilePath);
        
        Console.WriteLine("Additional message appended.");
        
        // Demonstrate streaming processing of the file
        Console.WriteLine("\nStreaming through all messages:");
        await foreach (var message in _streamProcessor.ReadJsonlFileAsync(sampleFilePath))
        {
            Console.WriteLine($"  Streaming message - Role: {message.Role}, Content blocks: {message.Content.Count()}");
        }

        // Verify by loading with the simpler method
        Console.WriteLine("\nReloading file to verify appended message:");
        var finalSession = await _sessionPersistence.LoadSessionAsync(sampleFilePath);
        Console.WriteLine($"Final session has {finalSession.Count} messages");

        // Clean up the temporary file
        if (File.Exists(sampleFilePath))
        {
            File.Delete(sampleFilePath);
            Console.WriteLine("Temporary file cleaned up.");
        }
    }
}