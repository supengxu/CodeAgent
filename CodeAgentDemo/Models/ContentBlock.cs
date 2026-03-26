using System.Text.Json;

namespace CodeAgentDemo.Models;

/// <summary>
/// 消息中内容块的抽象基类。
/// </summary>
public abstract record ContentBlock;

/// <summary>
/// 表示文本内容块。
/// </summary>
public record TextBlock(string Text) : ContentBlock;

/// <summary>
/// 表示思考/推理内容块。
/// </summary>
public record ThinkingBlock(string Thinking) : ContentBlock;

/// <summary>
/// 表示工具使用内容块。
/// </summary>
public record ToolUseBlock(string Id, string Name, JsonElement Input) : ContentBlock;

/// <summary>
/// 表示工具结果内容块。
/// </summary>
public record ToolResultBlock(string ToolUseId, string Content, bool IsError = false) : ContentBlock;
