namespace CodeAgentDemo.Models;

/// <summary>
/// Represents the status of a todo item.
/// </summary>
public enum TodoStatus
{
    /// <summary>
    /// Task is pending and not yet started.
    /// </summary>
    Pending,

    /// <summary>
    /// Task is currently being worked on.
    /// </summary>
    InProgress,

    /// <summary>
    /// Task has been completed.
    /// </summary>
    Completed
}

/// <summary>
/// Represents a todo item with status tracking.
/// </summary>
public class TodoItem
{
    /// <summary>
    /// Gets or sets the unique identifier of the todo item.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets or sets the description of the todo item.
    /// </summary>
    public required string Text { get; set; }

    /// <summary>
    /// Gets or sets the current status of the todo item.
    /// </summary>
    public TodoStatus Status { get; set; } = TodoStatus.Pending;

    /// <summary>
    /// Gets or sets the priority of the todo item.
    /// </summary>
    public string Priority { get; set; } = "medium";

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.Now;

    /// <summary>
    /// Gets or sets the last update timestamp.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}