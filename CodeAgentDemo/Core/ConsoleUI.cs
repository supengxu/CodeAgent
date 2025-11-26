using CodeAgentDemo.Models;

namespace CodeAgentDemo.Core;

/// <summary>
/// 控制台颜色定义
/// </summary>
public enum ConsoleTheme
{
    Default,      // 默认白色
    UserInput,    // 用户输入 - 绿色
    Assistant,    // AI 响应 - 白色
    Thinking,     // 思考内容 - 灰色
    ToolCall,     // 工具调用 - 青色
    ToolResult,   // 工具结果 - 蓝色
    Error,        // 错误 - 红色
    Warning,      // 警告 - 黄色
    Success,      // 成功 - 绿色
    Dim,          // 暗淡 - 灰色
    Accent        // 强调 - 品红
}

/// <summary>
/// 统一的控制台 UI 管理器，提供美观的输出格式化
/// </summary>
public class ConsoleUI
{
    private bool _isThinkingStreaming;
    private bool _thinkingNeedsPrefix;

    private static readonly Dictionary<ConsoleTheme, string> ThemeColors = new()
    {
        { ConsoleTheme.Default, "\x1b[0m" },
        { ConsoleTheme.UserInput, "\x1b[32m" },        // 绿色
        { ConsoleTheme.Assistant, "\x1b[0m" },         // 默认白色
        { ConsoleTheme.Thinking, "\x1b[90m" },        // 亮灰色
        { ConsoleTheme.ToolCall, "\x1b[36m" },        // 青色
        { ConsoleTheme.ToolResult, "\x1b[34m" },      // 蓝色
        { ConsoleTheme.Error, "\x1b[31m" },           // 红色
        { ConsoleTheme.Warning, "\x1b[33m" },         // 黄色
        { ConsoleTheme.Success, "\x1b[32m" },         // 绿色
        { ConsoleTheme.Dim, "\x1b[90m" },             // 灰色
        { ConsoleTheme.Accent, "\x1b[35m" }           // 品红
    };

    private static readonly string ResetColor = "\x1b[0m";

    /// <summary>
    /// 打印欢迎横幅
    /// </summary>
    public void PrintBanner()
    {
        const int boxWidth = 62; // 内容区域宽度

        Console.WriteLine();
        PrintBoxLine('╔', '╗', "═", boxWidth);
        PrintBoxContent("  CodeAgent - AI 编程助手", boxWidth);
        PrintBoxContent("", boxWidth);
        PrintBoxContent("  命令: quit 或 exit 退出", boxWidth);
        PrintBoxLine('╚', '╝', "═", boxWidth);
        Console.WriteLine();
    }

    /// <summary>
    /// 打印盒子边框线
    /// </summary>
    private void PrintBoxLine(char left, char right, string fill, int width)
    {
        WriteColor(left.ToString(), ConsoleTheme.Accent);
        WriteColor(string.Concat(Enumerable.Repeat(fill, width)), ConsoleTheme.Accent);
        WriteColor(right.ToString(), ConsoleTheme.Accent);
        Console.WriteLine();
    }

    /// <summary>
    /// 打印盒子内容行（自动对齐）
    /// </summary>
    private void PrintBoxContent(string content, int width)
    {
        WriteColor("║", ConsoleTheme.Accent);
        WriteColor(PadRightDisplay(content, width), ConsoleTheme.Default);
        WriteColor("║", ConsoleTheme.Accent);
        Console.WriteLine();
    }

    /// <summary>
    /// 按显示宽度右填充空格（中文字符占2个显示宽度）
    /// </summary>
    private static string PadRightDisplay(string text, int totalWidth)
    {
        var displayWidth = GetDisplayWidth(text);
        var padding = totalWidth - displayWidth;
        return text + new string(' ', Math.Max(0, padding));
    }

    /// <summary>
    /// 计算字符串的终端显示宽度
    /// </summary>
    private static int GetDisplayWidth(string text)
    {
        var width = 0;
        foreach (var c in text)
        {
            // CJK 统一汉字范围 + CJK 扩展等
            width += IsWideChar(c) ? 2 : 1;
        }
        return width;
    }

