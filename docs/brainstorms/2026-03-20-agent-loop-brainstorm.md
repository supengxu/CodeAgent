---
date: 2026-03-20
topic: agent-loop
---

# Agent 循环实现

## 我们要构建什么

一个 C#/.NET 8 的 Agent 循环系统，参考 claw0 的设计模式（`while True` + `stop_reason`），并支持工具执行。直接使用 OpenAI 和 Anthropic 官方 SDK，支持两者协议之间的无缝切换。

**核心行为：**
1. 在会话中累积消息
2. 调用 LLM API 并流式输出
3. **深度思考**（可选）：模型先输出思考过程，再输出最终回复
4. 根据 `stop_reason` 分支：`end_turn` → 打印，`tool_use` → 执行工具并循环回去
5. 优雅处理错误，继续循环

## 为什么选择这个方案

**原生 SDK + Provider 抽象层**：
- 直接使用官方 SDK，获得完整的 API 功能支持
- 自定义 `IChatProvider` 接口统一 Anthropic 和 OpenAI 协议
- `ITool` 接口支持可扩展的工具注册
- `SessionStore` 封装消息历史

**为何不选 Microsoft.Extensions.AI：**
- 预览版本不稳定，API 可能变化
- 对深度思考等新功能支持滞后
- 增加不必要的抽象层

## 关键决策

| 决策 | 选择 | 理由 |
|------|------|------|
| **LLM SDK** | 原生 SDK | OpenAI + Anthropic 官方包，完整功能支持 |
| **提供商抽象** | `IChatProvider` 接口 | 自定义接口，统一两种协议差异 |
| **工具接口** | `ITool` | 自定义接口，控制 schema 生成和执行 |
| **配置方式** | 环境变量 + .env 文件 | 与 claw0 参考一致，简单且可移植 |
| **输出模式** | 流式输出 | 更好的用户体验，token 即时显示 |
| **深度思考** | 可选启用 | 支持 extended thinking，模型在回复前进行内部推理 |
| **错误处理** | 打印并继续 | 非阻塞，用户可重试 |
| **范围** | 循环 + 工具 + 深度思考 | 核心 agent 功能、工具执行及推理能力 |

## 架构设计

```
CodeAgentDemo/
├── Core/
│   ├── AgentLoop.cs          # 主循环，stop_reason 分支处理
│   ├── SessionStore.cs       # 消息历史管理
│   └── ToolRegistry.cs       # 工具注册和查找
├── Providers/
│   ├── IChatProvider.cs      # 统一接口
│   ├── AnthropicProvider.cs  # Anthropic SDK 封装
│   ├── OpenAIProvider.cs     # OpenAI SDK 封装
│   └── ChatProviderFactory.cs # 根据配置创建 Provider
├── Tools/
│   ├── ITool.cs              # 工具接口定义
│   ├── ToolSchema.cs         # JSON schema 生成
│   └── BashTool.cs           # 现有 - 重构为 ITool
├── Models/
│   ├── ChatMessage.cs        # 统一消息模型
│   ├── ChatResponse.cs       # 统一响应模型
│   └── ThinkingContent.cs    # 深度思考内容模型
└── Program.cs                # 入口点，使用 AgentLoop
```

## 核心接口设计

### IChatProvider

```csharp
public interface IChatProvider
{
    /// <summary>
    /// 流式完成聊天请求
    /// </summary>
    IAsyncEnumerable<StreamChunk> CompleteStreamingAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取提供商名称
    /// </summary>
    string ProviderName { get; }
}

public record StreamChunk(
    string? Text,                    // 文本内容
    string? Thinking,                // 思考内容（可选）
    ToolCall? ToolCall,              // 工具调用
    string? StopReason,              // end_turn, tool_use, max_tokens
    FinishDetails? FinishDetails     // 完成详情
);

public record ToolCall(string Id, string Name, JsonElement Arguments);
```

### ITool

```csharp
public interface ITool
{
    string Name { get; }
    string Description { get; }
    JsonElement InputSchema { get; }  // JSON Schema for input parameters
    Task<ToolResult> ExecuteAsync(JsonElement arguments);
}

public record ToolResult(bool Success, string Output);
```

### ChatMessage

```csharp
public record ChatMessage(
    ChatRole Role,                   // system, user, assistant
    IEnumerable<ContentBlock> Content
);

public abstract record ContentBlock;

public record TextBlock(string Text) : ContentBlock;
public record ThinkingBlock(string Thinking) : ContentBlock;
public record ToolUseBlock(string Id, string Name, JsonElement Input) : ContentBlock;
public record ToolResultBlock(string ToolUseId, string Content, bool IsError = false) : ContentBlock;

public enum ChatRole { System, User, Assistant }
```

## 配置结构

```env
# 提供商选择
AI_PROVIDER=Anthropic  # 或 OpenAI

# 深度思考配置
ENABLE_THINKING=false          # 是否启用深度思考
THINKING_BUDGET_TOKENS=10000   # 思考预算（token 数），0 表示无限制

# Anthropic 配置
ANTHROPIC_API_KEY=sk-ant-xxx
ANTHROPIC_MODEL=claude-sonnet-4-20250514
ANTHROPIC_API_URL=https://api.anthropic.com  # 可选，覆盖默认端点

# OpenAI 兼容配置
OPENAI_API_KEY=sk-xxx
OPENAI_MODEL=gpt-4
OPENAI_API_URL=https://api.openai.com/v1  # 可选，覆盖默认端点
```

