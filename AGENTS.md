# CodeAgent 项目 Agent 执行规则

**强制使用中文进行交流，代码可用英文**

---

## 1. 架构硬约束（违反直接编译失败）

### 1.1 严格分层架构
```
CodeAgentDemo/
├── Program.cs           # 仅入口点，禁止业务逻辑
├── Core/                # 核心层：AgentLoop, SessionStore, ToolRegistry
├── Tools/               # 工具层：ITool 接口及实现
├── Providers/           # 提供商层：LLM 抽象和实现
├── Models/              # 数据模型：ChatMessage, ContentBlock 等
├── Services/            # 服务层：Token 计数、JSON 转换等
└── Cli/                 # CLI 层：命令行交互
```

**分层调用规则：**
- `Program.cs` → 只能依赖 `Core/` 和 `Cli/`
- `Core/` → 可以依赖 `Tools/`、`Providers/`、`Models/`
- `Tools/` → 只能依赖 `Models/`，禁止依赖 `Core/`
- `Providers/` → 只能依赖 `Models/`
- `Services/` → 无外部依赖，纯工具类

**禁止：**
- 跨层调用（如 Tool 直接调用 Provider）
- 循环依赖
- 在 `Program.cs` 中写业务逻辑

### 1.2 目录结构约定
- 新增文件必须在对应目录下创建
- 禁止私自创建新的顶层目录
- 测试文件放在 `CodeAgentDemo.Tests/` 对应目录

---

## 2. 代码规范硬规则

### 2.1 必须遵守
- **强制 Nullable**: 所有引用类型必须标注可空性
- **警告即错误**: `TreatWarningsAsErrors=true`，零警告才能编译通过
- **异步命名**: 异步方法必须以 `Async` 结尾
- **依赖注入**: 禁止 `new` 关键字创建服务实例，必须通过 DI

### 2.2 工具开发规范 (ITool 实现)
```csharp
// ✅ 正确：实现 ITool 接口
public class XxxTool : ITool
{
    public string Name => "xxx";
    public string Description => "简短描述（中文）";
    public JsonElement InputSchema => ...;
    
    public bool RequiresConfirmation(JsonElement arguments) => ...;
    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken ct) => ...;
}
```

**必须：**
- `Name` 使用小写下划线命名（如 `read_file`）
- `Description` 使用中文，说明用途和注意事项
- `InputSchema` 必须定义 `required` 字段
- `ExecuteAsync` 必须处理 `CancellationToken`
- 返回的 `ToolResult.Success` 必须准确反映执行状态

### 2.3 日志规范
- 禁止使用 `Console.WriteLine` 进行日志输出
- 必须使用 `ConsoleUI` 或 `ILogger<T>` 
- 错误信息使用 `ConsoleUI.PrintError()`

### 2.4 异常处理
- 工具执行异常必须捕获并返回 `ToolResult.Failed`
- 禁止吞掉异常（空 catch 块）
- 网络请求必须有超时处理

---

## 3. 完成标准 (Definition of Done)

### 3.1 每个功能必须包含
- [ ] 实现代码
- [ ] xUnit 单元测试（核心路径 100% 覆盖）
- [ ] XML 文档注释（public 方法/类）

### 3.2 校验要求
- 所有修改必须通过 `.\.harness\Validate.ps1` 全量校验
- 单元测试覆盖率 ≥ 80%
- 禁止提交未通过校验的代码

---

## 4. 执行要求

1. **每次编写/修改代码后**，必须运行校验脚本：
   ```powershell
   .\.harness\Validate.ps1
   ```

2. **校验不通过时**，必须根据错误信息自行修复，直到全量通过

3. **新增工具时**，必须同步更新：
   - `ToolRegistry` 注册
   - `Program.cs` 注册调用
   - 单元测试文件

4. **所有规则以本文件和仓库配置为准**，禁止自行放宽规则

---

## 5. 常见错误自动修复

| 错误类型 | 修复方法 |
|----------|----------|
| CS8600/CS8601/CS8602 (Nullable) | 添加 `?` 或 `!` 断言，或做空检查 |
| CS1998 (异步方法无 await) | 移除 `async` 关键字，返回 `Task.FromResult()` 或 `Task.CompletedTask` |
| CA1062 (参数空检查) | 在方法开头添加 `ArgumentNullException.ThrowIfNull(param);` |
| CA1031 (异常捕获过宽) | 捕获具体异常类型，或将 `catch (Exception)` 改为多个具体 catch |
| CA1822 (static 成员) | 给方法添加 `static` 关键字 |
| IDE0001/IDE0002 (命名简化) | 运行 `dotnet format` |
| 测试覆盖率不足 | 补充边界场景测试用例 |

### 错误修复示例

```csharp
// ❌ CS1998: 异步方法无 await
public async Task<string> GetNameAsync() => _name;

// ✅ 修复：移除 async，直接返回
public Task<string> GetNameAsync() => Task.FromResult(_name);

// ❌ CA1062: 参数未做空检查
public void Process(string data) {
    Console.WriteLine(data.Length);
}

// ✅ 修复：添加参数验证
public void Process(string data) {
    ArgumentNullException.ThrowIfNull(data);
    Console.WriteLine(data.Length);
}

// ❌ CA1822: 方法可标记为 static
public int Add(int a, int b) => a + b;

// ✅ 修复：添加 static
public static int Add(int a, int b) => a + b;
```

---

## 6. 环境配置

```env
# .env 文件（不提交到 Git）
AI_PROVIDER=Anthropic          # 或 OpenAI
ANTHROPIC_API_KEY=sk-ant-xxx
ANTHROPIC_MODEL=claude-sonnet-4-20250514
OPENAI_API_KEY=sk-xxx
OPENAI_MODEL=gpt-4
ENABLE_THINKING=true           # 启用思考模式
```

---

## 7. 项目特定规则

### 7.1 BashTool 安全
- 必须检查危险命令模式
- 必须有超时限制（默认 2 分钟，最大 10 分钟）
- 敏感操作必须要求用户确认

### 7.2 流式响应处理
- 必须正确处理 `StopReason` 分支
- 工具调用必须累积完整参数后再执行
- 必须处理取消令牌

### 7.3 会话管理
- 会话必须支持持久化
- 消息历史必须有序且可追溯
- 退出时必须保存会话状态

---

## 8. 跨平台兼容性（Mac/Windows）

本项目必须在 macOS 和 Windows 上都能正常运行。

### 8.1 路径处理

**必须使用** `Path.Combine()`、`Path.GetFullPath()` 等跨平台 API，禁止硬编码路径分隔符。

### 8.2 文件名比较

文件名比较必须忽略大小写：`StringComparison.OrdinalIgnoreCase`

### 8.3 BashTool 说明

- Mac/Linux：默认使用 `/bin/bash`
- Windows：需安装 Git Bash 或 WSL