    /// <summary>
    /// 判断字符是否为宽字符（在终端占2个显示宽度）
    /// </summary>
    private static bool IsWideChar(char c)
    {
        // CJK 统一汉字、扩展A、扩展B、符号和标点、全角字符等
        return c >= 0x4E00 && c <= 0x9FFF  // CJK 统一汉字
            || c >= 0x3400 && c <= 0x4DBF  // CJK 扩展A
            || c >= 0x20000 && c <= 0x2A6DF // CJK 扩展B (需要代理对，这里简化处理)
            || c >= 0x3000 && c <= 0x303F  // CJK 符号和标点
            || c >= 0xFF00 && c <= 0xFFEF; // 全角字符
    }

    /// <summary>
    /// 打印提示符
    /// </summary>
    public void PrintPrompt()
    {
        Console.WriteLine();
        WriteColor("▶ ", ConsoleTheme.UserInput);
    }

    /// <summary>
    /// 开始流式文本输出
    /// </summary>
    public void BeginStream()
    {
        _isThinkingStreaming = false;
        _thinkingNeedsPrefix = true;
    }

    public void EndStream()
    {
        if (_isThinkingStreaming)
        {
            Console.WriteLine();
        }
        _isThinkingStreaming = false;
        Console.WriteLine();
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
            Console.WriteLine();
            _isThinkingStreaming = false;
        }

