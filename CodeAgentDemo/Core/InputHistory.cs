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
        _currentIndex = _history.Count; // Position "after end" = no history item displayed
    }

    public string? Previous()
    {
        if (_history.Count == 0)
        {
            return null;
        }

        // If at end, start from most recent (last index)
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

        // If at end, just return null (nothing more to show)
        if (_currentIndex >= _history.Count)
        {
            return null;
        }

        // Move forward in history
        if (_currentIndex < _history.Count - 1)
        {
            _currentIndex++;
            return _history[_currentIndex];
        }

        // At the last item, move to "after end" position
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