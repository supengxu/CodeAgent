using CodeAgentDemo.Core;
using CodeAgentDemo.Models;

namespace CodeAgentDemo.Cli;

public class SessionCli : ISessionCli
{
    private readonly SessionManager _sessionManager;
    private readonly SessionPersistence _persistence;
    private SessionStore _currentSession;
    private string _currentSessionFilePath;
    private string _sessionsDir;

    public SessionStore CurrentSession => _currentSession;
    public string CurrentSessionId => Path.GetFileNameWithoutExtension(_currentSessionFilePath);

    public SessionCli(string sessionsDir)
    {
        _sessionsDir = sessionsDir;
        Directory.CreateDirectory(_sessionsDir);

        _sessionManager = new SessionManager(_sessionsDir);
        _persistence = new SessionPersistence();
        _currentSession = new SessionStore();
        _currentSessionFilePath = string.Empty;
    }

    public async Task<CliResult> TryExecuteCommandAsync(string input)
    {
        if (string.IsNullOrWhiteSpace(input) || !input.StartsWith("/"))
        {
            return CliResult.NotACommand();
        }

        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0].ToLowerInvariant();
        var args = parts.Skip(1).ToArray();

        return command switch
        {
            "/new" => await HandleNewCommandAsync(args),
            "/switch" => await HandleSwitchCommandAsync(args),
            "/list" or "/ls" => HandleListCommand(),
            "/context" or "/ctx" => HandleContextCommand(),
            "/clear" => HandleClearCommand(args),
            "/help" or "/?" => HandleHelpCommand(),
            _ => CliResult.Fail($"Unknown command: {command}. Type /help for available commands.")
        };
    }

    public async Task InitializeAsync()
    {
        var result = await _sessionManager.LoadMostRecentSessionAsync();

        if (result.HasValue)
        {
            _currentSession = result.Value.store;
            _currentSessionFilePath = result.Value.filePath;
        }
        else
        {
            await CreateNewSessionAsync(null);
        }

        // 确保会话文件路径已正确初始化
        if (string.IsNullOrEmpty(_currentSessionFilePath))
        {
            throw new InvalidOperationException("Failed to initialize session: session file path is empty");
        }
    }

    public async Task SaveCurrentSessionAsync()
    {
        if (!string.IsNullOrEmpty(_currentSessionFilePath))
        {
            await _persistence.SaveSessionAsync(_currentSession, _currentSessionFilePath);
        }
    }

    public async Task AppendMessageAsync(ChatMessage message)
    {
        _currentSession.AddMessage(message);

        if (!string.IsNullOrEmpty(_currentSessionFilePath))
        {
            await _persistence.AppendToSessionAsync(message, _currentSessionFilePath);
        }
    }

    private async Task<CliResult> HandleNewCommandAsync(string[] args)
    {
        var sessionName = args.Length > 0 ? args[0] : null;
        await CreateNewSessionAsync(sessionName);

        var displayName = string.IsNullOrEmpty(sessionName) ? CurrentSessionId : sessionName;
        return CliResult.OkWithClear($"Created new session: {displayName}");
    }

    private async Task CreateNewSessionAsync(string? name)
    {
        var sessionId = string.IsNullOrEmpty(name)
            ? GenerateSessionId()
            : SanitizeSessionId(name);

        var (store, filePath) = await _sessionManager.CreateSessionAsync(sessionId);

        _currentSession = store;
        _currentSessionFilePath = filePath;
    }

    private async Task<CliResult> HandleSwitchCommandAsync(string[] args)
    {
        if (args.Length == 0)
        {
            return CliResult.Fail("Usage: /switch <session-id>");
        }

        var sessionId = args[0];
        var sessions = _sessionManager.ListSessions();
        var matches = sessions.Where(s => s.SessionId.StartsWith(sessionId, StringComparison.OrdinalIgnoreCase)).ToList();

        if (matches.Count == 0)
        {
            return CliResult.Fail($"Session not found: {sessionId}");
        }

        if (matches.Count > 1)
        {
            var matchList = string.Join(", ", matches.Select(s => s.SessionId));
            return CliResult.Fail($"Ambiguous prefix. Matches: {matchList}");
        }

        await SaveCurrentSessionAsync();

        var result = await _sessionManager.LoadSessionAsync(matches[0].SessionId);

        if (result.HasValue)
        {
            _currentSession = result.Value.store;
            _currentSessionFilePath = result.Value.filePath;
            return CliResult.OkWithClear($"Switched to session: {matches[0].SessionId} ({_currentSession.Count} messages)");
        }

        return CliResult.Fail($"Failed to load session: {matches[0].SessionId}");
    }

    private CliResult HandleListCommand()
    {
        var sessions = _sessionManager.ListSessions();

        if (sessions.Count == 0)
        {
            return CliResult.Ok("No sessions found.");
        }

        var output = new System.Text.StringBuilder();
        output.AppendLine("Available sessions:");
        output.AppendLine("ID                   | Modified           | Msgs | Size");
        output.AppendLine("---------------------|--------------------|------|-------");

        foreach (var session in sessions)
        {
            var current = session.SessionId == CurrentSessionId ? "*" : " ";
            output.AppendLine($"{current}{session.SessionId,-19} | " +
                              $"{session.LastModified:yyyy-MM-dd HH:mm} | " +
                              $"{session.MessageCount,-4} | " +
                              $"{FormatFileSize(session.SizeBytes)}");
        }

        return CliResult.Ok(output.ToString());
    }

    private CliResult HandleContextCommand()
    {
        var messageCount = _currentSession.Count;
        var estimatedTokens = EstimateTokens(_currentSession);

        var output = new System.Text.StringBuilder();
        output.AppendLine($"Current Session: {CurrentSessionId}");
        output.AppendLine($"Messages: {messageCount}");
        output.AppendLine($"Estimated Tokens: ~{estimatedTokens:N0}");
        output.AppendLine($"File: {_currentSessionFilePath}");

        return CliResult.Ok(output.ToString());
    }

    private CliResult HandleClearCommand(string[] args)
    {
        if (args.Length == 0 || args[0] != "--force")
        {
            return CliResult.Ok("Are you sure you want to clear this session? Type /clear --force to confirm.");
        }

        _currentSession.Clear();
        return CliResult.Ok("Session cleared.");
    }

    private CliResult HandleHelpCommand()
    {
        var help = @"
Available Commands:
  /new [name]       Create a new session (optional name)
  /switch <id>      Switch to an existing session (supports prefix match)
  /list, /ls        List all sessions
  /context, /ctx    Show current session statistics
  /clear --force    Clear current session (requires confirmation)
  /help, /?         Show this help message
";
        return CliResult.Ok(help);
    }

    private static string GenerateSessionId()
    {
        return DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" +
               Path.GetRandomFileName().Substring(0, 8).Replace(".", "");
    }

    private static string SanitizeSessionId(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Where(c => !invalid.Contains(c)).ToArray());
        return sanitized.Length > 0
            ? sanitized.Substring(0, Math.Min(sanitized.Length, 50))
            : GenerateSessionId();
    }

    private static int EstimateTokens(SessionStore session)
    {
        var totalChars = session.Messages
            .Sum(m => m.Content.Sum(b => b switch
            {
                TextBlock t => t.Text.Length,
                ThinkingBlock th => th.Thinking.Length,
                ToolUseBlock tu => tu.Name.Length + tu.Input.GetRawText().Length,
                ToolResultBlock tr => tr.Content.Length,
                _ => 0
            }));

        return totalChars / 4;
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}

public record CliResult(bool IsCommand, bool Success, string Message, bool ClearScreen = false)
{
    public static CliResult NotACommand() => new(false, true, string.Empty);
    public static CliResult Ok(string message) => new(true, true, message);
    public static CliResult OkWithClear(string message) => new(true, true, message, true);
    public static CliResult Fail(string message) => new(true, false, message);
}