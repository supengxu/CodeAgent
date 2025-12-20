using System.Text;

namespace CodeAgentDemo.Core;

public interface IConsoleIO
{
    void Write(string value);
    void WriteLine(string? value = null);
    string? ReadLine();
}

public class DefaultConsoleIO : IConsoleIO
{
    public void Write(string value) => Console.Write(value);

    public void WriteLine(string? value = null) => Console.WriteLine(value ?? string.Empty);

    public string? ReadLine()
    {
        var input = new StringBuilder();
        var history = new List<string>();
        var historyIndex = -1;

        while (true)
        {
            var key = Console.ReadKey(true);

            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    Console.WriteLine();
                    if (input.Length > 0)
                    {
                        history.Add(input.ToString());
                        historyIndex = history.Count;
                    }
                    return input.ToString();

                case ConsoleKey.Backspace:
                    if (input.Length > 0)
                    {
                        var lastChar = input[^1];
                        input.Remove(input.Length - 1, 1);
                        var displayWidth = GetDisplayWidth(lastChar);
                        Console.Write(new string('\b', displayWidth) + new string(' ', displayWidth) + new string('\b', displayWidth));
                    }
                    break;

                case ConsoleKey.UpArrow:
                    if (history.Count > 0 && historyIndex > 0)
                    {
                        historyIndex--;
                        ClearLine(input);
                        input.Clear();
                        input.Append(history[historyIndex]);
                        Console.Write(input.ToString());
                    }
                    break;

                case ConsoleKey.DownArrow:
                    if (historyIndex < history.Count - 1)
                    {
                        historyIndex++;
                        ClearLine(input);
                        input.Clear();
                        input.Append(history[historyIndex]);
                        Console.Write(input.ToString());
                    }
                    break;

                case ConsoleKey.Escape:
                    ClearLine(input);
                    input.Clear();
                    break;

                default:
                    if (!char.IsControl(key.KeyChar))
                    {
                        input.Append(key.KeyChar);
                        Console.Write(key.KeyChar);
                    }
                    break;
            }
        }
    }

    private void ClearLine(StringBuilder input)
    {
        var totalWidth = 0;
        foreach (var c in input.ToString())
            totalWidth += GetDisplayWidth(c);

        if (totalWidth > 0)
            Console.Write(new string('\b', totalWidth) + new string(' ', totalWidth) + new string('\b', totalWidth));
    }

    private static int GetDisplayWidth(char c)
    {
        if (c >= 0x4E00 && c <= 0x9FFF ||
            c >= 0x3400 && c <= 0x4DBF ||
            c >= 0x3000 && c <= 0x303F ||
            c >= 0xFF00 && c <= 0xFFEF)
            return 2;
        return 1;
    }
}