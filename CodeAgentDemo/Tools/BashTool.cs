using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace CodeAgentDemo.Tools;

public class BashTool : ITool
{
    private readonly string _workDir;
    private readonly string _shellPath;

    private static readonly string[] DangerousPatterns =
    {
        "rm -rf /", "rm -rf /*", "mkfs", "dd if=/dev/zero",
        ":(){ :|:& };:", "> /dev/sd", "chmod -R 777 /",
        "curl | bash", "wget | bash", "curl | sh", "wget | sh"
    };

    private const int MaxCommandLength = 10000;
    private const int DefaultTimeoutMs = 120000;

    public BashTool(string workDir, string? shellPath = null)
    {
        if (string.IsNullOrWhiteSpace(workDir))
            throw new ArgumentException("Working directory cannot be null or empty", nameof(workDir));

        if (!Directory.Exists(workDir))
            throw new DirectoryNotFoundException($"Working directory does not exist: {workDir}");

        _workDir = workDir;
        _shellPath = shellPath ?? GetDefaultShellPath();
    }

    public string Name => "bash";

    public string Description =>
        "在持久 shell 会话中执行 bash 命令，支持可选超时和适当的安全措施。\n\n" +
        $"所有命令默认在 {_workDir} 中运行。如需在其他目录运行，请使用 `workdir` 参数。避免使用 `cd <目录> && <命令>` 模式，改用 `workdir` 参数。\n\n" +
        "重要：此工具用于终端操作（如 git、npm、docker 等）。不要用于文件操作（读取、写入、编辑、搜索、查找文件）——请使用专门的工具：\n" +
        "- 文件搜索：使用 Glob（不要用 find 或 ls）\n" +
        "- 内容搜索：使用 Grep（不要用 grep 或 rg）\n" +
        "- 读取文件：使用 Read（不要用 cat/head/tail）\n" +
        "- 编辑文件：使用 Edit（不要用 sed/awk）\n" +
        "- 写入文件：使用 Write（不要用 echo >/cat <<EOF）\n\n" +
        "用法说明：\n" +
        "- command 参数是必需的。\n" +
        "- 可指定可选超时（毫秒），默认 120000ms（2 分钟）。\n" +
        "- description 参数描述命令用途（5-10 个词）。";

    public JsonElement InputSchema => JsonDocument.Parse("""
    {
        "type": "object",
        "properties": {
            "command": {
                "type": "string",
                "description": "要执行的命令"
            },
            "timeout": {
                "type": "integer",
                "description": "可选超时时间（毫秒），默认 120000，最大 600000"
            },
            "workdir": {
                "type": "string",
                "description": "运行命令的工作目录，默认为项目目录。使用此参数而非 'cd' 命令。"
            },
            "description": {
                "type": "string",
                "description": "命令用途的简要描述（5-10 个词）"
            }
        },
        "required": ["command"]
    }
    """).RootElement;

    public bool RequiresConfirmation(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("command", out var commandProp))
            return true;

        var command = commandProp.GetString();
        if (string.IsNullOrEmpty(command))
            return true;

