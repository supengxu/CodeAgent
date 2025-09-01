---
title: Agent 循环实现
type: feat
status: active
date: 2026-03-20
origin: docs/brainstorms/2026-03-20-agent-loop-brainstorm.md
---

# Agent 循环实现

## Overview

实现一个 C#/.NET 8 的 Agent 循环系统，参考 claw0 设计模式（`while True` + `stop_reason`），支持工具执行和深度思考。使用 Anthropic SDK 和 OpenAI SDK 作为 LLM 提供商，通过自定义 `IChatProvider` 接口统一两种协议。

## Problem Statement

当前项目有一个骨架实现，`Program.cs` 引用了不存在的 `AgentLoop` 类。需要完整实现 Agent 循环的核心功能：

1. **消息累积** - 在会话中保持上下文
2. **流式输出** - 实时显示 LLM 响应
3. **工具调用** - 执行工具并返回结果
4. **深度思考** - 支持模型的 extended thinking 功能
5. **提供商切换** - 支持 Anthropic 和 OpenAI 协议

## Proposed Solution

采用 **Provider 模式** 架构：

```
AgentLoop (主循环)
    ├── IChatProvider (提供商抽象)
    │     ├── AnthropicProvider
    │     └── OpenAIProvider
    ├── ToolRegistry (工具注册)
    │     └── List<ITool>
    └── SessionStore (消息历史)
```

核心流程：
1. 用户输入 → 追加到 messages
2. 调用 IChatProvider.CompleteStreamingAsync()
3. 根据 stop_reason 分支处理
4. 工具执行后循环回步骤 2

## Technical Approach

### Architecture

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
│   ├── ContentBlock.cs       # 内容块类型
│   └── StreamChunk.cs        # 流式响应块
└── Program.cs                # 入口点，使用 AgentLoop
```

### Implementation Phases

#### Phase 1: Foundation (Models & Interfaces)

**目标**: 建立核心数据模型和接口定义

**文件列表**:
- [ ] `Models/ChatRole.cs` - 角色枚举（System, User, Assistant, Tool）
- [ ] `Models/ContentBlock.cs` - 内容块类型定义（TextBlock, ThinkingBlock, ToolUseBlock, ToolResultBlock）
- [ ] `Models/ChatMessage.cs` - 统一消息模型
- [ ] `Models/StreamChunk.cs` - 流式 delta 块（TextDelta, ThinkingDelta, ToolCallDelta）
- [ ] `Models/StreamResponse.cs` - 完整响应结果（Content, ToolCalls, StopReason）
- [ ] `Models/ToolCall.cs` - 工具调用模型
- [ ] `Models/ChatOptions.cs` - 聊天选项配置
- [ ] `Providers/IChatProvider.cs` - 提供商接口
- [ ] `Tools/ITool.cs` - 工具接口
- [ ] `Tools/ToolResult.cs` - 工具执行结果

**验收标准**:
- 所有模型可序列化/反序列化
- 接口定义清晰，包含 XML 文档注释
- StreamChunk/StreamResponse 分离正确

**StreamChunk vs StreamResponse 设计说明**:

```csharp
// 流式 delta - 每个流事件产生一个
public record StreamChunk(
    string? TextDelta,           // 文本增量
    string? ThinkingDelta,       // 思考增量
    ToolCallDelta? ToolCallDelta // 工具调用增量（部分更新）
);

// 工具调用增量 - 逐步构建完整工具调用
public record ToolCallDelta(
    string? Id,                  // 首次出现时有值
    string? Name,                // 函数名（可能分多次到达）
    string? ArgumentsDelta       // 参数 JSON 增量
);

// 完整响应 - 流结束后构建
public record StreamResponse(
    IEnumerable<ContentBlock> Content,  // 完整内容块
    IReadOnlyList<ToolCall> ToolCalls,  // 完整工具调用列表
    string StopReason                   // end_turn, tool_use, max_tokens
);

// 完整工具调用 - 执行时使用
public record ToolCall(
    string Id,
    string Name,
    JsonElement Arguments
);
```

**关键点**：
- `StreamChunk` 用于流式显示，每个事件增量很小
- `StreamResponse` 用于历史记录，需要完整内容
- 工具调用通过多个 delta 逐步构建，最后组装成 `ToolCall`

**预计工作量**: 1-2 小时

#### Phase 2: Core Implementation

**目标**: 实现核心循环逻辑

**文件列表**:
- [ ] `Core/SessionStore.cs` - 消息历史管理
- [ ] `Core/ToolRegistry.cs` - 工具注册表
- [ ] `Core/AgentLoop.cs` - 主循环实现

**AgentLoop 核心逻辑** (已修正):

```csharp
// Core/AgentLoop.cs
public class AgentLoop
{
    private readonly IChatProvider _provider;
    private readonly SessionStore _session;
    private readonly ToolRegistry _tools;
    private readonly ChatOptions _options;
    
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            // 1. 等待用户输入
            var input = await ReadUserInputAsync();
            if (ShouldExit(input)) break;
            
