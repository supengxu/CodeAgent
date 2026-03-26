using System.Text;
using System.Text.Json;
using CodeAgentDemo.Models;

namespace CodeAgentDemo.Core;

/// <summary>
/// 管理待办事项及其状态跟踪和验证。
/// </summary>
public class TodoManager
{
    private readonly List<TodoItem> _items = new();
    private int _roundsSinceLastUpdate;

    /// <summary>
    /// Gets all todo items.
    /// </summary>
    public IReadOnlyList<TodoItem> Items => _items.AsReadOnly();

    /// <summary>
    /// Gets the number of rounds since the last todo update.
    /// </summary>
    public int RoundsSinceLastUpdate => _roundsSinceLastUpdate;

    /// <summary>
    /// Increments the round counter when todo is not updated.
    /// </summary>
    public void IncrementRound()
    {
        _roundsSinceLastUpdate++;
    }

    /// <summary>
    /// 使用新项更新待办列表。
    /// </summary>
    /// <param name="items">要更新的待办项列表。</param>
    /// <returns>渲染的待办列表字符串。</returns>
    /// <exception cref="ArgumentException">当多个项具有 in_progress 状态时抛出。</exception>
    public string Update(IEnumerable<TodoItem> items)
    {
        var itemList = items.ToList();
        var validated = new List<TodoItem>();
        var inProgressCount = 0;

        foreach (var item in itemList)
        {
            if (item.Status == TodoStatus.InProgress)
            {
                inProgressCount++;
            }

            validated.Add(new TodoItem
            {
                Id = item.Id,
                Text = item.Text,
                Status = item.Status,
                Priority = item.Priority,
                CreatedAt = item.CreatedAt,
                UpdatedAt = DateTime.Now
            });
        }

        if (inProgressCount > 1)
        {
            throw new ArgumentException("Only one task can be in_progress at a time.");
        }

        _items.Clear();
        _items.AddRange(validated);
        _roundsSinceLastUpdate = 0;

        return Render();
    }

    /// <summary>
    /// 重置轮次计数器。
    /// </summary>
    public void ResetRound()
    {
        _roundsSinceLastUpdate = 0;
    }

    /// <summary>
    /// 检查是否应该注入提醒。
    /// </summary>
    /// <param name="roundsSinceLastUpdate">自上次更新以来的轮次数。</param>
    /// <param name="threshold">轮次阈值（默认 3）。</param>
    /// <returns>如果应该注入提醒返回 true。</returns>
    public bool ShouldNag(int roundsSinceLastUpdate, int threshold = 3)
    {
        return roundsSinceLastUpdate >= threshold && _items.Count > 0;
    }

    /// <summary>
    /// 将待办列表渲染为格式化的字符串。
    /// </summary>
    public string Render()
    {
        if (_items.Count == 0)
        {
            return "No todo items.";
        }

        var sb = new StringBuilder();
        sb.AppendLine("【待办事项】");

        foreach (var item in _items)
        {
            var statusIcon = item.Status switch
            {
                TodoStatus.Pending => "[ ]",
                TodoStatus.InProgress => "[>]",
                TodoStatus.Completed => "[x]",
                _ => "[ ]"
            };

            var priorityMarker = item.Priority?.ToLower() switch
            {
                "high" => "🔴",
                "low" => "🟢",
                _ => "🟡"
            };

            sb.AppendLine($"  {statusIcon} {priorityMarker} {item.Text}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// 获取当前的进行中项（如果有）。
    /// </summary>
    public TodoItem? CurrentInProgressItem =>
        _items.FirstOrDefault(i => i.Status == TodoStatus.InProgress);

    /// <summary>
    /// 获取待办项的 JSON 表示形式用于工具输出。
    /// </summary>
    public string ToJson()
    {
        return JsonSerializer.Serialize(_items, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
}