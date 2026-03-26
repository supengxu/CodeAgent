using CodeAgentDemo.Core;

namespace CodeAgentDemo.Examples;

/// <summary>
/// 命令行会话管理辅助工具。</summary>
public static class SessionCliHelper
{
    /// <summary>
    /// 解析命令行参数以确定会话行为。</summary>
    public static async Task<(SessionStore? sessionStore, string sessionFilePath)> ParseSessionArgsAsync(string[] args)
    {
        SessionStore? sessionStore = null;
        string sessionFilePath = string.Empty;

        var useSession = false;
        var sessionId = string.Empty;
        var listSessions = false;

        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--session" && i + 1 < args.Length)
            {
                sessionId = args[++i];
                useSession = true;
            }
            else if (args[i] == "--list-sessions" || args[i] == "-ls")
            {
                listSessions = true;
            }
        }

        // 初始化会话目录
        var sessionsDir = Path.Combine(Directory.GetCurrentDirectory(), "sessions");
        Directory.CreateDirectory(sessionsDir);

        if (listSessions)
        {
            await PrintAvailableSessions(sessionsDir);
        }

        if (useSession)
        {
            // 尝试加载现有会话
            var sessionManager = new SessionManager(sessionsDir);
            var result = sessionId != "latest"
                ? await sessionManager.LoadSessionAsync(sessionId)
                : await sessionManager.LoadMostRecentSessionAsync();

            if (result.HasValue)
            {
                sessionStore = result.Value.store;
                sessionFilePath = result.Value.filePath;
                Console.WriteLine($"Loaded session from: {sessionFilePath}");
            }
            else
            {
                // 未找到会话，创建新的
                sessionId = sessionId == "latest" ? GenerateNewSessionId() : sessionId;
                sessionFilePath = Path.Combine(sessionsDir, $"{sessionId}.jsonl");

                // 创建新会话并保存
                sessionStore = new SessionStore();
                var persistence = new SessionPersistence();
                await persistence.SaveSessionAsync(sessionStore, sessionFilePath);

                Console.WriteLine($"Created new session: {sessionFilePath}");
            }
        }
        else if (!listSessions)
        {
            // 默认行为：创建带时间戳 ID 的新会话
            var sessionIdStr = GenerateNewSessionId();
            sessionFilePath = Path.Combine(sessionsDir, $"{sessionIdStr}.jsonl");

            sessionStore = new SessionStore();
            var persistence = new SessionPersistence();
            await persistence.SaveSessionAsync(sessionStore, sessionFilePath);

            Console.WriteLine($"Started new session: {sessionFilePath}");
        }

        return (sessionStore, sessionFilePath);
    }

    /// <summary>
    /// 打印所有可用的会话。</summary>
    /// <param name="sessionsDir"></param>
    private static Task PrintAvailableSessions(string sessionsDir)
    {
        var sessionManager = new SessionManager(sessionsDir);
        var sessions = sessionManager.ListSessions();

        if (sessions.Count == 0)
        {
            Console.WriteLine("No sessions found.\n");
            return Task.CompletedTask;
        }

        Console.WriteLine("Available sessions:");
        Console.WriteLine("ID                   | Created            | Modified           | Messages | Size");
        Console.WriteLine("---------------------|--------------------|--------------------|----------|-------");

        foreach (var session in sessions)
        {
            Console.WriteLine($"{session.SessionId,-20} | " +
                             $"{session.CreationTime:yyyy-MM-dd HH:mm:ss} | " +
                             $"{session.LastModified:yyyy-MM-dd HH:mm:ss} | " +
                             $"{session.MessageCount,-8} | " +
                             $"{FormatFileSize(session.SizeBytes)}");
        }

        Console.WriteLine();
        return Task.CompletedTask;
    }

    private static string GenerateNewSessionId()
    {
        return DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" +
               Path.GetRandomFileName().Substring(0, 8).Replace(".", "");
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}