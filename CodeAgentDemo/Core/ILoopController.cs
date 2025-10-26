using CodeAgentDemo.Models;

namespace CodeAgentDemo.Core;

/// <summary>
/// Interface for controlling agent loop iterations and detecting cycles.
/// </summary>
public interface ILoopController
{
    /// <summary>
    /// Gets the current loop state.
    /// </summary>
    LoopState State { get; }

    /// <summary>
    /// Checks if another iteration is allowed and updates the state.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if iteration is allowed, false if limits are reached.</returns>
    Task<bool> CheckIterationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Detects if the current state forms a cycle with recent states.
    /// </summary>
    /// <param name="stateHash">Hash of the current state.</param>
    /// <returns>True if a cycle is detected.</returns>
    bool DetectCycle(string stateHash);

    /// <summary>
    /// Generates a hash for the current state for cycle detection.
    /// </summary>
    /// <param name="stateData">State data to hash.</param>
    /// <returns>A hash string representing the state.</returns>
    string GetStateHash(string stateData);

    /// <summary>
    /// Updates token usage in the current state.
    /// </summary>
    /// <param name="tokenUsage">Current token usage.</param>
    void UpdateTokenUsage(int tokenUsage);

    /// <summary>
    /// Resets the loop controller state for a new session.
    /// </summary>
    void Reset();
}