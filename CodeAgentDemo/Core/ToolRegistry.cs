using CodeAgentDemo.Interfaces;
using CodeAgentDemo.Tools;

namespace CodeAgentDemo.Core;

/// <summary>
/// Registry for managing available tools.
/// </summary>
public class ToolRegistry : IToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new();

    /// <summary>
    /// Registers a tool.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when a tool with the same name already exists.</exception>
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
    /// Gets a tool by name.
    /// </summary>
    /// <returns>The tool if found, null otherwise.</returns>
    public ITool? GetTool(string name)
    {
        return _tools.TryGetValue(name, out var tool) ? tool : null;
    }

    /// <summary>
    /// Gets all registered tools.
    /// </summary>
    public IEnumerable<ITool> GetAllTools()
    {
        return _tools.Values.ToList();
    }

    /// <summary>
    /// Gets the number of registered tools.
    /// </summary>
    public int Count => _tools.Count;

    /// <summary>
    /// Checks if a tool with the given name is registered.
    /// </summary>
    public bool HasTool(string name)
    {
        return _tools.ContainsKey(name);
    }
}