            _session.AddMessage(ChatRole.User, new TextBlock(input));
            
            // 2. 内层循环：处理工具调用
            while (true)
            {
                // 3. 收集完整响应（流式显示）
                var response = await CollectStreamingResponseAsync(cancellationToken);
                
                // 4. 将助手消息添加到历史
                _session.AddMessage(ChatRole.Assistant, response.Content);
                
                // 5. 根据 stop_reason 分支
                if (response.StopReason == "tool_use")
                {
                    // 执行所有工具调用
                    foreach (var toolCall in response.ToolCalls)
                    {
                        var result = await ExecuteToolAsync(toolCall);
                        // 添加工具结果消息
                        _session.AddMessage(ChatRole.Tool, new ToolResultBlock(
                            toolCall.Id, result.Output, !result.Success));
                    }
                    // 继续内层循环，重新调用 LLM
                    continue;
                }
                
                // end_turn 或 max_tokens，退出内层循环
                break;
            }
        }
    }
    
    private async Task<StreamResponse> CollectStreamingResponseAsync(CancellationToken ct)
    {
        var textBuilder = new StringBuilder();
        var thinkingBuilder = new StringBuilder();
        var toolCalls = new List<ToolCallBuilder>();
        
        await foreach (var chunk in _provider.CompleteStreamingAsync(
            _session.Messages, _options, ct))
        {
            // 流式显示
            if (chunk.TextDelta != null) Console.Write(chunk.TextDelta);
            if (chunk.ThinkingDelta != null) 
                Console.WriteLine($"[思考] {chunk.ThinkingDelta}");
            
            // 累积内容
            textBuilder.Append(chunk.TextDelta);
            thinkingBuilder.Append(chunk.ThinkingDelta);
            
            // 处理工具调用 delta
            if (chunk.ToolCallDelta != null)
            {
                AccumulateToolCall(toolCalls, chunk.ToolCallDelta);
            }
        }
        
        return new StreamResponse(
            Content: BuildContentBlocks(textBuilder, thinkingBuilder, toolCalls),
            ToolCalls: BuildToolCalls(toolCalls),
            StopReason: chunk.StopReason
        );
    }
}
```

**验收标准**:
- `SessionStore` 正确管理消息历史
- `ToolRegistry` 支持工具注册和查找
- `AgentLoop` 正确处理 stop_reason 分支

**预计工作量**: 2-3 小时

#### Phase 3: Provider Implementations

**目标**: 实现 Anthropic 和 OpenAI 提供商

**文件列表**:
- [ ] `Providers/AnthropicProvider.cs` - Anthropic SDK 封装
- [ ] `Providers/OpenAIProvider.cs` - OpenAI SDK 封装
- [ ] `Providers/ChatProviderFactory.cs` - 工厂类

**AnthropicProvider 关键实现**:

```csharp
// Providers/AnthropicProvider.cs
using Anthropic.SDK;
using Anthropic.SDK.Messaging;

public class AnthropicProvider : IChatProvider
{
    private readonly AnthropicClient _client;
    private readonly string _model;
    private readonly bool _enableThinking;
    private readonly int _thinkingBudget;
    
    public AnthropicProvider(string apiKey, string model, bool enableThinking = false, int thinkingBudget = 0)
    {
        _client = new AnthropicClient(apiKey);
        _model = model;
        _enableThinking = enableThinking;
        _thinkingBudget = thinkingBudget;
    }
    
    public string ProviderName => "Anthropic";
    
    public async IAsyncEnumerable<StreamChunk> CompleteStreamingAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var parameters = new MessageParameters
        {
            Model = _model,
            Messages = ConvertMessages(messages),
            MaxTokens = options?.MaxTokens ?? 4096,
            Tools = ConvertTools(options?.Tools),
            System = options?.SystemPrompt != null 
                ? new List<SystemMessage> { new SystemMessage(options.SystemPrompt) }
                : null,
            Thinking = _enableThinking ? new ThinkingParameters 
            { 
                Type = ThinkingType.enabled, 
                BudgetTokens = _thinkingBudget 
            } : null,
            Stream = true
        };
        
