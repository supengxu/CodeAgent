# CodeAgent Harness Engineering 优化方案

## 当前状态评估

### 项目现状
CodeAgent 是一个基于 .NET 8 的 AI Agent 循环系统，使用 Microsoft.Extensions.AI 作为 LLM 提供商抽象层。

### 现有 Harness 组件分析

| 组件 | 当前状态 | 问题 |
|------|----------|------|
| **工具编排** | 基本框架存在 (ToolRegistry) | 无重试机制、无熔断器、错误处理不完整 |
| **验证循环** | 仅工具确认对话框 | 无输出验证、无 schema 检查 |
| **成本控制** | MaxTokens 限制 | 无任务级预算、无 token 追踪 |
| **可观测性** | 基础控制台输出 | 无结构化日志、无执行追踪 |
| **上下文工程** | 直接传递完整历史 | 无上下文窗口管理、无智能裁剪 |

### 反模式清单
- ❌ BashTool 直接执行用户输入（虽然有基础危险命令过滤）
- ❌ 无速率限制/重试退避机制
- ❌ 工具执行失败无验证和恢复
- ❌ 无成本包络（可能导致 API 费用失控）
- ❌ 缺乏结构化可观测性数据

---

## 五大核心组件设计方案

### 1. 成本包络管理 (Cost Envelope Management)

#### 目标
防止任务费用失控，提供预算上限和早期预警。

#### 核心设计

```csharp
// Core/CostEnvelope.cs
public class CostEnvelope
{
    private readonly decimal _budgetCents;  // 预算上限（美元分）
    private readonly int _maxTokensPerCall;
    private readonly int _maxConsecutiveCalls;
    
    private decimal _spentCents;
    private int _consecutiveCalls;
    private DateTime _windowStart;
    
    public CostEnvelope(decimal budgetUsd = 2.0m, int maxTokens = 4096, int maxCalls = 50)
    {
        _budgetCents = budgetUsd * 100;
        _maxTokensPerCall = maxTokens;
        _maxConsecutiveCalls = maxCalls;
    }
    
    public bool CanProceed(TokenEstimate estimate)
    {
        if (_spentCents + estimate.CostCents > _budgetCents)
            return false;
        if (_consecutiveCalls >= _maxConsecutiveCalls)
            return false;
        return true;
    }
    
    public void RecordSpend(TokenUsage usage)
    {
        _spentCents += usage.EstimateCost();
        _consecutiveCalls++;
    }
}

// 成本追踪器
public interface ICostTracker
{
    void TrackToolCall(string toolName, TokenUsage usage);
    void TrackLLMCall(TokenUsage usage);
    CostReport GetCurrentSessionReport();
}
```

#### 实施建议
1. 默认任务预算: $2.00 (约 400K-800K tokens)
2. 预警阈值: 预算的 50%、80%、95%
3. 超出预算时: 优雅终止，返回部分结果和原因

---

### 2. 验证循环机制 (Verification Loops) - 最高 ROI

#### 目标
在继续下一步前验证每步输出，捕获工具失败和无效数据。

#### 核心设计

