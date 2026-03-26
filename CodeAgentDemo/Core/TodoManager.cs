using System.Text;
using System.Text.Json;
using CodeAgentDemo.Models;

namespace CodeAgentDemo.Core;

/// <summary>
/// Manages todo items with state tracking and validation.
/// </summary>
public class TodoManager
{
    private readonly List<TodoItem> _items = new();
    private int _roundsSinceLastUpdate;

    /// <summary>
    /// Updates the todo list with new items.
    /// </summary>
    /// <param name="items">List of todo items to update.</param>
    /// <returns>Rendered todo list string.</returns>
    /// <exception cref="ArgumentException">Thrown when multiple items have in_progress status.</exception>
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
    /// Resets the round counter.
    /// </summary>
    public void ResetRound()
    {
        _roundsSinceLastUpdate = 0;
    }

    /// <summary>
    /// Checks if nag reminder should be injected.
    /// </summary>
    /// <param name="roundsSinceLastUpdate">Number of rounds since todo was last updated.</param>
    /// <param name="threshold">Number of rounds threshold (default 3).</param>
    /// <returns>True if reminder should be injected.</returns>
    public bool ShouldNag(int roundsSinceLastUpdate, int threshold = 3)
    {
        return roundsSinceLastUpdate >= threshold && _items.Count > 0;
    }

    /// <summary>
    /// Renders the todo list as a formatted string.
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
    /// Gets the current in-progress item if any.
    /// </summary>
    public TodoItem? CurrentInProgressItem =>
        _items.FirstOrDefault(i => i.Status == TodoStatus.InProgress);

    /// <summary>
    /// Gets JSON representation of todos for tool output.
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