using CodeAgentDemo.Models;

namespace CodeAgentDemo.Core;

/// <summary>
/// Interface for task planning engine that assesses task complexity and generates execution plans.
/// </summary>
public interface IPlanningEngine
{
    /// <summary>
    /// Assesses the complexity of a given task.
    /// </summary>
    /// <param name="task">The task description to assess.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The assessed complexity level.</returns>
    Task<ComplexityLevel> AssessComplexityAsync(string task, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates an execution plan for a complex task.
    /// </summary>
    /// <param name="task">The task description to plan.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task plan with ordered execution steps.</returns>
    Task<TaskPlan> GeneratePlanAsync(string task, CancellationToken cancellationToken = default);
}