        await foreach (var update in _client.Messages.StreamClaudeMessageAsync(parameters, cancellationToken))
        {
            yield return ConvertToChunk(update);
        }
    }
    
    private List<Message> ConvertMessages(IEnumerable<ChatMessage> messages)
    {
        var result = new List<Message>();
        foreach (var msg in messages)
        {
            result.Add(new Message
            {
                Role = msg.Role switch
                {
                    ChatRole.User => RoleType.User,
                    ChatRole.Assistant => RoleType.Assistant,
                    _ => RoleType.User
                },
                Content = ConvertContent(msg.Content)
            });
        }
        return result;
    }
    
    private List<ContentBase> ConvertContent(IEnumerable<ContentBlock> content)
    {
        return content.Select(block => block switch
        {
            TextBlock t => (ContentBase)new TextContent { Text = t.Text },
            ThinkingBlock th => new ThinkingContent { Thinking = th.Thinking },
            ToolUseBlock tu => new ToolUseContent 
            { 
                Id = tu.Id, 
                Name = tu.Name, 
                Input = tu.Input 
            },
            ToolResultBlock tr => new ToolResultContent
            {
                ToolUseId = tr.ToolUseId,
                Content = tr.Content,
                IsError = tr.IsError
            },
            _ => new TextContent { Text = "" }
        }).ToList();
    }
    
    private List<Tool> ConvertTools(IEnumerable<ToolDefinition>? tools)
    {
        if (tools == null) return null;
        
        return tools.Select(t => new Tool
        {
            Name = t.Name,
            Description = t.Description,
            InputSchema = t.InputSchema
        }).ToList();
    }
    
    private StreamChunk ConvertToChunk(MessageResponse update)
    {
        string? textDelta = null;
        string? thinkingDelta = null;
        ToolCallDelta? toolCallDelta = null;
        string? stopReason = null;
        
        // 处理 delta
        if (update.Delta != null)
        {
            textDelta = update.Delta.Text;
            // Anthropic SDK v5+ 支持思考内容
            if (update.Delta is { } delta && delta.GetType().GetProperty("Thinking") != null)
            {
                thinkingDelta = delta.GetType().GetProperty("Thinking")?.GetValue(delta)?.ToString();
            }
        }
        
        // 处理 content block（工具调用）
        if (update.ContentBlock != null && update.ContentBlock.Type == "tool_use")
        {
            var toolBlock = update.ContentBlock as ToolUseContent;
            if (toolBlock != null)
            {
                toolCallDelta = new ToolCallDelta(
                    Id: toolBlock.Id,
                    Name: toolBlock.Name,
                    ArgumentsDelta: toolBlock.Input.ToString()
                );
            }
        }
        
        // 处理 stop_reason
        stopReason = update.StopReason;
        
        return new StreamChunk(textDelta, thinkingDelta, toolCallDelta);
    }
}
```

**OpenAIProvider 关键实现**:

```csharp
// Providers/OpenAIProvider.cs
using OpenAI;
using OpenAI.Chat;

public class OpenAIProvider : IChatProvider
{
    private readonly ChatClient _client;
    private readonly string _model;
    
    public OpenAIProvider(string apiKey, string model, string? endpoint = null)
    {
        _model = model;
        var options = endpoint != null 
            ? new OpenAIClientOptions { Endpoint = new Uri(endpoint) }
            : new OpenAIClientOptions();
        
        var openAIClient = new OpenAIClient(new ApiKeyCredential(apiKey), options);
        _client = openAIClient.GetChatClient(model);
    }
    
    public string ProviderName => "OpenAI";
    
    public async IAsyncEnumerable<StreamChunk> CompleteStreamingAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var openaiMessages = ConvertMessages(messages, options?.SystemPrompt);
        var chatOptions = ConvertOptions(options);
        
