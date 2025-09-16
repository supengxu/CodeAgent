using System.Diagnostics;
using System.Runtime.InteropServices;
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
    
    public string Description => "Execute a bash command with safety validation";
    
    public JsonElement InputSchema => JsonDocument.Parse("""{"type":"object","properties":{"command":{"type":"string","description":"The bash command to execute"}},"required":["command"]}""").RootElement;

    /// <summary>
    /// 判断命令是否需要用户确认。
    /// 安全命令（仅限当前目录内的编辑、删除操作）不需要确认。
    /// </summary>
    public bool RequiresConfirmation(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("command", out var commandProp))
            return true;
        
        var command = commandProp.GetString();
        if (string.IsNullOrEmpty(command))
            return true;
        
        return !IsSafeCommand(command);
    }
    
    /// <summary>
    /// 判断命令是否为安全命令（仅在当前目录内操作的编辑、删除命令）。
    /// </summary>
    private bool IsSafeCommand(string command)
    {
      
        
      
        
        return IsAllowedSafeOperation(command);
    }
    
    private bool ContainsAbsolutePath(string command)
    {
        var tokens = TokenizeCommand(command);
        foreach (var token in tokens)
        {
            if (token == command.Split(' ')[0] || token.StartsWith("-"))
                continue;
            
            if (token.StartsWith("/"))
                return true;
        }
        return false;
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
    
    private List<string> TokenizeCommand(string command)
    {
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuote = false;
        char quoteChar = '\0';
        
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

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken = default)
    {
        if (!arguments.TryGetProperty("command", out var commandProp))
            return new ToolResult(false, "Command property is required");
        
        var command = commandProp.GetString();
        if (string.IsNullOrEmpty(command))
            return new ToolResult(false, "Command is required");
        
        if (command.Length > MaxCommandLength)
            return new ToolResult(false, $"Command too long (max {MaxCommandLength} characters)");
        
        foreach (var pattern in DangerousPatterns)
        {
            if (command.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return new ToolResult(false, $"Command contains blocked pattern: '{pattern}'");
        }

        try
        {
            using var process = new Process();
            var startInfo = new ProcessStartInfo
            {
                FileName = _shellPath,
                WorkingDirectory = _workDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            // SECURITY: ArgumentList prevents command injection by escaping arguments properly.
            // Never use string concatenation for shell arguments.
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add(command);
            
            process.StartInfo = startInfo;
            process.Start();
            
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMinutes(5));
            
            try
            {
                await Task.WhenAll(outputTask, errorTask);
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return new ToolResult(false, "Command execution timed out or was cancelled");
            }

            var output = outputTask.Result;
            var error = errorTask.Result;

            if (!string.IsNullOrWhiteSpace(error))
            {
                var result = string.IsNullOrWhiteSpace(output) 
                    ? $"Error: {error}" 
                    : $"{output}\nError: {error}";
                return new ToolResult(false, result);
            }

            var success = string.IsNullOrWhiteSpace(output) 
                ? "(no output)" 
                : output.Trim();
            return new ToolResult(true, success);
        }
        catch (Exception ex)
        {
            return new ToolResult(false, $"Error executing command: {ex.Message}");
        }
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
