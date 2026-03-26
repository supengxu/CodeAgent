namespace CodeAgentDemo.Core;

public class InputHistory
{
    private readonly List<string> _history = new();
    private int _currentIndex = 0;

    public void Add(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        if (_history.Contains(input))
        {
            return;
        }

        _history.Add(input);
        _currentIndex = _history.Count; // 定位在"末尾"之后 = 不显示历史项
    }

    public string? Previous()
    {
        if (_history.Count == 0)
        {
            return null;
        }

        // 如果在末尾，从最近的历史项开始（最后一个索引）
        if (_currentIndex >= _history.Count)
        {
            _currentIndex = _history.Count - 1;
        }
        else if (_currentIndex > 0)
        {
            _currentIndex--;
        }

        return _history[_currentIndex];
    }

    public string? Next()
    {
        if (_history.Count == 0)
        {
            return null;
        }

        // 如果在末尾，直接返回 null（没有更多内容显示）
        if (_currentIndex >= _history.Count)
        {
            return null;
        }

        // 在历史记录中向前移动
        if (_currentIndex < _history.Count - 1)
        {
            _currentIndex++;
            return _history[_currentIndex];
        }

        // 在最后一项，移动到"末尾"之后的位置
        _currentIndex = _history.Count;
        return null;
    }

    public void ResetNavigation()
    {
        _currentIndex = _history.Count;
    }

    public void Clear()
    {
        _history.Clear();
        _currentIndex = 0;
    }

    public int Count => _history.Count;

    public bool HasHistory => _history.Count > 0;

    internal int CurrentIndex => _currentIndex;
}