using CodeAgentDemo.Models;

namespace CodeAgentDemo.Core;

public class MultiLineInput : IInputHandler
{
    private readonly List<string> _lines = new();
    private int _currentLine = 0;
    private int _currentColumn = 0;
    private bool _isCancelled = false;
    private int _renderStartLine = 0;
    private bool _isFirstRender = true;

    public string Prompt { get; set; }

    public string CurrentText => string.Join("\n", _lines);

    public MultiLineInput(string prompt = "> ")
    {
        Prompt = prompt;
        _lines.Add(string.Empty);
    }

    public static MultiLineInput WithInitialText(string initialText, string prompt = "> ")
    {
        var input = new MultiLineInput(prompt);
        input._lines.Clear();

        if (!string.IsNullOrEmpty(initialText))
        {
            var lines = initialText.Split('\n');
            input._lines.AddRange(lines);
            input._currentLine = lines.Length - 1;
            input._currentColumn = lines.Last().Length;
        }
        else
        {
            input._lines.Add(string.Empty);
        }

        return input;
    }

    public Task<InputResult> ReadInputAsync(CancellationToken ct)
    {
        _isCancelled = false;
        _isFirstRender = true;
        _renderStartLine = Console.CursorTop;

        while (!ct.IsCancellationRequested && !_isCancelled)
        {
            Render();

            var keyInfo = Console.ReadKey(true);
            ct.ThrowIfCancellationRequested();

            var result = HandleKey(keyInfo);

            if (result != null)
            {
                ClearInputArea();
                return Task.FromResult(result);
            }
        }

        return Task.FromResult(InputResult.Cancelled());
    }

    public void CancelInput()
    {
        _isCancelled = true;
        _lines.Clear();
        _lines.Add(string.Empty);
        _currentLine = 0;
        _currentColumn = 0;
    }

    public InputResult? HandleKey(ConsoleKeyInfo keyInfo)
    {
        ArgumentNullException.ThrowIfNull(keyInfo);

        // Shift+Enter = 换行（在当前光标位置插入新行）
        if (keyInfo.Modifiers.HasFlag(ConsoleModifiers.Shift) && keyInfo.Key == ConsoleKey.Enter)
        {
            HandleEnter();
            return null;
        }

        // 仅 Enter = 发送消息
        if (keyInfo.Key == ConsoleKey.Enter)
        {
            return InputResult.Submitted(CurrentText);
        }

        switch (keyInfo.Key)
        {

            case ConsoleKey.UpArrow:
                HandleUpArrow();
                break;

            case ConsoleKey.DownArrow:
                HandleDownArrow();
                break;

            case ConsoleKey.LeftArrow:
                HandleLeftArrow();
                break;

            case ConsoleKey.RightArrow:
                HandleRightArrow();
                break;

            case ConsoleKey.Home:
                HandleHome();
                break;

            case ConsoleKey.End:
                HandleEnd();
                break;

            case ConsoleKey.Backspace:
                HandleBack();
                break;

            case ConsoleKey.Delete:
                HandleDelete();
                break;

            default:
                if (!char.IsControl(keyInfo.KeyChar))
                {
                    Handle(keyInfo.KeyChar);
                }
                break;
        }

        return null;
    }

    public (int line, int column) GetCursorPosition()
    {
        return (_currentLine, _currentColumn);
    }

    public void SetCursorPosition(int line, int column)
    {
        if (line < 0) throw new ArgumentOutOfRangeException(nameof(line), "Line index cannot be negative");
        if (column < 0) throw new ArgumentOutOfRangeException(nameof(column), "Column index cannot be negative");

        int lineCount = _lines.Count;
        _currentLine = Math.Min(line, lineCount - 1);

        int lineLength = _lines[_currentLine].Length;
        _currentColumn = Math.Min(column, lineLength);
    }

    public int GetLineCount() => _lines.Count;

    public string GetLineAt(int index)
    {
        if (index >= 0 && index < _lines.Count)
        {
            return _lines[index];
        }
        return string.Empty;
    }

    public void DeleteLineAt(int index)
    {
        if (index >= 0 && index < _lines.Count)
        {
            _lines.RemoveAt(index);

            if (_lines.Count == 0)
            {
                _lines.Add(string.Empty);
            }

            _currentLine = Math.Min(_currentLine, _lines.Count - 1);
            _currentColumn = Math.Min(_currentColumn, _lines[_currentLine].Length);
        }
    }

    private void HandleEnter()
    {
        var currentLine = _lines[_currentLine];
        var beforeCursor = currentLine[.._currentColumn];
        var afterCursor = currentLine[_currentColumn..];

        _lines[_currentLine] = beforeCursor;
        _lines.Insert(_currentLine + 1, afterCursor);

        _currentLine++;
        _currentColumn = 0;
    }

