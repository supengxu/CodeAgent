using CodeAgentDemo.Models;

namespace CodeAgentDemo.Core;

/// <summary>
/// 控制台 UI 管理器接口
/// </summary>
public interface IConsoleUI
{
    /// <summary>
    /// 打印欢迎横幅
    /// </summary>
    void PrintBanner();

    /// <summary>
    /// 打印提示符
    /// </summary>
    void PrintPrompt();

    /// <summary>
    /// 开始流式文本输出
    /// </summary>
    void BeginStream();

    /// <summary>
    /// 结束流式输出
    /// </summary>
    void EndStream();

    /// <summary>
    /// 流式输出文本内容
    /// </summary>
    void StreamText(string delta);

    /// <summary>
    /// 流式输出思考内容
    /// </summary>
    void StreamThinking(string delta);

    /// <summary>
    /// 打印工具调用开始
    /// </summary>
    void PrintToolCallStart(string toolName, string arguments);

    /// <summary>
    /// 打印工具调用检测到
    /// </summary>
    void PrintToolCallDetected();

    /// <summary>
    /// 打印工具执行确认提示
    /// </summary>
    void PrintToolConfirmation(string toolName);

    /// <summary>
    /// 打印工具执行中状态
    /// </summary>
    void PrintToolExecuting(string toolName);

    /// <summary>
    /// 打印工具执行结果
    /// </summary>
    void PrintToolResult(string toolName, string result, bool success);

    /// <summary>
    /// 打印错误信息
    /// </summary>
    void PrintError(string message);

    /// <summary>
    /// 打印警告信息
    /// </summary>
    void PrintWarning(string message);

    /// <summary>
    /// 打印分隔线
    /// </summary>
    void PrintSeparator();

    /// <summary>
    /// 打印会话历史消息
    /// </summary>
    void DisplaySessionHistory(IReadOnlyList<ChatMessage> messages);

    /// <summary>
    /// 打印会话统计
    /// </summary>
    void PrintStats(int messageCount, int toolCallCount);

    /// <summary>
    /// 打印响应统计
    /// </summary>
    void PrintResponseUsage(DateTime startTime, DateTime endTime, int? inputTokens, int? outputTokens);

    /// <summary>
    /// 打印退出消息
    /// </summary>
    void PrintGoodbye();
}