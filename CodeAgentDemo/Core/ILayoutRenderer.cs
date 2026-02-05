namespace CodeAgentDemo.Core;

/// <summary>
/// 布局渲染器接口
/// </summary>
public interface ILayoutRenderer
{
    /// <summary>
    /// 渲染输出内容
    /// </summary>
    /// <param name="content">输出内容</param>
    void RenderOutput(string content);

    /// <summary>
    /// 渲染输入提示
    /// </summary>
    /// <param name="prompt">提示文本</param>
    /// <param name="input">输入处理器</param>
    void RenderInput(string prompt, IInputHandler input);

    /// <summary>
    /// 锁定输入
    /// </summary>
    void LockInput();

    /// <summary>
    /// 解锁输入
    /// </summary>
    void UnlockInput();
}