        return !IsSafeCommand(command);
    }

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken = default)
    {
        if (!arguments.TryGetProperty("command", out var commandProp))
            return new ToolResult(false, "command is required");

        var command = commandProp.GetString();
        if (string.IsNullOrEmpty(command))
            return new ToolResult(false, "command is required");

        if (command.Length > MaxCommandLength)
            return new ToolResult(false, $"Command too long (max {MaxCommandLength} characters)");

        foreach (var pattern in DangerousPatterns)
        {
            if (command.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return new ToolResult(false, $"Command contains blocked pattern: '{pattern}'");
        }

        var workDir = _workDir;
        if (arguments.TryGetProperty("workdir", out var workdirProp))
        {
            var customWorkDir = workdirProp.GetString();
            if (!string.IsNullOrEmpty(customWorkDir))
            {
                workDir = Path.GetFullPath(Path.IsPathRooted(customWorkDir) ? customWorkDir : Path.Combine(_workDir, customWorkDir));
                if (!Directory.Exists(workDir))
                    return new ToolResult(false, $"Working directory does not exist: {workDir}");
            }
        }

        var timeoutMs = DefaultTimeoutMs;
        if (arguments.TryGetProperty("timeout", out var timeoutProp))
        {
            var customTimeout = timeoutProp.GetInt32();
            if (customTimeout < 0)
                return new ToolResult(false, "timeout must be a positive number");
            timeoutMs = Math.Min(customTimeout, 600000);
        }

        var description = arguments.TryGetProperty("description", out var descProp) 
            ? descProp.GetString() ?? "Execute bash command" 
            : "Execute bash command";

        try
        {
            using var process = new Process();
            var startInfo = new ProcessStartInfo
            {
                FileName = _shellPath,
                WorkingDirectory = workDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add(command);

            process.StartInfo = startInfo;
            process.Start();

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            var outputTask = Task.Run(async () =>
            {
                using var reader = process.StandardOutput;
                while (await reader.ReadLineAsync(cancellationToken) is { } line)
                {
                    outputBuilder.AppendLine(line);
                }
            }, cancellationToken);

            var errorTask = Task.Run(async () =>
            {
                using var reader = process.StandardError;
                while (await reader.ReadLineAsync(cancellationToken) is { } line)
                {
                    errorBuilder.AppendLine(line);
                }
            }, cancellationToken);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));

            try
            {
                await Task.WhenAll(outputTask, errorTask);
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { }

                var timeoutMsg = timeoutMs >= 60000
                    ? $"{timeoutMs / 60000} minutes"
                    : $"{timeoutMs / 1000} seconds";

                return new ToolResult(false, $"Command execution timed out after {timeoutMsg}");
            }

            var output = outputBuilder.ToString();
            var error = errorBuilder.ToString();

            if (!string.IsNullOrWhiteSpace(error))
            {
                var result = string.IsNullOrWhiteSpace(output)
                    ? $"Error: {error.Trim()}"
                    : $"{output.Trim()}\nError: {error.Trim()}";
                return new ToolResult(process.ExitCode == 0, result);
            }

            return new ToolResult(process.ExitCode == 0, string.IsNullOrWhiteSpace(output) ? "(no output)" : output.Trim());
        }
        catch (Exception ex)
        {
            return new ToolResult(false, $"Error executing command: {ex.Message}");
        }
    }

    private bool IsSafeCommand(string command)
    {
        return IsAllowedSafeOperation(command);
    }

    private bool IsAllowedSafeOperation(string command)
    {
        var firstWord = command.TrimStart().Split(' ')[0];

        var safeCommands = new HashSet<string>
        {
            "ls", "cat", "head", "tail", "grep", "find", "wc", "sort", "uniq",
            "touch", "mkdir", "cp", "mv", "rm", "rmdir",
            "echo", "printf",
            "sed", "awk",
            "chmod", "chown",
            "git", "dotnet", "npm", "yarn", "pip",
            "code", "vim", "nano"
        };

        if (!safeCommands.Contains(firstWord))
            return false;

        if (firstWord == "rm")
        {
            if (command.Contains(" -rf /") || command.Contains(" -rf /*"))
                return false;

            var tokens = TokenizeCommand(command);
            foreach (var token in tokens)
            {
                if (token.StartsWith("-")) continue;
                if (token == "rm") continue;

                if (token == "*" || token == ".")
                    return false;
            }
        }

        return true;
    }

    private static List<string> TokenizeCommand(string command)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        var inQuote = false;
        var quoteChar = '\0';

        foreach (var c in command)
        {
            if ((c == '"' || c == '\'') && !inQuote)
            {
                inQuote = true;
                quoteChar = c;
            }
            else if (c == quoteChar && inQuote)
            {
                inQuote = false;
                quoteChar = '\0';
            }
            else if (c == ' ' && !inQuote)
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
            tokens.Add(current.ToString());

        return tokens;
    }

    private static string GetDefaultShellPath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var gitBash = @"C:\Program Files\Git\bin\bash.exe";
            if (File.Exists(gitBash))
                return gitBash;

            var wslBash = @"C:\Windows\System32\bash.exe";
            if (File.Exists(wslBash))
                return wslBash;

            return "bash.exe";
        }

        return "/bin/bash";
    }
}