```csharp
// Core/Verification/IToolVerifier.cs
public interface IToolVerifier
{
    VerificationResult Verify(ToolCall call, ToolResult result);
}

// Schema 验证（低成本，50-150ms 延迟）
public class SchemaVerifier : IToolVerifier
{
    public VerificationResult Verify(ToolCall call, ToolResult result)
    {
        // 1. 检查输出非空
        if (string.IsNullOrWhiteSpace(result.Output))
            return VerificationResult.Failed("Empty output", canRetry: true);
        
        // 2. 检查错误标记
        if (!result.Success)
            return VerificationResult.Failed(result.Output, canRetry: false);
        
        // 3. 工具特定的 schema 检查
        var schemaValidation = ValidateToolSpecificSchema(call.Name, result.Output);
        if (!schemaValidation.IsValid)
            return VerificationResult.Failed(schemaValidation.Error, canRetry: true);
        
        return VerificationResult.Passed();
    }
}

// 语义验证（高成本，使用 LLM 二次检查）
public class SemanticVerifier : IToolVerifier
{
    private readonly IChatProvider _provider;
    
    public async Task<VerificationResult> VerifyAsync(ToolCall call, ToolResult result, string taskContext)
    {
        var prompt = $"""
            Task: {taskContext}
            Tool: {call.Name}
            Input: {call.Arguments}
            Output: {result.Output}
            
            Does this output make sense for the task? Answer YES/NO with brief reason.
            """;
        
        var response = await _provider.CompleteAsync(prompt);
        var passed = response.Contains("YES");
        
        return passed 
            ? VerificationResult.Passed()
            : VerificationResult.Failed(response, canRetry: true);
    }
}

// 带验证的工具执行
public class VerifiedToolExecutor
{
    private readonly ITool _tool;
    private readonly IEnumerable<IToolVerifier> _verifiers;
    private readonly IRetryPolicy _retryPolicy;
    
    public async Task<ToolResult> ExecuteWithVerificationAsync(
        ToolCall call, 
        CancellationToken ct)
    {
        var attempt = 0;
        while (attempt < _retryPolicy.MaxAttempts)
        {
            var result = await _tool.ExecuteAsync(call.Arguments, ct);
            
            // 顺序运行所有验证器
            foreach (var verifier in _verifiers)
            {
                var verification = verifier.Verify(call, result);
                if (!verification.Passed)
                {
                    if (verification.CanRetry && attempt < _retryPolicy.MaxAttempts - 1)
                    {
                        attempt++;
                        await Task.Delay(_retryPolicy.GetDelay(attempt));
                        break; // 重试
                    }
                    return ToolResult.Failed($"Verification failed: {verification.Reason}");
                }
            }
            
            return result; // 所有验证通过
        }
        
        return ToolResult.Failed("Max retry attempts exceeded");
    }
}
```

#### 实施优先级
1. **立即实施**: Schema 验证（所有工具输出非空、无错误）
2. **第二阶段**: 工具特定验证（如 ReadTool 检查文件存在性）
3. **第三阶段**: 语义验证（针对关键路径）

---

### 3. 可观测性系统 (Observability)

#### 目标
结构化执行追踪，支持调试和性能分析。

#### 核心设计

```csharp
// Core/Observability/ExecutionTrace.cs
public class ExecutionTrace
{
    public string TraceId { get; } = Guid.NewGuid().ToString();
    public DateTime StartedAt { get; } = DateTime.UtcNow;
    public List<ExecutionStep> Steps { get; } = new();
    
    public void AddStep(ExecutionStep step) => Steps.Add(step);
    
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions 
    { 
        WriteIndented = true 
    });
}

public class ExecutionStep
{
    public string StepType { get; set; } // "llm_call", "tool_call", "verification"
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object> Inputs { get; set; }
    public Dictionary<string, object> Outputs { get; set; }
    public TimeSpan Duration { get; set; }
    public TokenUsage TokenUsage { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}

// 追踪记录器
public interface IExecutionTracer
{
    ExecutionTrace CurrentTrace { get; }
    void StartTrace();
    void RecordLLMCall(string model, string[] messages, string response, TokenUsage usage);
    void RecordToolCall(string toolName, JsonElement arguments, ToolResult result, TimeSpan duration);
    void RecordVerification(ToolCall call, VerificationResult result);
    void EndTrace(string finalStatus);
    void SaveToFile(string path);
}

// 控制台 + 文件 双重输出
public class CompositeTracer : IExecutionTracer
{
    private readonly IExecutionTracer[] _tracers;
    
    public void RecordToolCall(string toolName, JsonElement args, ToolResult result, TimeSpan duration)
    {
        foreach (var tracer in _tracers) 
            tracer.RecordToolCall(toolName, args, result, duration);
    }
    // ... 其他方法
}
```

#### 输出示例
```json
{
  "traceId": "550e8400-e29b-41d4-a716-446655440000",
  "startedAt": "2026-03-21T10:30:00Z",
  "steps": [
    {
      "stepType": "llm_call",
      "timestamp": "2026-03-21T10:30:01Z",
      "duration": "00:00:02.5",
      "tokenUsage": { "input": 1200, "output": 150 },
      "success": true
    },
    {
      "stepType": "tool_call",
      "toolName": "read",
      "timestamp": "2026-03-21T10:30:04Z",
      "duration": "00:00:00.1",
      "success": true
    },
    {
      "stepType": "verification",
      "timestamp": "2026-03-21T10:30:04Z",
      "result": "passed"
    }
  ]
}
```

---

### 4. 工具编排优化 (Tool Orchestration)

#### 目标
可靠的工具调用、错误处理、超时管理和重试策略。

