# CodeAgent 知识库

**生成时间:** 2026-03-20 11:18 CST
**分支:** main

## 概述

C#/.NET 8 Agent 循环系统，参考 claw0 设计模式。使用 Microsoft.Extensions.AI 作为 LLM 提供商抽象层，支持 Anthropic 和 OpenAI 兼容端点切换。

**核心行为：** 消息累积 → LLM 调用(流式) → `stop_reason` 分支 → 工具执行/输出

## 结构

```
CodeAgent/
├── CodeAgent.sln
└── CodeAgentDemo/
    ├── Program.cs           # 入口 - Agent 循环
    ├── Tools/BashTool.cs    # Bash 命令执行工具
    ├── Core/                # 计划中: AgentLoop, SessionStore, ToolRegistry
    ├── Managers/            # 计划中
    └── Providers/           # 计划中: ChatClientFactory
```

## 查找指南

| 任务 | 位置 | 说明 |
|------|------|------|
| 入口点 | `CodeAgentDemo/Program.cs` | 主循环，用户交互 |
| 工具实现 | `CodeAgentDemo/Tools/` | ITool 接口及实现 |
| 架构设计 | `docs/brainstorms/` | 设计决策和规划 |
| 项目配置 | `CodeAgentDemo/CodeAgentDemo.csproj` | 依赖和框架设置 |

## 代码映射

| 符号 | 类型 | 位置 | 职责 |
|------|------|------|------|
| `BashTool` | class | Tools/BashTool.cs | 执行 shell 命令，5分钟超时 |
| `AgentLoop` | class | 计划中 | stop_reason 分支处理 |
| `ITool` | interface | 计划中 | 工具抽象，转换为 AIFunction |

## 约定

- **.NET 8.0**: `implicitUsings` + `nullable` 启用
- **异步优先**: 所有工具和 API 调用使用 async/await
- **环境变量配置**: 通过 .env 文件管理 API 密钥

## 配置

```env
AI_PROVIDER=Anthropic          # 或 OpenAI
ANTHROPIC_API_KEY=sk-ant-xxx
ANTHROPIC_MODEL=claude-sonnet-4-20250514
OPENAI_API_KEY=sk-xxx
OPENAI_MODEL=gpt-4
```

## 反模式 (本项目)

- **无输入验证**: BashTool 直接执行用户输入的 shell 命令
- **无速率限制**: API 调用无退避/重试机制
- **无用户确认**: 工具执行前无需用户批准
- **无会话持久化**: 重启后会话丢失

## 命令

```bash
dotnet build                  # 构建
dotnet run --project CodeAgentDemo  # 运行
dotnet test                   # 测试 (未配置)
```

## 注意事项

1. **项目状态**: 早期开发阶段，Core/Managers/Providers 目录为空
2. **依赖预览版**: Microsoft.Extensions.AI 使用 preview 版本
3. **跨平台**: BashTool 硬编码 `/bin/bash`，Windows 需调整