        await foreach (var update in _client.CompleteChatStreamingAsync(openaiMessages, chatOptions, cancellationToken))
        {
            yield return ConvertToChunk(update);
        }
    }
    
    private IEnumerable<OpenAI.Chat.ChatMessage> ConvertMessages(
        IEnumerable<ChatMessage> messages, 
        string? systemPrompt)
    {
        var result = new List<OpenAI.Chat.ChatMessage>();
        
        // OpenAI: System message 放在第一个
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            result.Add(new SystemChatMessage(systemPrompt));
        }
        
        foreach (var msg in messages)
        {
            result.Add(msg.Role switch
            {
                ChatRole.User => new UserChatMessage(ConvertContent(msg.Content)),
                ChatRole.Assistant => new AssistantChatMessage(ConvertContent(msg.Content)),
                ChatRole.Tool => ConvertToolResult(msg),
                _ => throw new ArgumentException($"Unknown role: {msg.Role}")
            });
        }
        
        return result;
    }
    
    private List<ChatMessageContentPart> ConvertContent(IEnumerable<ContentBlock> content)
    {
        return content.Select(block => block switch
        {
            TextBlock t => ChatMessageContentPart.CreateTextPart(t.Text),
            ThinkingBlock th => ChatMessageContentPart.CreateTextPart($"[Thinking] {th.Thinking}"),
            ToolUseBlock tu => ChatMessageContentPart.CreateTextPart($"[ToolUse: {tu.Name}]"),
            ToolResultBlock tr => ChatMessageContentPart.CreateTextPart(tr.Content),
            _ => ChatMessageContentPart.CreateTextPart("")
        }).ToList();
    }
    
    private StreamChunk ConvertToChunk(StreamingChatCompletionUpdate update)
    {
        string? textDelta = null;
        string? thinkingDelta = null;
        ToolCallDelta? toolCallDelta = null;
        
        if (update.ContentUpdate.Count > 0)
        {
            textDelta = update.ContentUpdate[0].Text;
        }
        
        // OpenAI 工具调用是增量形式
        if (update.ToolCallUpdates.Count > 0)
        {
            var tc = update.ToolCallUpdates[0];
            toolCallDelta = new ToolCallDelta(
                Id: tc.Id,
                Name: tc.FunctionName,
                ArgumentsDelta: tc.FunctionArgumentsUpdate
            );
        }
        
        return new StreamChunk(textDelta, thinkingDelta, toolCallDelta);
    }
}
```

**验收标准**:
- AnthropicProvider 正确处理流式响应和工具调用
- OpenAIProvider 支持自定义端点
- ChatProviderFactory 根据配置正确创建 Provider

**预计工作量**: 3-4 小时

#### Phase 4: Tool System

**目标**: 完善工具系统

**文件列表**:
- [ ] `Tools/ToolSchema.cs` - JSON Schema 生成器
- [ ] `Tools/BashTool.cs` - 重构为 ITool 实现

**BashTool 重构**:

```csharp
// Tools/BashTool.cs
public class BashTool : ITool
{
    private readonly string _workDir;
    
    public string Name => "bash";
    public string Description => "Execute a bash command";
    
