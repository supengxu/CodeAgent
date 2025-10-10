using System.Text.Json;
using CodeAgentDemo.Models;

namespace CodeAgentDemo.Core;

/// <summary>
/// Manages multiple sessions and provides utilities to list, load, and create sessions.
/// </summary>
public class SessionManager
{
    private readonly string _sessionsDirectory;
    private readonly JsonSerializerOptions _jsonOptions;

    public SessionManager(string sessionsDirectory = "sessions")
    {
        _sessionsDirectory = sessionsDirectory;
        Directory.CreateDirectory(_sessionsDirectory); // Ensure directory exists

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };
        
        _jsonOptions.Converters.Add(new ContentBlockJsonConverterFactory());
    }

    /// <summary>
    /// Gets a list of available session files in the sessions directory
    /// </summary>
    public List<SessionInfo> ListSessions()
    {
        var sessionFiles = Directory.GetFiles(_sessionsDirectory, "*.jsonl")
            .Select(path => new FileInfo(path))
            .OrderByDescending(fi => fi.CreationTime) // Most recent first
            .Select(fi => GetSessionInfo(fi))
            .ToList();

        return sessionFiles;
    }

    /// <summary>
    /// Creates or gets an empty session store with the specified ID
    /// </summary>
    /// <param name="sessionId">Unique identifier for the session (without extension)</param>
    /// <returns>New SessionStore instance</returns>
    public async Task<(SessionStore store, string filePath)> CreateSessionAsync(string sessionId)
    {
        var filePath = Path.Combine(_sessionsDirectory, $"{sessionId}.jsonl");
        
        var sessionStore = new SessionStore();
        
        // Ensure the file exists (even if empty)
        if (!File.Exists(filePath))
        {
            using var _ = File.Create(filePath);
        }
        
        return (sessionStore, filePath);
    }

    /// <summary>
    /// Loads an existing session from the specified ID
    /// </summary>
    /// <param name="sessionId">Session ID to load (without extension)</param>
    public async Task<(SessionStore store, string filePath)?> LoadSessionAsync(string sessionId)
    {
        var filePath = Path.Combine(_sessionsDirectory, $"{sessionId}.jsonl");
        
        if (!File.Exists(filePath))
        {
            return null;
        }

        var persistence = new SessionPersistence();
        var sessionStore = await persistence.LoadSessionAsync(filePath);
        
        return (sessionStore, filePath);
    }

    /// <summary>
    /// Loads the most recent session file if any exist
    /// </summary>
    public async Task<(SessionStore store, string filePath)?> LoadMostRecentSessionAsync()
    {
        var sessions = ListSessions();
        if (sessions.Count > 0)
        {
            var mostRecent = sessions.First();
            return await LoadSessionAsync(mostRecent.SessionId);
        }
        
        return null;
    }

    /// <summary>
    /// Deletes a session file
    /// </summary>
    /// <param name="sessionId">ID of session to delete (without extension)</param>
    public void DeleteSession(string sessionId)
    {
        var filePath = Path.Combine(_sessionsDirectory, $"{sessionId}.jsonl");
        
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    /// <summary>
    /// Gets metadata information for a session file
    /// </summary>
    private SessionInfo GetSessionInfo(FileInfo fileInfo)
    {
        var sessionId = Path.GetFileNameWithoutExtension(fileInfo.Name);
        var messageCount = CountMessagesInFile(fileInfo.FullName);
        
        return new SessionInfo(
            SessionId: sessionId,
            FilePath: fileInfo.FullName,
            CreationTime: fileInfo.CreationTime,
            LastModified: fileInfo.LastWriteTime,
            SizeBytes: fileInfo.Length,
            MessageCount: messageCount
        );
    }

    /// <summary>
    /// Counts the number of messages in a JSONL file by counting lines
    /// </summary>
    private int CountMessagesInFile(string filePath)
    {
        try
        {
            return File.ReadLines(filePath)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Count();
        }
        catch
        {
            // If we can't read the file, return 0
            return 0;
        }
    }
}

/// <summary>
/// Information about a session file
/// </summary>
public record SessionInfo(
    string SessionId,
    string FilePath,
    DateTime CreationTime,
    DateTime LastModified,
    long SizeBytes,
    int MessageCount
);