#### 核心设计

```csharp
// Core/ToolOrchestration/ToolOrchestrator.cs
public class ToolOrchestrator
{
    private readonly IToolRegistry _registry;
    private readonly IExecutionTracer _tracer;
    private readonly ICostTracker _costTracker;
    
    // 熔断器模式
    private readonly CircuitBreaker _circuitBreaker = new(
        failureThreshold: 5,
        recoveryTimeout: TimeSpan.FromSeconds(30)
    );
    
    public async Task<ToolResult> ExecuteAsync(ToolCall call, CancellationToken ct)
    {
        var tool = _registry.GetTool(call.Name);
        if (tool == null)
            return ToolResult.Failed($"Tool '{call.Name}' not found");
        
        // 检查熔断器
        if (_circuitBreaker.IsOpen)
            return ToolResult.Failed("Circuit breaker is open - too many recent failures");
        
        var startTime = DateTime.UtcNow;
        try
        {
            using var timeoutCts = new CancellationTokenSource(GetTimeoutForTool(call.Name));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            
            var result = await tool.ExecuteAsync(call.Arguments, linkedCts.Token);
            
            _circuitBreaker.RecordSuccess();
            _tracer.RecordToolCall(call.Name, call.Arguments, result, 
                DateTime.UtcNow - startTime);
            
            return result;
        }
        catch (OperationCanceledException)
        {
            _circuitBreaker.RecordFailure();
            return ToolResult.Failed("Tool execution timed out");
        }
        catch (Exception ex)
        {
            _circuitBreaker.RecordFailure();
            _tracer.RecordError("tool_execution", ex);
            return ToolResult.Failed($"Tool execution failed: {ex.Message}");
        }
    }
    
    private TimeSpan GetTimeoutForTool(string toolName) => toolName switch
    {
        "bash" => TimeSpan.FromMinutes(5),
        "web_search" => TimeSpan.FromSeconds(30),
        "read" => TimeSpan.FromSeconds(5),
        _ => TimeSpan.FromSeconds(30)
    };
}

// 重试策略
public interface IRetryPolicy
{
    int MaxAttempts { get; }
    TimeSpan GetDelay(int attemptNumber);
}

public class ExponentialBackoffRetry : IRetryPolicy
{
    public int MaxAttempts { get; } = 3;
    private readonly TimeSpan _baseDelay = TimeSpan.FromSeconds(1);
    private readonly TimeSpan _maxDelay = TimeSpan.FromSeconds(30);
    
    public TimeSpan GetDelay(int attempt)
    {
        var delay = _baseDelay * Math.Pow(2, attempt - 1);
        return TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds, _maxDelay.TotalMilliseconds));
    }
}
```

#### BashTool 安全增强
```csharp
// 在现有基础上增加
public class EnhancedBashTool : ITool
{
    private readonly BashTool _inner;
    private readonly ICommandAuditor _auditor;
    
    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct)
    {
        // 1. 审计日志
        _auditor.LogCommand(args.GetProperty("command").GetString());
        
        // 2. 沙箱执行（可选）
        if (RequiresSandbox(args))
        {
            return await ExecuteInSandboxAsync(args, ct);
        }
        
        // 3. 执行并验证
        var result = await _inner.ExecuteAsync(args, ct);
        
        // 4. 输出大小限制
        if (result.Output.Length > MaxOutputSize)
        {
            result = result with { 
                Output = result.Output[..MaxOutputSize] + $"\n[Output truncated: {result.Output.Length} chars]" 
            };
        }
        
        return result;
    }
}
```

---

### 5. 上下文工程 (Context Engineering)

#### 目标
精确控制 Agent 每步获得的信息，避免上下文窗口溢出。

#### 核心设计

