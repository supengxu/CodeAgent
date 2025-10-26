namespace CodeAgentDemo.Models;

/// <summary>
/// Represents a task execution plan with ordered steps.
/// </summary>
public class TaskPlan
{
    /// <summary>
    /// Gets or sets the ordered list of steps to execute.
    /// </summary>
    public List<string> Steps { get; set; } = new();

    /// <summary>
    /// Gets or sets the index of the current step being executed.
    /// </summary>
    public int CurrentStep { get; set; }

    /// <summary>
    /// Gets a value indicating whether all steps have been completed.
    /// </summary>
    public bool IsComplete => CurrentStep >= Steps.Count;

    /// <summary>
    /// Gets or sets the original task description.
    /// </summary>
    public string? OriginalTask { get; set; }

    /// <summary>
    /// Gets or sets the complexity assessment result.
    /// </summary>
    public ComplexityLevel Complexity { get; set; }

    /// <summary>
    /// Advances to the next step in the plan.
    /// </summary>
    public void AdvanceStep()
    {
        if (!IsComplete)
        {
            CurrentStep++;
        }
    }

    /// <summary>
    /// Gets the current step description, or null if complete.
    /// </summary>
    public string? GetCurrentStepDescription()
    {
        return IsComplete ? null : Steps[CurrentStep];
    }
}

/// <summary>
/// Represents the complexity level of a task.
/// </summary>
public enum ComplexityLevel
{
    /// <summary>
    /// Simple task that can be executed directly without planning.
    /// </summary>
    Simple,

    /// <summary>
    /// Complex task that requires planning before execution.
    /// </summary>
    Complex
}