        // 直接输出，不换行
        WriteColor(delta, ConsoleTheme.Assistant);
    }

    /// <summary>
    /// 流式输出思考内容
    /// </summary>
    public void StreamThinking(string delta)
    {
        if (string.IsNullOrEmpty(delta)) return;

        if (!_isThinkingStreaming)
        {
            Console.WriteLine();
            WriteColor("┌─ 💭 思考 ─", ConsoleTheme.Thinking);
            Console.WriteLine();
            _isThinkingStreaming = true;
            _thinkingNeedsPrefix = true;
        }

        if (_thinkingNeedsPrefix)
        {
            WriteColor("  ", ConsoleTheme.Thinking);
            _thinkingNeedsPrefix = false;
        }

        foreach (var c in delta)
        {
            if (c == '\n')
            {
                Console.WriteLine();
                _thinkingNeedsPrefix = true;
            }
            else
            {
                WriteColor(c.ToString(), ConsoleTheme.Thinking);
            }
        }
    }

    /// <summary>
    /// 打印工具调用开始
    /// </summary>
    public void PrintToolCallStart(string toolName, string arguments)
    {
        Console.WriteLine();
        WriteColor("┌─ 🔧 工具调用 ", ConsoleTheme.ToolCall);
        WriteColor(toolName, ConsoleTheme.Accent);
        Console.WriteLine();
        WriteColor("│  参数: ", ConsoleTheme.ToolCall);
        WriteColor(TruncateJson(arguments, 100), ConsoleTheme.Dim);
        Console.WriteLine();
        WriteColor("└─", ConsoleTheme.ToolCall);
    }

    /// <summary>
    /// 打印工具调用开始（流式检测到时）
    /// </summary>
    public void PrintToolCallDetected()
    {
        Console.WriteLine();
        WriteColor("🔧 检测到工具调用...", ConsoleTheme.ToolCall);
    }

    /// <summary>
    /// 打印工具执行确认提示
    /// </summary>
    public void PrintToolConfirmation(string toolName)
    {
        Console.WriteLine();
        WriteColor("⚡ 执行工具: ", ConsoleTheme.Warning);
        WriteColor(toolName, ConsoleTheme.Accent);
        Console.WriteLine();
    }

    /// <summary>
    /// 打印工具执行中状态
    /// </summary>
    public void PrintToolExecuting(string toolName)
    {
        // 输出到同一行，不换行，以便后续可以覆盖
        WriteColor("  ⏳ 正在执行 ", ConsoleTheme.Warning);
        WriteColor(toolName, ConsoleTheme.Accent);
        WriteColor("...", ConsoleTheme.Dim);
        // 不换行，保持在当前行
    }

    /// <summary>
    /// 打印工具执行结果
    /// </summary>
    public void PrintToolResult(string toolName, string result, bool success)
    {
        // 清除当前行（"正在执行"状态），然后打印结果
        Console.Write("\r\x1b[2K");  // 回到行首并清除整行

        var theme = success ? ConsoleTheme.Success : ConsoleTheme.Error;
        var icon = success ? "✓" : "✗";

        WriteColor($"  {icon} ", theme);
        WriteColor(toolName, ConsoleTheme.ToolResult);
        WriteColor(success ? " 完成" : " 失败", theme);

        if (!string.IsNullOrEmpty(result))
        {
            Console.WriteLine();
            WriteColor("  ", ConsoleTheme.Default);
            WriteColor(TruncateText(result, 200), ConsoleTheme.Dim);
        }
        Console.WriteLine();
    }

    /// <summary>
    /// 打印错误信息
    /// </summary>
    public void PrintError(string message)
    {
        Console.WriteLine();
        WriteColor("❌ 错误: ", ConsoleTheme.Error);
        WriteColor(message, ConsoleTheme.Error);
        Console.WriteLine();
    }

    /// <summary>
    /// 打印警告信息
    /// </summary>
    public void PrintWarning(string message)
    {
        Console.WriteLine();
        WriteColor("⚠️  警告: ", ConsoleTheme.Warning);
        WriteColor(message, ConsoleTheme.Warning);
        Console.WriteLine();
    }

    /// <summary>
    /// 打印分隔线
    /// </summary>
    public void PrintSeparator()
    {
        WriteColor("─".PadRight(60, '─'), ConsoleTheme.Dim);
        Console.WriteLine();
    }

    /// <summary>
    /// 打印会话历史消息
    /// </summary>
    public void DisplaySessionHistory(IReadOnlyList<ChatMessage> messages)
    {
        PrintSeparator();
        var blockCount = messages is ICollection<ChatMessage> coll ? coll.Count : messages.Count();
        WriteColor($" Loading {blockCount} historical messages ", ConsoleTheme.Dim);
        Console.WriteLine();
        PrintSeparator();
        Console.WriteLine();

        foreach (var message in messages)
        {
            PrintMessageHeader(message);

            foreach (var block in message.Content)
            {
                switch (block)
                {
                    case TextBlock textBlock:
                        Console.WriteLine(textBlock.Text);
                        break;
                    case ThinkingBlock thinkingBlock:
                        Console.WriteLine($"[Thinking: {TruncateText(thinkingBlock.Thinking, 100)}]");
                        break;
                    case ToolUseBlock toolUseBlock:
                        Console.WriteLine($"[Tool Use: {toolUseBlock.Name}]");
                        break;
                    case ToolResultBlock toolResultBlock:
                        Console.WriteLine($"[Tool Result: {TruncateText(toolResultBlock.Content, 100)}]");
                        break;
                }
            }

            Console.WriteLine();
        }

        PrintSeparator();
    }

    private void PrintMessageHeader(ChatMessage message)
    {
        var roleColor = message.Role switch
        {
            ChatRole.User => ConsoleTheme.UserInput,
            ChatRole.Assistant => ConsoleTheme.Assistant,
            ChatRole.Tool => ConsoleTheme.ToolResult,
            _ => ConsoleTheme.Default
        };

        WriteColor($"┌─ {message.Role} ", ConsoleTheme.Accent);
        var blockCount = message.Content is ICollection<ContentBlock> col ? col.Count : message.Content.Count();
        WriteColor($"({blockCount} block(s))", roleColor);
        Console.WriteLine();
    }

    /// <summary>
    /// 打印会话统计
    /// </summary>
    public void PrintStats(int messageCount, int toolCallCount)
    {
        Console.WriteLine();
        WriteColor("📊 会话统计: ", ConsoleTheme.Dim);
        WriteColor($"{messageCount} 消息", ConsoleTheme.Accent);
        WriteColor(" · ", ConsoleTheme.Dim);
        WriteColor($"{toolCallCount} 工具调用", ConsoleTheme.Accent);
        Console.WriteLine();
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

        WriteColor("│  ⏱ ", ConsoleTheme.Dim);
        WriteColor(timeStr, ConsoleTheme.Accent);

        if (inputTokens.HasValue && outputTokens.HasValue)
        {
            WriteColor(" · ", ConsoleTheme.Dim);
            WriteColor($"📥 {inputTokens}", ConsoleTheme.ToolCall);
            WriteColor(" ", ConsoleTheme.Dim);
            WriteColor($"📤 {outputTokens}", ConsoleTheme.ToolResult);
            WriteColor(" tokens", ConsoleTheme.Dim);
        }

        Console.WriteLine();
    }

    /// <summary>
    /// 打印退出消息
    /// </summary>
    public void PrintGoodbye()
    {
        Console.WriteLine();
        WriteColor("👋 再见！", ConsoleTheme.UserInput);
        Console.WriteLine();
    }

    /// <summary>
    /// 带颜色输出
    /// </summary>
    private static void WriteColor(string text, ConsoleTheme theme)
    {
        Console.Write($"{ThemeColors[theme]}{text}{ResetColor}");
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