```csharp
// Core/Context/ContextAssembler.cs
public class ContextAssembler
{
    private readonly int _maxContextTokens;
    private readonly ITokenCounter _tokenCounter;
    
    public ContextAssembler(int maxTokens = 8000, ITokenCounter? counter = null)
    {
        _maxContextTokens = maxTokens;
        _tokenCounter = counter ?? new SimpleTokenCounter();
    }
    
    public IEnumerable<ChatMessage> AssembleContext(
        IEnumerable<ChatMessage> history,
        string currentTask,
        ContextPriority priority)
    {
        var context = new List<ChatMessage>();
        var tokenBudget = _maxContextTokens;
        
        // 1. 系统提示（最高优先级）
        var systemMsg = history.FirstOrDefault(m => m.Role == ChatRole.System);
        if (systemMsg != null)
        {
            context.Add(systemMsg);
            tokenBudget -= _tokenCounter.Count(systemMsg.Content);
        }
        
        // 2. 当前任务
        var taskMsg = ChatMessage.CreateText(ChatRole.User, currentTask);
        var taskTokens = _tokenCounter.Count(taskMsg.Content);
        tokenBudget -= taskTokens;
        
        // 3. 相关历史消息（按优先级选择）
        var relevantHistory = SelectRelevantHistory(history, tokenBudget, priority);
        context.AddRange(relevantHistory);
        
        // 4. 添加当前任务
        context.Add(taskMsg);
        
        return context;
    }
    
    private IEnumerable<ChatMessage> SelectRelevantHistory(
        IEnumerable<ChatMessage> history, 
        int tokenBudget,
        ContextPriority priority)
    {
        // 最近 N 轮对话（时间衰减）
        var recentMessages = history
            .Where(m => m.Role != ChatRole.System)
            .Reverse() // 从最新开始
            .TakeWhile(m => {
                var tokens = _tokenCounter.Count(m.Content);
                if (tokenBudget >= tokens)
                {
                    tokenBudget -= tokens;
                    return true;
                }
                return false;
            })
            .Reverse(); // 恢复时间顺序
        
        return recentMessages;
    }
}

// 智能摘要（当历史太长时）
public class ConversationSummarizer
{
    private readonly IChatProvider _provider;
    
    public async Task<ChatMessage> SummarizeAsync(IEnumerable<ChatMessage> messages)
    {
        var summaryPrompt = """
            Summarize the following conversation, preserving:
            1. Key decisions made
            2. Files/tools mentioned
            3. Current task state
            
            Be concise (max 500 tokens).
            
            Conversation:
            {messages}
            """;
        
        var summary = await _provider.CompleteAsync(summaryPrompt);
        return ChatMessage.CreateText(ChatRole.System, $"[Summary] {summary}");
    }
}
```

---

## 实施路线图

### Phase 1: 基础 Harness（1-2 周）
- [ ] 成本包络管理（Token 追踪 + 预算限制）
- [ ] Schema 验证循环（工具输出验证）
- [ ] 基础可观测性（JSON 执行追踪）

### Phase 2: 可靠性增强（2-3 周）
- [ ] 工具编排（重试、熔断器、超时）
- [ ] BashTool 沙箱化
- [ ] 上下文窗口管理

### Phase 3: 生产级优化（3-4 周）
- [ ] 语义验证循环
- [ ] 智能上下文摘要
- [ ] 评估流水线（自动化测试集）

---

## 关键设计决策

### 1. 验证策略选择
```
Schema 验证: 必须实施（低成本，高价值）
├── 输出非空检查
├── 错误状态检查
└── 工具特定格式验证

语义验证: 针对关键工具
└── ReadTool: 验证返回的是有效代码/文本
└── BashTool: 验证命令执行符合预期
```

### 2. 成本计算模型
```csharp
// 基于模型的估算（实际费用按实际 API 返回计算）
public decimal EstimateCost(string model, int inputTokens, int outputTokens) => model switch
{
    "claude-sonnet-4" => (inputTokens * 0.0003m + outputTokens * 0.0015m) / 1000,
    "gpt-4" => (inputTokens * 0.0003m + outputTokens * 0.0006m) / 1000,
    _ => 0
};
```

### 3. 错误恢复策略
```
工具失败 → Schema 验证失败 → 可重试？
    ↓ 否              ↓ 是
  返回错误    执行退避重试
              ↓ 超过最大次数
            返回错误 + 部分上下文
```

---

## 预期效果

| 指标 | 当前 | 目标 |
|------|------|------|
| 工具调用失败率 | ~15% | <5% |
| 任务完成率 | ~75% | >95% |
| 调试时间 | 小时级 | 分钟级 |
| 成本失控风险 | 高 | 可控 |

---

## 参考实现

所有设计模式参考了 Harness Engineering 的核心原则：
- [Harness Engineering 完整指南](https://harness-engineering.ai/blog/what-is-harness-engineering/)
- OpenAI Codex 生产实践
- LangChain Terminal Bench 优化案例
