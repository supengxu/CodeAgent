using CodeAgentDemo.Models;
using Spectre.Console;

namespace CodeAgentDemo.Core;

/// <summary>
/// 使用 Spectre.Console 实现的 IConsoleIO 接口
/// 支持多行输入和单行输入模式
/// </summary>
public class SpectreConsoleIO : IConsoleIO
{
    private readonly bool _useMultiLine;

    /// <summary>
    /// 创建 SpectreConsoleIO 实例
    /// </summary>
    /// <param name="useMultiLine">是否使用多行输入模式，默认 true</param>
    public SpectreConsoleIO(bool useMultiLine = true)
    {
        _useMultiLine = useMultiLine;
    }

    /// <inheritdoc />
    public void Write(string value)
    {
        AnsiConsole.Write(value);
    }

    /// <inheritdoc />
    public void WriteLine(string? value = null)
    {
        AnsiConsole.WriteLine(value ?? string.Empty);
    }

    /// <inheritdoc />
    public string? ReadLine()
    {
        if (_useMultiLine)
        {
            return ReadMultiLine();
        }

        return ReadSingleLine();
    }

    /// <summary>
    /// 使用 Spectre.Console 读取单行输入（简单场景）
    /// </summary>
    private string? ReadSingleLine()
    {
        var input = AnsiConsole.Ask<string>("");
        return input;
    }

    /// <summary>
    /// 使用 MultiLineInput 读取多行输入（复杂场景）
    /// </summary>
    private string? ReadMultiLine()
    {
        using var cts = new CancellationTokenSource();

        // 设置 Ctrl+C 取消
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        var multiLineInput = new MultiLineInput("> ");

        try
        {
            var result = multiLineInput.ReadInputAsync(cts.Token).GetAwaiter().GetResult();

            return result.State switch
            {
                InputState.Submitted => result.Text,
                InputState.Cancelled => null,
                InputState.Empty => string.Empty,
                _ => null
            };
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    /// 提示用户确认（用于工具执行前的确认）
    /// </summary>
    /// <param name="message">确认消息</param>
    /// <returns>用户确认返回 true，否则返回 false</returns>
    public bool Confirm(string message)
    {
        return AnsiConsole.Confirm(message);
    }

    /// <summary>
    /// 从选项列表中选择
    /// </summary>
    /// <typeparam name="T">选项类型</typeparam>
    /// <param name="prompt">提示文本</param>
    /// <param name="options">选项列表</param>
    /// <returns>用户选择的选项</returns>
    public T? Select<T>(string prompt, IEnumerable<T> options) where T : notnull
    {
        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<T>()
                .Title(prompt)
                .AddChoices(options));

        return selection;
    }
}