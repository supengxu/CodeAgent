using System.Text.Json;
using CodeAgentDemo.Models;
using Spectre.Console;

namespace CodeAgentDemo.Core;

/// <summary>
/// 使用 Spectre.Console 的控制台 UI 管理器，提供美观的输出格式化
/// </summary>
public class SpectreConsoleUI : IConsoleUI
{
    private bool _isThinkingStreaming;
    private bool _thinkingNeedsPrefix;

    /// <summary>
    /// 打印欢迎横幅
    /// </summary>
    public void PrintBanner()
    {
        AnsiConsole.WriteLine();

        var rule = new Rule("[bold magenta]CodeAgent - AI 编程助手[/]")
        {
            Justification = Justify.Left,
            Border = BoxBorder.Double,
            Style = new Style(Color.Magenta1)
        };
        AnsiConsole.Write(rule);

        AnsiConsole.WriteLine("  [grey]命令: quit 或 exit 退出[/]");

        var rule2 = new Rule("[grey][/]")
        {
            Justification = Justify.Left,
            Border = BoxBorder.Double,
            Style = new Style(Color.Magenta1)
        };
        AnsiConsole.Write(rule2);

        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// 打印提示符
    /// </summary>
    public void PrintPrompt()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Markup("[green]▶ [/]");
    }

    /// <summary>
    /// 开始流式文本输出
    /// </summary>
    public void BeginStream()
    {
        _isThinkingStreaming = false;
        _thinkingNeedsPrefix = true;

        // 清除"正在思考"提示（2行：空行 + 文字）
        AnsiConsole.Write("\x1b[2A\x1b[J");
    }

    /// <summary>
    /// 结束流式输出
    /// </summary>
    public void EndStream()
    {
        if (_isThinkingStreaming)
        {
            AnsiConsole.WriteLine();
        }
        _isThinkingStreaming = false;
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// 流式输出文本内容
    /// </summary>
    public void StreamText(string delta)
    {
        if (string.IsNullOrEmpty(delta)) return;

        // 如果之前在思考模式，先关闭
        if (_isThinkingStreaming)
        {
            AnsiConsole.WriteLine();
            _isThinkingStreaming = false;
        }

        // 直接输出，不换行
        AnsiConsole.Markup(delta);
    }

    /// <summary>
    /// 流式输出思考内容
    /// </summary>
    public void StreamThinking(string delta)
    {
        if (string.IsNullOrEmpty(delta)) return;

        if (!_isThinkingStreaming)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Markup("[grey]┌─ 💭 思考 ─[/]");
            AnsiConsole.WriteLine();
            _isThinkingStreaming = true;
            _thinkingNeedsPrefix = true;
        }

        if (_thinkingNeedsPrefix)
        {
            AnsiConsole.Markup("[grey]  [/]");
            _thinkingNeedsPrefix = false;
        }

        foreach (var c in delta)
        {
            if (c == '\n')
            {
                AnsiConsole.WriteLine();
                _thinkingNeedsPrefix = true;
            }
            else
            {
                AnsiConsole.Markup($"[grey]{c}[/]");
            }
        }
    }

    /// <summary>
    /// 打印工具调用开始
    /// </summary>
    public void PrintToolCallStart(string toolName, string arguments)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Markup("[cyan]┌─ 🔧 工具调用 [/]");
        AnsiConsole.Markup($"[magenta]{EscapeMarkup(toolName)}[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.Markup($"[cyan]│  参数: [/]");
        AnsiConsole.Markup($"[dim]{EscapeMarkup(TruncateJson(arguments, 100))}[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.Markup("[cyan]└─[/]");
    }

    /// <summary>
    /// 打印工具调用检测到
    /// </summary>
    public void PrintToolCallDetected()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Markup("[cyan]🔧 检测到工具调用...[/]");
    }

    /// <summary>
    /// 打印工具执行确认提示
    /// </summary>
    public void PrintToolConfirmation(string toolName)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Markup("[yellow]⚡ 执行工具: [/]");
        AnsiConsole.Markup($"[magenta]{EscapeMarkup(toolName)}[/]");
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// 打印工具执行中状态
    /// </summary>
    public void PrintToolExecuting(string toolName)
    {
        // 输出到同一行，不换行，以便后续可以覆盖
        AnsiConsole.Markup("[yellow]  ⏳ 正在执行 [/]");
        AnsiConsole.Markup($"[magenta]{EscapeMarkup(toolName)}[/]");
        AnsiConsole.Markup("[dim]...[/]");
        // 不换行，保持在当前行
    }

