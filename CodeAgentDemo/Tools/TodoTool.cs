using System.Text.Json;
using CodeAgentDemo.Core;
using CodeAgentDemo.Models;

namespace CodeAgentDemo.Tools;

/// <summary>
/// Tool for managing todo items.
/// </summary>
public class TodoTool(
    TodoManager todoManager) : ITool
{
    private readonly TodoManager _todoManager = todoManager ?? throw new ArgumentNullException(nameof(todoManager));

    public string Name => "todo";

    public string Description =>
        "任务管理工具，用于创建、更新和跟踪待办事项。\n\n" +
        "功能：\n" +
        "- 创建新的待办事项\n" +
        "- 更新待办事项状态（pending/in_progress/completed）\n" +
        "- 设置待办事项优先级（high/medium/low）\n" +
        "- 查看当前所有待办事项\n\n" +
        "注意：同时只能有一个 in_progress 状态的任务。\n" +
        "建议在处理多步任务时使用此工具来跟踪进度。";

    public JsonElement InputSchema => JsonDocument.Parse("""
    {
        "type": "object",
        "properties": {
            "action": {
                "type": "string",
                "description": "操作类型：list（查看）、add（添加）、update（更新）、remove（删除）、clear（清空）",
                "enum": ["list", "add", "update", "remove", "clear"]
            },
            "items": {
                "type": "array",
                "description": "待办事项列表，用于批量更新",
                "items": {
                    "type": "object",
                    "properties": {
                        "id": {
                            "type": "string",
                            "description": "待办事项唯一标识"
                        },
                        "text": {
                            "type": "string",
                            "description": "待办事项描述"
                        },
                        "status": {
                            "type": "string",
                            "description": "状态：pending、in_progress、completed",
                            "enum": ["pending", "in_progress", "completed"]
                        },
                        "priority": {
                            "type": "string",
                            "description": "优先级：high、medium、low",
                            "enum": ["high", "medium", "low"]
                        }
                    },
                    "required": ["id", "text"]
                }
            },
            "id": {
                "type": "string",
                "description": "待办事项ID（用于单个更新/删除）"
            },
            "text": {
                "type": "string",
                "description": "待办事项描述（用于添加或更新）"
            },
            "status": {
                "type": "string",
                "description": "状态：pending、in_progress、completed",
                "enum": ["pending", "in_progress", "completed"]
            },
            "priority": {
                "type": "string",
                "description": "优先级：high、medium、low",
                "enum": ["high", "medium", "low"]
            }
        },
        "required": ["action"]
    }
    """).RootElement;

    public bool RequiresConfirmation(JsonElement arguments) => false;

    public Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken = default)
    {
        var action = arguments.TryGetProperty("action", out var actionProp)
            ? actionProp.GetString() ?? "list"
            : "list";

        try
        {
            return action switch
            {
                "list" => HandleList(),
                "add" => HandleAdd(arguments),
                "update" => HandleUpdate(arguments),
                "remove" => HandleRemove(arguments),
                "clear" => HandleClear(),
                _ => Task.FromResult(new ToolResult(false, $"Unknown action: {action}"))
            };
        }
        catch (ArgumentException ex)
        {
            return Task.FromResult(new ToolResult(false, ex.Message));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ToolResult(false, $"Error: {ex.Message}"));
        }
    }

    private Task<ToolResult> HandleList()
    {
        var rendered = _todoManager.Render();
        return Task.FromResult(new ToolResult(true, rendered));
    }

    private Task<ToolResult> HandleAdd(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("text", out var textProp))
        {
            return Task.FromResult(new ToolResult(false, "text is required for add action"));
        }

        var text = textProp.GetString() ?? "";
        var priority = arguments.TryGetProperty("priority", out var pProp)
            ? pProp.GetString() ?? "medium"
            : "medium";

        var id = Guid.NewGuid().ToString("N")[..8];
        var newItem = new TodoItem
        {
            Id = id,
            Text = text,
            Status = TodoStatus.Pending,
            Priority = priority
        };

        var currentItems = _todoManager.Items.ToList();
        currentItems.Add(newItem);

        var result = _todoManager.Update(currentItems);
        return Task.FromResult(new ToolResult(true, $"已添加: {text}\n\n{result}"));
    }

    private Task<ToolResult> HandleUpdate(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("id", out var idProp))
        {
            return Task.FromResult(new ToolResult(false, "id is required for update action"));
        }

        var id = idProp.GetString();
        if (string.IsNullOrEmpty(id))
        {
            return Task.FromResult(new ToolResult(false, "id cannot be empty"));
        }

        var currentItems = _todoManager.Items.ToList();
        var item = currentItems.FirstOrDefault(i => i.Id == id);

        if (item == null)
        {
            return Task.FromResult(new ToolResult(false, $"Todo item not found: {id}"));
        }

        if (arguments.TryGetProperty("text", out var textProp))
        {
            var text = textProp.GetString();
            if (!string.IsNullOrEmpty(text))
            {
                item.Text = text;
            }
        }

        if (arguments.TryGetProperty("status", out var statusProp))
        {
            var statusStr = statusProp.GetString();
            item.Status = statusStr switch
            {
                "pending" => TodoStatus.Pending,
                "in_progress" => TodoStatus.InProgress,
                "completed" => TodoStatus.Completed,
                _ => item.Status
            };
        }

        if (arguments.TryGetProperty("priority", out var pProp))
        {
            var priority = pProp.GetString();
            if (!string.IsNullOrEmpty(priority))
            {
                item.Priority = priority;
            }
        }

        var result = _todoManager.Update(currentItems);
        return Task.FromResult(new ToolResult(true, result));
    }

    private Task<ToolResult> HandleRemove(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("id", out var idProp))
        {
            return Task.FromResult(new ToolResult(false, "id is required for remove action"));
        }

        var id = idProp.GetString();
        if (string.IsNullOrEmpty(id))
        {
            return Task.FromResult(new ToolResult(false, "id cannot be empty"));
        }

        var currentItems = _todoManager.Items.ToList();
        var removed = currentItems.RemoveAll(i => i.Id == id);

        if (removed == 0)
        {
            return Task.FromResult(new ToolResult(false, $"Todo item not found: {id}"));
        }

        var result = _todoManager.Update(currentItems);
        return Task.FromResult(new ToolResult(true, $"已删除: {id}\n\n{result}"));
    }

    private Task<ToolResult> HandleClear()
    {
        var result = _todoManager.Update(Array.Empty<TodoItem>());
        return Task.FromResult(new ToolResult(true, "已清空所有待办事项"));
    }
}