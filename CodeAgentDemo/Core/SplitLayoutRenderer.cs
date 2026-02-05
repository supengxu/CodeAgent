using Spectre.Console;
using Spectre.Console.Rendering;

namespace CodeAgentDemo.Core;

/// <summary>
/// 分屏布局渲染器 - 输出区域可滚动，输入区域固定在底部
/// </summary>
public class SplitLayoutRenderer : ILayoutRenderer
{
    private readonly Layout _rootLayout;
    private readonly Layout _outputLayout;
    private readonly Layout _inputLayout;
    private readonly object _lockObject = new();
    private bool _isInputLocked;
    private string _currentPrompt = string.Empty;
    private IInputHandler? _currentInputHandler;

    /// <summary>
    /// 初始化分屏布局渲染器
    /// </summary>
    public SplitLayoutRenderer()
    {
        // 创建根布局
        _rootLayout = new Layout("Root")
            .SplitRows(
                new Layout("Output"),
                new Layout("Input").Size(3)
            );

        _outputLayout = _rootLayout["Output"];
        _inputLayout = _rootLayout["Input"];

        // 初始化输出区域
        _outputLayout.Update(new Panel(string.Empty)
            .Expand()
            .Border(BoxBorder.None));

        // 初始化输入区域
        UpdateInputArea();
    }

    /// <summary>
    /// 渲染输出内容到输出区域
    /// </summary>
    /// <param name="content">输出内容</param>
    public void RenderOutput(string content)
    {
        lock (_lockObject)
        {
            var panel = new Panel(new Text(content))
                .Expand()
                .Border(BoxBorder.None);

            _outputLayout.Update(panel);
            AnsiConsole.Write(_rootLayout);
        }
    }

    /// <summary>
    /// 渲染输入提示到输入区域
    /// </summary>
    /// <param name="prompt">提示文本</param>
    /// <param name="input">输入处理器</param>
    public void RenderInput(string prompt, IInputHandler input)
    {
        lock (_lockObject)
        {
            _currentPrompt = prompt;
            _currentInputHandler = input;
            UpdateInputArea();
        }
    }

    /// <summary>
    /// 锁定输入区域（流式输出时使用）
    /// </summary>
    public void LockInput()
    {
        lock (_lockObject)
        {
            _isInputLocked = true;
            UpdateInputArea();
        }
    }

    /// <summary>
    /// 解锁输入区域
    /// </summary>
    public void UnlockInput()
    {
        lock (_lockObject)
        {
            _isInputLocked = false;
            UpdateInputArea();
        }
    }

    /// <summary>
    /// 更新输入区域显示
    /// </summary>
    private void UpdateInputArea()
    {
        IRenderable content;

        if (_isInputLocked)
        {
            content = new Panel(new Markup("[grey]输入已锁定...[/]"))
                .Border(BoxBorder.Rounded)
                .BorderStyle(new Style(Color.Grey));
        }
        else if (!string.IsNullOrEmpty(_currentPrompt))
        {
            var markup = new Markup($"[green]{_currentPrompt}[/] [grey](等待输入...)[/]");
            content = new Panel(markup)
                .Border(BoxBorder.Rounded)
                .BorderStyle(new Style(Color.Green));
        }
        else
        {
            content = new Panel(new Markup("[grey]准备就绪[/]"))
                .Border(BoxBorder.Rounded)
                .BorderStyle(new Style(Color.Grey));
        }

        _inputLayout.Update(content);
    }
}