    /// <summary>
    /// 打印工具执行结果
    /// </summary>
    public void PrintToolResult(string toolName, string result, bool success)
    {
        // 清除当前行（"正在执行"状态），然后打印结果
        AnsiConsole.Write("\r\x1b[2K");  // 回到行首并清除整行

        var icon = success ? "✓" : "✗";
        var color = success ? "green" : "red";

        AnsiConsole.Markup($"  [{color}]{icon}[/] ");
        AnsiConsole.Markup($"[blue]{EscapeMarkup(toolName)}[/]");
        AnsiConsole.Markup($"  [{color}]{(success ? " 完成" : " 失败")}[/]");

        if (!string.IsNullOrEmpty(result))
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Markup($"[grey]  {EscapeMarkup(TruncateText(result, 200))}[/]");
        }
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// 打印错误信息
    /// </summary>
    public void PrintError(string message)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Markup($"[red]❌ 错误: {EscapeMarkup(message)}[/]");
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// 打印警告信息
    /// </summary>
    public void PrintWarning(string message)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Markup($"[yellow]⚠️  警告: {EscapeMarkup(message)}[/]");
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// 打印分隔线
    /// </summary>
    public void PrintSeparator()
    {
        AnsiConsole.Markup("[dim]" + new string('─', 60) + "[/]");
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// 打印会话历史消息
    /// </summary>
    public void DisplaySessionHistory(IReadOnlyList<ChatMessage> messages)
    {
        PrintSeparator();
        var blockCount = messages is ICollection<ChatMessage> coll ? coll.Count : messages.Count;
        AnsiConsole.Markup($"[dim] Loading {blockCount} historical messages [/]");
        AnsiConsole.WriteLine();
        PrintSeparator();
        AnsiConsole.WriteLine();

        foreach (var message in messages)
        {
            PrintMessageHeader(message);

            foreach (var block in message.Content)
            {
                switch (block)
                {
                    case TextBlock textBlock:
                        AnsiConsole.WriteLine(textBlock.Text);
                        break;
                    case ThinkingBlock thinkingBlock:
                        AnsiConsole.WriteLine($"[Thinking: {TruncateText(thinkingBlock.Thinking, 100)}]");
                        break;
                    case ToolUseBlock toolUseBlock:
                        AnsiConsole.WriteLine($"[Tool Use: {toolUseBlock.Name}]");
                        break;
                    case ToolResultBlock toolResultBlock:
                        AnsiConsole.WriteLine($"[Tool Result: {TruncateText(toolResultBlock.Content, 100)}]");
                        break;
                }
            }

            AnsiConsole.WriteLine();
        }

        PrintSeparator();
    }

    private void PrintMessageHeader(ChatMessage message)
    {
        var roleColor = message.Role switch
        {
            ChatRole.User => "green",
            ChatRole.Assistant => "default",
            ChatRole.Tool => "blue",
            _ => "default"
        };

        var blockCount = message.Content is ICollection<ContentBlock> col ? col.Count : message.Content.Count();
        AnsiConsole.Markup($"[magenta]┌─ {message.Role} [/]");
        AnsiConsole.Markup($"[{roleColor}]({blockCount} block(s))[/]");
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// 打印会话统计
    /// </summary>
    public void PrintStats(int messageCount, int toolCallCount)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Markup("[dim]📊 会话统计: [/]");
        AnsiConsole.Markup($"[magenta]{messageCount} 消息[/]");
        AnsiConsole.Markup("[dim] · [/]");
        AnsiConsole.Markup($"[magenta]{toolCallCount} 工具调用[/]");
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// 打印响应统计（时间和 token）
    /// </summary>
    public void PrintResponseUsage(DateTime startTime, DateTime endTime, int? inputTokens, int? outputTokens)
    {
        var duration = endTime - startTime;
        var timeStr = duration.TotalSeconds >= 1
            ? $"{duration.TotalSeconds:F1}s"
            : $"{duration.TotalMilliseconds:F0}ms";

        AnsiConsole.Markup("[dim]│  ⏱ [/]");
        AnsiConsole.Markup($"[magenta]{timeStr}[/]");

        if (inputTokens.HasValue && outputTokens.HasValue)
        {
            AnsiConsole.Markup("[dim] · [/]");
            AnsiConsole.Markup($"[cyan]📥 {inputTokens}[/]");
            AnsiConsole.Markup("[dim] [/]");
            AnsiConsole.Markup($"[blue]📤 {outputTokens}[/]");
            AnsiConsole.Markup("[dim] tokens[/]");
        }

        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// 打印退出消息
    /// </summary>
    public void PrintGoodbye()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Markup("[green]👋 再见！[/]");
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// 打印信息消息
    /// </summary>
    public void PrintInfo(string message)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Markup("[cyan]ℹ️  [/]");
        AnsiConsole.Markup($"[dim]{EscapeMarkup(message)}[/]");
        AnsiConsole.WriteLine();
    }

    public void PrintThinking()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Markup("[dim]🤔 正在思考...[/]");
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// 转义 Spectre.Console markup 特殊字符
    /// </summary>
    private static string EscapeMarkup(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return text.Replace("[", "[[").Replace("]", "]]");
    }

    /// <summary>
    /// 截断 JSON 字符串
    /// </summary>
    private static string TruncateJson(string json, int maxLength)
    {
        if (string.IsNullOrEmpty(json) || json.Length <= maxLength)
            return json;

        return json[..maxLength] + "...";
    }

    /// <summary>
    /// 截断文本
    /// </summary>
    private static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;

        // 移除换行符
        var singleLine = text.Replace("\n", " ").Replace("\r", "");
        return singleLine[..maxLength] + "...";
    }
}