    public JsonElement InputSchema => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "command": {
                    "type": "string",
                    "description": "The bash command to execute"
                }
            },
            "required": ["command"]
        }
        """).RootElement;
    
    public async Task<ToolResult> ExecuteAsync(JsonElement arguments)
    {
        var command = arguments.GetProperty("command").GetString()!;
        // 执行命令...
        return new ToolResult(true, output);
    }
}
```

**验收标准**:
- BashTool 实现 ITool 接口
- JSON Schema 正确生成
- 工具执行结果正确返回

**预计工作量**: 1-2 小时

#### Phase 5: Integration & Entry Point

**目标**: 集成所有组件，更新入口点

**文件列表**:
- [ ] `Program.cs` - 更新入口点
- [ ] `.env.example` - 环境变量示例
- [ ] `CodeAgentDemo.csproj` - 更新依赖

**Program.cs 更新**:

```csharp
// Program.cs
using CodeAgentDemo.Core;
using CodeAgentDemo.Providers;
using CodeAgentDemo.Tools;
using DotNetEnv;

Env.Load();

var provider = ChatProviderFactory.Create();
var tools = new ToolRegistry();
tools.Register(new BashTool(Directory.GetCurrentDirectory()));

var options = new ChatOptions
{
    SystemPrompt = "You are a helpful coding assistant.",
    MaxTokens = 4096,
    EnableThinking = bool.Parse(Environment.GetEnvironmentVariable("ENABLE_THINKING") ?? "false")
};

var agent = new AgentLoop(provider, tools, options);

Console.WriteLine("CodeAgent - Agent Loop");
Console.WriteLine("Enter your prompt (or 'quit' to exit):\n");

await agent.RunAsync();
```

**验收标准**:
- 程序正确启动并加载配置
- 用户可以与 Agent 交互
- 工具调用正常工作

**预计工作量**: 1 小时

## System-Wide Impact

### Interaction Graph

```
User Input
    │
    ▼
Program.cs (Entry)
    │
    ├── ChatProviderFactory.Create()
    │       └── 读取环境变量 → 创建 IChatProvider
    │
    ├── ToolRegistry.Register()
    │       └── 注册工具实现
    │
    └── AgentLoop.RunAsync()
            │
            ├── SessionStore.AddMessage()
            │
            ├── IChatProvider.CompleteStreamingAsync()
            │       │
            │       ├── AnthropicProvider → Anthropic SDK
            │       └── OpenAIProvider → OpenAI SDK
            │
            └── ProcessChunkAsync()
                    │
                    ├── Console.Write (输出)
                    └── ToolRegistry.Execute() → ITool.ExecuteAsync()
```

### Error Propagation

| 层级 | 错误类型 | 处理方式 |
|------|----------|----------|
| SDK 层 | API 错误 (401, 429, 500) | 捕获异常，打印错误信息，继续循环 |
| SDK 层 | 网络超时/中断 | 捕获异常，提示重试 |
| Provider 层 | 消息转换错误 | 记录日志，返回错误 chunk |
| Provider 层 | JSON 解析错误 (工具参数) | 返回错误结果给 LLM |
| AgentLoop 层 | 工具执行错误 | 返回 ToolResultBlock(IsError=true) |
| AgentLoop 层 | 流中断（部分响应） | 不添加到历史，提示重新输入 |
| AgentLoop 层 | 工具执行超时 | 返回超时错误给 LLM |
| AgentLoop 层 | 消息历史过大 | 后续：自动截断旧消息 |
| 用户层 | Ctrl+C | 优雅退出 |

### State Lifecycle

- **Session**: 内存中保存，程序退出后丢失
- **Messages**: 按 FIFO 顺序累积，无大小限制
- **Tools**: 启动时注册，运行时不可变

### API Surface Parity

Anthropic 和 OpenAI 的差异：

| 功能 | Anthropic | OpenAI |
|------|-----------|--------|
| 消息格式 | Content 数组 | 字符串/消息类型 |
| 工具调用 | tool_use block | tool_calls 数组 |
| 思考内容 | thinking block | 无原生支持 |
| Stop Reason | end_turn/tool_use/max_tokens | stop/tool_calls/length |

`IChatProvider` 接口统一这些差异。

### System Message 处理

Anthropic 和 OpenAI 处理 System Message 的方式不同：

| 提供商 | 方式 |
|--------|------|
| **Anthropic** | `system` 参数（不在 messages 数组中） |
| **OpenAI** | 第一个消息 role=`system` |

**IChatProvider 接口设计**:

```csharp
public interface IChatProvider
{
    IAsyncEnumerable<StreamChunk> CompleteStreamingAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default);
    
    string ProviderName { get; }
}

public class ChatOptions
{
    public string? SystemPrompt { get; set; }  // 统一系统提示
    public int MaxTokens { get; set; } = 4096;
    public bool EnableThinking { get; set; }
    public int ThinkingBudgetTokens { get; set; }
    public IEnumerable<ToolDefinition>? Tools { get; set; }
}
```

**Provider 实现时转换**:
- `AnthropicProvider`: 将 SystemPrompt 放入 `MessageParameters.System`
- `OpenAIProvider`: 将 SystemPrompt 转换为第一个 SystemChatMessage

## Acceptance Criteria

### Functional Requirements

- [ ] **F1**: 支持多轮对话，消息正确累积
- [ ] **F2**: 流式输出，token 实时显示
- [ ] **F3**: 工具调用，结果正确返回给 LLM
- [ ] **F4**: Anthropic Provider 正常工作
- [ ] **F5**: OpenAI Provider 正常工作（含自定义端点）
- [ ] **F6**: 深度思考功能可选启用
- [ ] **F7**: 配置通过环境变量加载

### Non-Functional Requirements

- [ ] **NF1**: 启动时间 < 2 秒
- [ ] **NF2**: 内存占用 < 100MB（空闲状态）
- [ ] **NF3**: 支持 Ctrl+C 优雅退出

### Quality Gates

- [ ] **Q1**: 代码通过 `dotnet build` 无错误
- [ ] **Q2**: 所有文件有 XML 文档注释
- [ ] **Q3**: 遵循 .NET 命名约定

## Test Scenarios

### Unit Test Scenarios

| 场景 | 描述 | 预期结果 |
|------|------|----------|
| **UT-01** | SessionStore 添加消息 | 消息正确追加到历史 |
| **UT-02** | SessionStore 角色顺序 | User → Assistant → User 正确 |
| **UT-03** | ToolRegistry 注册工具 | 工具可通过 Name 查找 |
| **UT-04** | ToolRegistry 工具不存在 | 返回 null 或抛出异常 |
| **UT-05** | StreamChunk 累积文本 | 多个 delta 正确拼接 |
| **UT-06** | ToolCallDelta 组装 | 多个 delta 组装为完整 ToolCall |
| **UT-07** | ChatOptions 默认值 | MaxTokens=4096, EnableThinking=false |
| **UT-08** | ConfigurationValidator 有效配置 | 无异常抛出 |
| **UT-09** | ConfigurationValidator 缺少 API Key | 抛出 InvalidOperationException |

### Integration Test Scenarios

| 场景 | 描述 | 预期结果 |
|------|------|----------|
| **IT-01** | Anthropic 简单对话 | 返回文本响应 |
| **IT-02** | Anthropic 流式输出 | 文本逐步显示 |
| **IT-03** | Anthropic 工具调用 | tool_use → 执行 → tool_result → 最终响应 |
| **IT-04** | Anthropic 深度思考 | 返回 thinking + text 内容 |
| **IT-05** | Anthropic 多轮对话 | 上下文正确保持 |
| **IT-06** | OpenAI 简单对话 | 返回文本响应 |
| **IT-07** | OpenAI 自定义端点 | 正确连接到指定 URL |
| **IT-08** | OpenAI 工具调用 | tool_calls → 执行 → 最终响应 |
| **IT-09** | API 错误处理 | 401 错误显示友好信息 |
| **IT-10** | 网络超时处理 | 显示超时错误，允许重试 |

### End-to-End Test Scenarios

```gherkin
Feature: Agent Loop

Scenario: 多轮对话保持上下文
  Given Agent 已启动
  When 用户输入 "法国的首都是哪里？"
  And 用户输入 "它的人口是多少？"
  Then Agent 应该回答巴黎的人口
  And 不需要用户重新说明"法国"

Scenario: 工具调用循环
  Given Agent 已启动并注册 BashTool
  When 用户输入 "列出当前目录的文件"
  Then Agent 应该调用 bash 工具
  And 显示命令执行结果
  And 可能根据结果进行后续操作

Scenario: 错误恢复
  Given Agent 已启动
  When API 调用失败（如网络错误）
  Then 显示错误信息
  And 用户可以重新输入
  And 之前的对话历史保持

Scenario: 深度思考模式
  Given ENABLE_THINKING=true
  And Agent 已启动
  When 用户输入复杂问题
  Then 显示 [思考] 内容
  And 显示最终回答
```

### Provider-Specific Test Cases

#### AnthropicProvider 测试

```csharp
[Fact]
public async Task AnthropicProvider_WithThinking_ReturnsThinkingContent()
{
    var provider = new AnthropicProvider(apiKey, "claude-sonnet-4-20250514", enableThinking: true, thinkingBudget: 1000);
    var messages = new[] { new ChatMessage(ChatRole.User, new TextBlock("What is 2+2?")) };
    
    var chunks = new List<StreamChunk>();
    await foreach (var chunk in provider.CompleteStreamingAsync(messages))
    {
        chunks.Add(chunk);
    }
    
    // 验证有 thinking delta 或 text delta
    Assert.True(chunks.Any(c => c.ThinkingDelta != null || c.TextDelta != null));
}

[Fact]
public async Task AnthropicProvider_ToolUse_ExecutesAndReturns()
{
    var provider = new AnthropicProvider(apiKey, "claude-sonnet-4-20250514");
    var tools = new[] { new ToolDefinition("bash", "Execute command", bashSchema) };
    var options = new ChatOptions { Tools = tools };
    
    // ... 验证工具调用流程
}
```

#### OpenAIProvider 测试

```csharp
[Fact]
public async Task OpenAIProvider_CustomEndpoint_Connects()
{
    var customUrl = "https://custom-llm.example.com/v1";
    var provider = new OpenAIProvider(apiKey, "custom-model", customUrl);
    
    // 验证连接到自定义端点
    var exception = await Record.ExceptionAsync(async () =>
    {
        await foreach (var _ in provider.CompleteStreamingAsync(messages)) { }
    });
    
    // 可能因模型不存在而失败，但应成功连接
    Assert.IsNotType<UriFormatException>(exception);
}
```

## Success Metrics

| 指标 | 目标 |
|------|------|
| 功能完整性 | 所有 Acceptance Criteria 通过 |
| 代码质量 | 无编译错误，无警告 |
| 用户体验 | 响应流畅，无明显延迟 |

## Dependencies & Prerequisites

### NuGet 依赖

```xml
<!-- 核心依赖 -->
<PackageReference Include="Anthropic.SDK" Version="5.10.0" />
<PackageReference Include="OpenAI" Version="2.*" />
<PackageReference Include="DotNetEnv" Version="3.*" />
<PackageReference Include="System.Text.Json" Version="8.0.0" />
```

> **注意**: Anthropic.SDK v5.10.0+ 已支持 Extended Thinking，无需额外封装。

### 环境变量

```env
AI_PROVIDER=Anthropic
ANTHROPIC_API_KEY=sk-ant-xxx
ANTHROPIC_MODEL=claude-sonnet-4-20250514
ENABLE_THINKING=false
THINKING_BUDGET_TOKENS=10000
```

### 配置验证

启动时验证配置：

```csharp
public static class ConfigurationValidator
{
    public static void Validate()
    {
        var provider = Environment.GetEnvironmentVariable("AI_PROVIDER");
        if (string.IsNullOrEmpty(provider))
            throw new InvalidOperationException("AI_PROVIDER is required");
        
        var apiKey = Environment.GetEnvironmentVariable($"{provider}_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException($"{provider}_API_KEY is required");
        
        var thinkingBudget = Environment.GetEnvironmentVariable("THINKING_BUDGET_TOKENS");
        if (thinkingBudget != null && int.Parse(thinkingBudget) > 100000)
            Console.WriteLine("Warning: THINKING_BUDGET_TOKENS > 100000 may cause slow responses");
    }
}
```

## Risk Analysis & Mitigation

| 风险 | 影响 | 状态 | 缓解措施 |
|------|------|------|----------|
| Anthropic SDK 不支持 thinking | 中 | ✅ 已解决 | SDK v5.10.0+ 已支持 Extended Thinking |
| OpenAI 自定义端点不兼容 | 低 | ✅ 已验证 | 使用 `OpenAIClientOptions.Endpoint` 配置 |
| 消息历史过大 | 中 | 待处理 | 后续添加 token 计数和截断 |

## Deep Research Findings

### Anthropic.SDK Extended Thinking 支持

**确认**: Anthropic.SDK (v5.10.0+) 已完整支持 Extended Thinking 功能。

**API 用法**:

```csharp
// 原生 API
var parameters = new MessageParameters()
{
    Messages = messages,
    MaxTokens = 4096,
    Model = AnthropicModels.Claude46Sonnet,
    Thinking = new ThinkingParameters()
    {
        BudgetTokens = 4000  // 思考预算
    }
};
var res = await client.Messages.GetClaudeMessageAsync(parameters);
var thoughts = res.Message.ThinkingContent;  // 获取思考内容

// IChatClient 集成 (推荐)
using Anthropic.SDK.Extensions;

IChatClient client = new AnthropicClient().Messages;
ChatOptions options = new()
{
    ModelId = AnthropicModels.Claude46Sonnet,
}.WithThinking(4000);  // 启用深度思考

// 自适应思考 (Claude 4.6+)
ChatOptions options = new()
{
    ModelId = AnthropicModels.Claude46Sonnet,
}.WithAdaptiveThinking();  // Claude 自动决定何时思考

// 交错思考 (高级)
ChatOptions options = new()
{
    ModelId = AnthropicModels.Claude46Sonnet,
}.WithInterleavedThinking(8000);  // 允许思考 token 超过 max_tokens
```

**关键发现**:
- 支持 `BudgetTokens` 控制思考长度
- 支持 `AdaptiveThinking` 让 Claude 自动决定
- 支持 `InterleavedThinking` 允许思考 token 超过输出限制
- SDK 已实现 `ThinkingContent` 类型处理流式思考内容

### OpenAI SDK 自定义端点

**确认**: OpenAI SDK 支持通过 `OpenAIClientOptions.Endpoint` 配置自定义端点。

**API 用法**:

```csharp
// 自定义端点配置
var clientOptions = new OpenAIClientOptions
{
    Endpoint = new Uri("https://your-custom-endpoint.com/v1")
};

var openAIClient = new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions);
var chatClient = openAIClient.GetChatClient("model-name");
```

**错误处理最佳实践**:

```csharp
try
{
    var completion = await chatClient.CompleteChatAsync(messages, options);
    // 处理响应...
}
catch (RequestFailedException ex)
{
    switch (ex.Status)
    {
        case 401: throw new UnauthorizedAccessException("Invalid API key");
        case 429: throw new RateLimitExceededException("Rate limit exceeded");
        case 404: throw new InvalidOperationException("Endpoint not found");
        default: throw new InvalidOperationException($"API error: {ex.Message}");
    }
}
```

### IAsyncEnumerable 最佳实践

**取消令牌处理**:

```csharp
public async IAsyncEnumerable<StreamChunk> CompleteStreamingAsync(
    IEnumerable<ChatMessage> messages,
    ChatOptions? options = null,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    await foreach (var update in _sdkClient.StreamAsync(..., cancellationToken))
    {
        cancellationToken.ThrowIfCancellationRequested();
        yield return ConvertToChunk(update);
    }
}
```

**错误传播**:

```csharp
public async IAsyncEnumerable<StreamChunk> StreamWithErrorHandling(...)
{
    IAsyncEnumerator<SdkUpdate> enumerator;
    try
    {
        enumerator = _sdkClient.StreamAsync(...).GetAsyncEnumerator(cancellationToken);
    }
    catch (Exception ex)
    {
        yield return new StreamChunk(Error: ex.Message);
        yield break;
    }
    
    while (await enumerator.MoveNextAsync())
    {
        yield return ConvertToChunk(enumerator.Current);
    }
}
```

### 依赖包更新

```xml
<!-- 推荐版本 -->
<PackageReference Include="Anthropic.SDK" Version="5.10.0" />
<PackageReference Include="OpenAI" Version="2.*" />
<PackageReference Include="DotNetEnv" Version="3.*" />
<PackageReference Include="System.Text.Json" Version="8.0.0" />

<!-- 可选：IChatClient 集成 -->
<PackageReference Include="Microsoft.Extensions.AI.Abstractions" Version="9.4.0-preview" />
```

## Future Considerations

- **Session 持久化**: 保存到 JSONL 文件
- **工具审批**: 执行前请求用户确认
- **速率限制**: 自动重试和退避
- **Token 计数**: 上下文窗口管理

## Sources & References

### Origin

- **Brainstorm document:** [docs/brainstorms/2026-03-20-agent-loop-brainstorm.md](../brainstorms/2026-03-20-agent-loop-brainstorm.md)
  - 关键决策：使用原生 SDK 而非 Microsoft.Extensions.AI
  - 架构：Provider 模式，IChatProvider 统一接口
  - 范围：循环 + 工具 + 深度思考

### Internal References

- 现有代码: `CodeAgentDemo/Tools/BashTool.cs`
- 入口点: `CodeAgentDemo/Program.cs`
- 项目配置: `CodeAgentDemo/CodeAgentDemo.csproj`

### External References

- claw0 s01_agent_loop.md: https://github.com/shareAI-lab/claw0/blob/main/sessions/zh/s01_agent_loop.md
- Anthropic SDK: https://github.com/tghamm/anthropic-sdk-dotnet
- OpenAI SDK: https://github.com/openai/openai-dotnet

### Open Questions from Brainstorm

1. ~~**思考内容显示**~~ - ✅ 已解决：SDK 支持 `ThinkingContent`，默认折叠显示
2. **会话持久化** - 后续考虑 (YAGNI)
3. **工具审批流程** - 后续版本添加
4. **速率限制** - 需要时添加

## Technical Review Corrections

### 已修复问题

| 问题 | 修正内容 |
|------|----------|
| **Agent Loop 逻辑缺陷** | 添加内层循环处理 tool_use，正确累积响应后重新调用 LLM |
| **StreamChunk 设计问题** | 分离 `StreamChunk`（delta）和 `StreamResponse`（完整响应） |
| **缺少工具结果消息格式** | 定义 `ToolResultBlock` 并在 ChatRole 中添加 Tool 角色 |
| **System Message 处理** | 添加 `ChatOptions.SystemPrompt`，Provider 负责转换格式 |
| **错误处理不完整** | 扩展错误传播表，覆盖 JSON 解析、流中断、超时等场景 |
| **配置验证缺失** | 添加 `ConfigurationValidator` 类 |

### 仍需注意

- **消息历史管理**: 当前无大小限制，后续需要 token 计数和裁剪
- **OpenAI SDK API**: 需在实现时验证 v2 具体 API 签名
- **取消策略**: 已传递 CancellationToken，但工具执行超时需要额外处理