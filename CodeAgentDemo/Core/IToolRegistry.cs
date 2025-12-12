using CodeAgentDemo.Tools;

namespace CodeAgentDemo.Core;

public interface IToolRegistry
{
    void Register(ITool tool);
    ITool? GetTool(string name);
    IEnumerable<ITool> GetAllTools();
    int Count { get; }
    bool HasTool(string name);
}