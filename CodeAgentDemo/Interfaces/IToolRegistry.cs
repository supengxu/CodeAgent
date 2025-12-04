using CodeAgentDemo.Tools;

namespace CodeAgentDemo.Interfaces;

/// <summary>
/// 工具注册表接口
/// </summary>
public interface IToolRegistry
{
    /// <summary>
    /// 注册工具
    /// </summary>
    void Register(ITool tool);

    /// <summary>
    /// 根据名称获取工具
    /// </summary>
    ITool? GetTool(string name);

    /// <summary>
    /// 获取所有已注册的工具
    /// </summary>
    IEnumerable<ITool> GetAllTools();

    /// <summary>
    /// 已注册工具数量
    /// </summary>
    int Count { get; }

    /// <summary>
    /// 检查指定名称的工具是否已注册
    /// </summary>
    bool HasTool(string name);
}