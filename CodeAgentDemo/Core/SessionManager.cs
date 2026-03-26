using System.Text.Json;
using CodeAgentDemo.Models;

namespace CodeAgentDemo.Core;

/// <summary>
/// 管理多个会话并提供列出、加载和创建会话的工具方法。
/// </summary>
public class SessionManager
{
    private readonly string _sessionsDirectory;
    private readonly JsonSerializerOptions _jsonOptions;

    public SessionManager(string sessionsDirectory = "sessions")
    {
        _sessionsDirectory = sessionsDirectory;
        Directory.CreateDirectory(_sessionsDirectory); // 确保目录存在

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
    /// 获取会话目录中可用的会话文件列表
    /// </summary>
    public List<SessionInfo> ListSessions()
    {
        var sessionFiles = Directory.GetFiles(_sessionsDirectory, "*.jsonl")
            .Select(path => new FileInfo(path))
            .OrderByDescending(fi => fi.CreationTime) // 最新的优先
            .Select(fi => GetSessionInfo(fi))
            .ToList();

        return sessionFiles;
    }

    /// <summary>
    /// 创建或获取具有指定 ID 的空会话存储
    /// </summary>
    /// <param name="sessionId">会话的唯一标识符（不带扩展名）</param>
    /// <returns>新的 SessionStore 实例</returns>
    public Task<(SessionStore store, string filePath)> CreateSessionAsync(string sessionId)
    {
        var filePath = Path.Combine(_sessionsDirectory, $"{sessionId}.jsonl");

        var sessionStore = new SessionStore();

        // 确保文件存在（即使为空）
        if (!File.Exists(filePath))
        {
            using var _ = File.Create(filePath);
        }

        return Task.FromResult((sessionStore, filePath));
    }

    /// <summary>
    /// 从指定 ID 加载现有会话
    /// </summary>
    /// <param name="sessionId">要加载的会话 ID（不带扩展名）</param>
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
    /// 如果存在则加载最新的会话文件
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
    /// 删除会话文件
    /// </summary>
    /// <param name="sessionId">要删除的会话 ID（不带扩展名）</param>
    public void DeleteSession(string sessionId)
    {
        var filePath = Path.Combine(_sessionsDirectory, $"{sessionId}.jsonl");

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    /// <summary>
    /// 获取会话文件的元数据信息
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
    /// 通过计算行数来统计 JSONL 文件中的消息数量
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
            // 如果无法读取文件，则返回 0
            return 0;
        }
    }
}

/// <summary>
/// 会话文件的信息
/// </summary>
public record SessionInfo(
    string SessionId,
    string FilePath,
    DateTime CreationTime,
    DateTime LastModified,
    long SizeBytes,
    int MessageCount
);
