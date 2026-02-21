using CodeAgentDemo.Models;

namespace CodeAgentDemo.Core;

/// <summary>
/// Implementation of loop manager that delegates to LoopController and adds stop reason determination.
/// </summary>
public class LoopManager : ILoopManager
{
    private readonly ILoopController _loopController;
    private readonly IConsoleUI _ui;

    /// <inheritdoc />
    public LoopState State => _loopController.State;

    /// <summary>
    /// Initializes a new instance of the LoopManager class.
    /// </summary>
    /// <param name="loopController">The loop controller for iteration management.</param>
    /// <param name="ui">The console UI for warnings.</param>
    public LoopManager(ILoopController loopController, IConsoleUI ui)
    {
        _loopController = loopController ?? throw new ArgumentNullException(nameof(loopController));
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
    }

    /// <inheritdoc />
    public Task<bool> CanContinueAsync(CancellationToken cancellationToken = default)
    {
        return _loopController.CheckIterationAsync(cancellationToken);
    }

    /// <inheritdoc />
    public bool DetectCycle(string stateHash)
    {
        return _loopController.DetectCycle(stateHash);
    }

    /// <inheritdoc />
    public string GetStateHash(string stateData)
    {
        return _loopController.GetStateHash(stateData);
    }

    /// <inheritdoc />
    public void UpdateTokenUsage(int totalTokens)
    {
        _loopController.UpdateTokenUsage(totalTokens);
    }

    /// <inheritdoc />
    public void Reset()
    {
        _loopController.Reset();
    }

    /// <inheritdoc />
    public string DetermineStopReason()
    {
        var state = _loopController.State;

        if (state.IterationLimitReached)
        {
            _ui.PrintWarning("已达到最大迭代次数限制");
            return "iteration_limit";
        }

        if (state.TokenLimitReached)
        {
            _ui.PrintWarning("已达到 token 使用限制");
            return "token_limit";
        }

        if (state.DetectedCycle)
        {
            _ui.PrintWarning("检测到循环行为");
            return "cycle_detected";
        }

        return "loop_control_stop";
    }
}