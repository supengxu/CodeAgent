namespace CodeAgentDemo.Models;

public enum InputState
{
    Submitted,
    Cancelled,
    Empty
}

public record InputResult(string Text, InputState State)
{
    public static InputResult Submitted(string text) => new(text, InputState.Submitted);
    public static InputResult Cancelled() => new(string.Empty, InputState.Cancelled);
    public static InputResult Empty() => new(string.Empty, InputState.Empty);
}