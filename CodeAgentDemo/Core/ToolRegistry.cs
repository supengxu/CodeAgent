using CodeAgentDemo.Tools;

namespace CodeAgentDemo.Core;

/// <summary>
/// 用于管理可用工具的注册表。
/// </summary>
public class ToolRegistry : IToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new();

    /// <summary>
    /// 注册工具。
    /// </summary>
    /// <exception cref="ArgumentException">当已存在同名工具时抛出。</exception>
    public void Register(ITool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        if (_tools.ContainsKey(tool.Name))
        {
            throw new ArgumentException($"Tool '{tool.Name}' is already registered.");
        }

        _tools[tool.Name] = tool;
    }

    /// <summary>
    /// 按名称获取工具。
    /// </summary>
    /// <returns>如果找到返回工具，否则返回 null。</returns>
    public ITool? GetTool(string name)
    {
        return _tools.TryGetValue(name, out var tool) ? tool : null;
    }

    /// <summary>
    /// 获取所有已注册的工具。
    /// </summary>
    public IEnumerable<ITool> GetAllTools()
    {
        return _tools.Values.ToList();
    }

    /// <summary>
    /// 获取已注册工具的数量。
    /// </summary>
    public int Count => _tools.Count;

    /// <summary>
    /// 检查是否已注册具有给定名称的工具。
    /// </summary>
    public bool HasTool(string name)
    {
        return _tools.ContainsKey(name);
    }
}