    private void HandleUpArrow()
    {
        if (_currentLine > 0)
        {
            _currentLine--;
            int lineLength = _lines[_currentLine].Length;
            _currentColumn = Math.Min(_currentColumn, lineLength);
        }
    }

    private void HandleDownArrow()
    {
        if (_currentLine < _lines.Count - 1)
        {
            _currentLine++;
            int lineLength = _lines[_currentLine].Length;
            _currentColumn = Math.Min(_currentColumn, lineLength);
        }
    }

    private void HandleLeftArrow()
    {
        if (_currentColumn > 0)
        {
            _currentColumn--;
        }
        else if (_currentLine > 0)
        {
            _currentLine--;
            _currentColumn = _lines[_currentLine].Length;
        }
    }

    private void HandleRightArrow()
    {
        int currentLineLength = _lines[_currentLine].Length;

        if (_currentColumn < currentLineLength)
        {
            _currentColumn++;
        }
        else if (_currentLine < _lines.Count - 1)
        {
            _currentLine++;
            _currentColumn = 0;
        }
    }

    private void HandleHome()
    {
        _currentColumn = 0;
    }

    private void HandleEnd()
    {
        _currentColumn = _lines[_currentLine].Length;
    }

    private void HandleBack()
    {
        if (_currentColumn > 0)
        {
            var currentLine = _lines[_currentLine];
            _lines[_currentLine] = currentLine[..(_currentColumn - 1)] + currentLine[_currentColumn..];
            _currentColumn--;
        }
        else if (_currentLine > 0)
        {
            var prevLine = _lines[_currentLine - 1];
            var currentLine = _lines[_currentLine];
            _lines[_currentLine - 1] = prevLine + currentLine;
            _currentColumn = prevLine.Length;
            _lines.RemoveAt(_currentLine);
            _currentLine--;
        }
    }

    private void HandleDelete()
    {
        var currentLine = _lines[_currentLine];

        if (_currentColumn < currentLine.Length)
        {
            _lines[_currentLine] = currentLine[.._currentColumn] + currentLine[(_currentColumn + 1)..];
        }
        else if (_currentLine < _lines.Count - 1)
        {
            var nextLine = _lines[_currentLine + 1];
            _lines[_currentLine] = currentLine + nextLine;
            _lines.RemoveAt(_currentLine + 1);
        }
    }

    private void Handle(char c)
    {
        var currentLine = _lines[_currentLine];
        _lines[_currentLine] = currentLine[.._currentColumn] + c + currentLine[_currentColumn..];
        _currentColumn++;
    }

    private void Render()
    {
        if (_isFirstRender)
        {
            _renderStartLine = Console.CursorTop;
            _isFirstRender = false;
        }

        // 移动光标到起始行
        Console.CursorTop = _renderStartLine;
        Console.CursorLeft = 0;

        // 清除并重新绘制所有行
        for (int i = 0; i < _lines.Count; i++)
        {
            ClearCurrentLine();
            Console.Write(Prompt);
            Console.Write(_lines[i]);

            if (i < _lines.Count - 1)
            {
                Console.WriteLine();
            }
        }

        // 清除可能的多余行（如果之前有更多行）
        int currentLine = Console.CursorTop;
        int extraLines = currentLine - (_renderStartLine + _lines.Count - 1);
        for (int i = 0; i < extraLines; i++)
        {
            Console.WriteLine();
            ClearCurrentLine();
        }

        // 移动光标到当前编辑位置
        Console.CursorTop = _renderStartLine + _currentLine;
        Console.CursorLeft = Prompt.Length + GetDisplayWidth(_lines[_currentLine][.._currentColumn]);
    }

    private void ClearInputArea()
    {
        Console.CursorTop = _renderStartLine;
        Console.CursorLeft = 0;

        for (int i = 0; i < _lines.Count; i++)
        {
            ClearCurrentLine();
            if (i < _lines.Count - 1)
            {
                Console.WriteLine();
            }
        }
    }

    private void ClearCurrentLine()
    {
        Console.Write("\x1b[2K");
        Console.Write("\r");
    }

    private static int GetDisplayWidth(string text)
    {
        int width = 0;
        foreach (var c in text)
        {
            width += IsWideChar(c) ? 2 : 1;
        }
        return width;
    }

    private static bool IsWideChar(char c)
    {
        return (c >= '\u4E00' && c <= '\u9FFF') ||
               (c >= '\u3400' && c <= '\u4DBF') ||
               (c >= '\u3000' && c <= '\u303F') ||
               (c >= '\uFF00' && c <= '\uFFEF') ||
               (c >= '\u3040' && c <= '\u309F') ||
               (c >= '\u30A0' && c <= '\u30FF') ||
               (c >= '\uAC00' && c <= '\uD7AF');
    }
}
