# CodeAgent

基于大语言模型的 CLI 编程助手，使用 .NET 8.0 构建。

## 功能特性

- **交互式 CLI** - 基于 Spectre.Console 的丰富终端界面
- **工具系统** - 文件操作、代码搜索、网页搜索、Shell 命令执行
- **会话持久化** - 保存和恢复对话历史
- **流式响应** - 实时展示 AI 响应
- **规划与反思** - 自主任务规划和自纠正能力
- **多 LLM 提供商** - 支持 OpenAI 和 Anthropic 兼容 API

## 项目架构

```
CodeAgentDemo/
├── Core/           # Agent 循环、会话管理、工具注册中心
├── Tools/          # 工具实现（Read、Write、Edit、Grep 等）
├── Providers/      # LLM 提供商抽象层（OpenAI、Anthropic）
├── Models/         # 数据模型（ChatMessage、StreamResponse 等）
├── Services/       # 工具类（Token 计数、JSON 转换）
└── Cli/            # CLI 交互层
```

## 环境要求

- .NET 8.0 SDK
- OpenAI API 密钥（或兼容的 API 提供商）

## 配置说明

在项目根目录创建 `.env` 文件：

```env
AI_PROVIDER=OpenAI
OPENAI_API_KEY=your-api-key-here
OPENAI_MODEL=gpt-4
ENABLE_THINKING=true
```

### 环境变量

| 变量 | 说明 | 默认值 |
|------|------|--------|
| `AI_PROVIDER` | LLM 提供商（OpenAI/Anthropic） | OpenAI |
| `OPENAI_API_KEY` | OpenAI API 密钥 | - |
| `OPENAI_MODEL` | 使用的模型 | gpt-4 |
| `ANTHROPIC_API_KEY` | Anthropic API 密钥 | - |
| `ANTHROPIC_MODEL` | Anthropic 模型 | claude-sonnet-4-20250514 |
| `ENABLE_THINKING` | 启用深度思考模式 | false |

## 构建项目

```bash
cd CodeAgentDemo
dotnet build
```

## 运行项目

```bash
dotnet run
```

## 可用工具

| 工具 | 说明 |
|------|------|
| `read` | 读取文件内容 |
| `write` | 创建或覆盖文件 |
| `edit` | 修改现有文件 |
| `glob` | 按模式查找文件 |
| `grep` | 搜索文件内容 |
| `bash` | 执行 Shell 命令 |
| `web_search` | 网页搜索 |
| `code_search` | GitHub 代码搜索 |

## 开发指南

### 运行测试

```bash
dotnet test
```

### 代码质量

项目强制要求：
- 可空引用类型
- 警告视为错误
- 公共 API 必须包含 XML 文档注释

## 许可证

MIT