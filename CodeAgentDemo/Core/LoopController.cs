using System.Security.Cryptography;
using System.Text;
using CodeAgentDemo.Models;

namespace CodeAgentDemo.Core;

/// <summary>
/// Implementation of loop controller that manages iteration limits and cycle detection.
/// </summary>
public class LoopController : ILoopController
{
    private readonly LoopControlConfig _config;
    private readonly LoopState _state;
    private readonly List<string> _recentStateHashes;

    /// <inheritdoc />
    public LoopState State => _state;

    /// <summary>
    /// Initializes a new instance of the LoopController class.
    /// </summary>
    /// <param name="config">Loop control configuration.</param>
    /// <param name="maxTokens">Maximum token limit (optional).</param>
    public LoopController(LoopControlConfig config, int maxTokens = 0)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _state = new LoopState { MaxTokens = maxTokens };
        _recentStateHashes = new List<string>();
    }

    /// <inheritdoc />
    public Task<bool> CheckIterationAsync(CancellationToken cancellationToken = default)
    {
        // Check iteration limit
        if (_state.IterationCount >= _config.MaxIterations)
        {
            _state.IterationLimitReached = true;
            return Task.FromResult(false);
        }

        // Check token limit
        double tokenRatio = _state.MaxTokens > 0
            ? (double)_state.TokenUsage / _state.MaxTokens
            : 0;

        if (tokenRatio >= _config.TokenLimitRatio)
        {
            _state.TokenLimitReached = true;
            return Task.FromResult(false);
        }

        // Check for cycle
        if (_state.DetectedCycle)
        {
            return Task.FromResult(false);
        }

        // Increment iteration count
        _state.IterationCount++;

        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public bool DetectCycle(string stateHash)
    {
        ArgumentNullException.ThrowIfNull(stateHash);

        // Check if this hash already exists in recent states
        if (_recentStateHashes.Contains(stateHash))
        {
            _state.DetectedCycle = true;
            return true;
        }

        // Add to recent states
        _recentStateHashes.Add(stateHash);

        // Maintain window size
        while (_recentStateHashes.Count > _config.CycleDetectionWindow)
        {
            _recentStateHashes.RemoveAt(0);
        }

        return false;
    }

    /// <inheritdoc />
    public string GetStateHash(string stateData)
    {
        ArgumentNullException.ThrowIfNull(stateData);

        // Use SHA256 for consistent hashing
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(stateData));
        return Convert.ToHexString(bytes);
    }

    /// <inheritdoc />
    public void UpdateTokenUsage(int tokenUsage)
    {
        _state.TokenUsage = tokenUsage;
    }

    /// <inheritdoc />
    public void Reset()
    {
        _state.IterationCount = 0;
        _state.TokenUsage = 0;
        _state.DetectedCycle = false;
        _state.IterationLimitReached = false;
        _state.TokenLimitReached = false;
        _recentStateHashes.Clear();
    }
}