using CodeAgentDemo.Tools;

namespace CodeAgentDemo.Core;

public interface IToolFactory
{
    IEnumerable<ITool> CreateTools();
}