using System.Security.Cryptography;
using System.Text;
using CodeAgentDemo.Models;

namespace CodeAgentDemo.Core;

/// <summary>
/// 循环控制器的实现类，管理迭代限制和循环检测。
/// </summary>
public class LoopController : ILoopController
{
    private readonly LoopControlConfig _config;
    private readonly LoopState _state;
    private readonly List<string> _recentStateHashes;

    /// <inheritdoc />
    public LoopState State => _state;

    /// <summary>
    /// 初始化 LoopController 类的新实例。
    /// </summary>
    /// <param name="config">循环控制配置。</param>
    /// <param name="maxTokens">最大 token 限制（可选）。</param>
    public LoopController(LoopControlConfig config, int maxTokens = 0)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _state = new LoopState { MaxTokens = maxTokens };
        _recentStateHashes = new List<string>();
    }

    /// <inheritdoc />
    public Task<bool> CheckIterationAsync(CancellationToken cancellationToken = default)
    {
        // 检查迭代限制
        if (_state.IterationCount >= _config.MaxIterations)
        {
            _state.IterationLimitReached = true;
            return Task.FromResult(false);
        }

        // 检查 token 限制
        double tokenRatio = _state.MaxTokens > 0
            ? (double)_state.TokenUsage / _state.MaxTokens
            : 0;

        if (tokenRatio >= _config.TokenLimitRatio)
        {
            _state.TokenLimitReached = true;
            return Task.FromResult(false);
        }

        // 检查循环
        if (_state.DetectedCycle)
        {
            return Task.FromResult(false);
        }

        // 增加迭代计数
        _state.IterationCount++;

        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public bool DetectCycle(string stateHash)
    {
        ArgumentNullException.ThrowIfNull(stateHash);

        // 检查此哈希是否已存在于最近状态中
        if (_recentStateHashes.Contains(stateHash))
        {
            _state.DetectedCycle = true;
            return true;
        }

        // 添加到最近状态
        _recentStateHashes.Add(stateHash);

        // 维护窗口大小
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

        // 使用 SHA256 进行一致性哈希
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