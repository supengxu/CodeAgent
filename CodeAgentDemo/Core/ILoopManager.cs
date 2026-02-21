using CodeAgentDemo.Models;

namespace CodeAgentDemo.Core;

/// <summary>
/// Manages the agent loop iteration control including limits and cycle detection.
/// </summary>
public interface ILoopManager
{
    /// <summary>
    /// Checks if the loop can continue to the next iteration.
    /// </summary>
    Task<bool> CanContinueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Detects if a cycle exists in the given state hash.
    /// </summary>
    bool DetectCycle(string stateHash);

    /// <summary>
    /// Gets the state hash for the given state data.
    /// </summary>
    string GetStateHash(string stateData);

    /// <summary>
    /// Updates the token usage counter.
    /// </summary>
    void UpdateTokenUsage(int totalTokens);

    /// <summary>
    /// Resets the loop state for a new iteration cycle.
    /// </summary>
    void Reset();

    /// <summary>
    /// Gets the current loop state.
    /// </summary>
    LoopState State { get; }

    /// <summary>
    /// Determines the stop reason based on current state.
    /// </summary>
    string DetermineStopReason();
}