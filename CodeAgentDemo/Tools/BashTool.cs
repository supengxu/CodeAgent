using System.Diagnostics;
using System.Text.Json;

namespace CodeAgentDemo.Tools;

public class BashTool : ITool
{
    private readonly string _workDir;

    public BashTool(string workDir)
    {
        _workDir = workDir;
    }

    public string Name => "bash";
    
    public string Description => "Execute a bash command";
    
    public JsonElement InputSchema => JsonDocument.Parse("""{"type":"object","properties":{"command":{"type":"string","description":"The bash command to execute"}},"required":["command"]}""").RootElement;

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments)
    {
        var command = arguments.GetProperty("command").GetString();
        if (string.IsNullOrEmpty(command))
        {
            return new ToolResult(false, "Command is required");
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{EscapeCommand(command)}\"",
                WorkingDirectory = _workDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();
            
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            
            await Task.WhenAll(outputTask, errorTask);
            process.WaitForExit((int)TimeSpan.FromMinutes(5).TotalMilliseconds);

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

    private static string EscapeCommand(string command)
    {
        return command.Replace("\"", "\\\"");
    }
}