## Stop Reason 流程

```
API 响应（流式）
    │
    ├── content block: "thinking"
    │       └── [深度思考模式] 流式打印思考内容（可选：隐藏/折叠显示）
    │
    ├── content block: "text"
    │       └── 流式打印最终回复文本
    │
    └── stop_reason:
        ├── "end_turn"
        │       └── 完成 → 继续循环等待输入
        │
        ├── "tool_use"
        │       ├── 提取 tool_name + arguments
        │       ├── 在 ToolRegistry 中查找工具
        │       ├── 执行工具 → 获取结果
        │       ├── 将 tool_result 追加到 messages
        │       └── 循环回到 API 调用
        │
        └── "max_tokens"
                └── 打印警告 + 部分输出 → 继续循环
```

## 深度思考说明

**Extended Thinking** 允许模型在生成最终回复前进行内部推理：

| 特性 | 说明 |
|------|------|
| **思考内容** | 模型的推理过程，显示"为什么这样回答" |
| **思考预算** | `thinking_budget_tokens` 控制最大思考长度 |
| **输出格式** | Anthropic 返回 `thinking` block + `text` block |
| **消息累积** | 思考内容需追加到 messages 中，保持上下文一致 |

## 依赖包

```xml
<!-- 官方 SDK -->
<PackageReference Include="Anthropic.SDK" Version="3.x" />
<PackageReference Include="OpenAI" Version="2.x" />

<!-- 工具库 -->
<PackageReference Include="System.Text.Json" Version="8.0.0" />
<PackageReference Include="DotNetEnv" Version="3.0.0" />  <!-- .env 加载 -->
```

## 原生 SDK 用法指南

### Anthropic SDK 示例

```csharp
using Anthropic.SDK;
using Anthropic.SDK.Messaging;

var client = new AnthropicClient(apiKey);

var messages = new List<Message>
{
    new() { Role = RoleType.User, Content = "Hello!" }
};

// 流式响应
var stream = client.Messages.StreamMessageAsync(
    new MessageParameters
    {
        Model = "claude-sonnet-4-20250514",
        Messages = messages,
        MaxTokens = 4096,
        Tools = tools  // 工具定义
    });

await foreach (var update in stream)
{
    if (update.Delta?.Text != null)
        Console.Write(update.Delta.Text);
    
    if (update.ContentBlock?.Type == "tool_use")
    {
        // 处理工具调用
    }
}
```

### OpenAI SDK 示例

```csharp
using OpenAI.Chat;

// 支持自定义端点（兼容 OpenAI 协议的第三方服务）
var client = new ChatClient(
    "gpt-4o", 
    apiKey,
    new OpenAIOptions 
    { 
        Endpoint = new Uri(Environment.GetEnvironmentVariable("OPENAI_API_URL") ?? "https://api.openai.com/v1")
    });

var messages = new List<ChatMessage>
{
    new SystemChatMessage("You are a helpful assistant."),
    new UserChatMessage("Hello!")
};

// 流式响应
await foreach (var update in client.CompleteChatStreamingAsync(messages))
{
    if (update.ContentUpdate.Count > 0)
        Console.Write(update.ContentUpdate[0].Text);
    
    if (update.ToolCallsUpdate.Count > 0)
    {
        // 处理工具调用
    }
}
```

### Provider 统一封装

```csharp
public class AnthropicProvider : IChatProvider
{
    private readonly AnthropicClient _client;
    
    public async IAsyncEnumerable<StreamChunk> CompleteStreamingAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var anthropicMessages = ConvertMessages(messages);
        var parameters = new MessageParameters
        {
            Model = options?.Model ?? "claude-sonnet-4-20250514",
            Messages = anthropicMessages,
            MaxTokens = options?.MaxTokens ?? 4096,
            Tools = ConvertTools(options?.Tools)
        };
        
        await foreach (var update in _client.Messages.StreamMessageAsync(parameters, cancellationToken))
        {
            yield return ConvertToChunk(update);
        }
    }
    
    // 消息格式转换...
    // 工具格式转换...
}
```

## 待定问题

1. **思考内容显示** - 深度思考内容默认显示还是折叠？（建议：可选显示，默认折叠）
2. **会话持久化** - 是否需要将会话保存到磁盘以便恢复？（后续考虑 - YAGNI）
3. **工具审批流程** - 执行工具前是否需要用户确认？（在 s03 等效版本中添加）
4. **速率限制** - 是否需要处理 API 速率限制并重试/退避？（需要时添加）

## 下一步

→ 运行 `/ce:plan` 获取详细实现步骤

## 参考资料

- claw0 s01_agent_loop.md: https://github.com/shareAI-lab/claw0/blob/main/sessions/zh/s01_agent_loop.md
- Anthropic SDK: https://github.com/tghamm/anthropic-sdk-dotnet
- OpenAI SDK: https://github.com/openai/openai-dotnet