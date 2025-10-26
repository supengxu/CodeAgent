namespace CodeAgentDemo.Models;

/// <summary>
/// Configuration for context management and compression.
/// </summary>
public class ContextConfig
{
    /// <summary>
    /// Token threshold for triggering context compression.
    /// When total tokens exceed this value, compression is considered.
    /// </summary>
    public int CompressionThreshold { get; set; } = 100000;

    /// <summary>
    /// Minimum number of recent conversation turns to preserve during compression.
    /// These turns will not be compressed or removed.
    /// </summary>
    public int MinRecentTurns { get; set; } = 4;

    /// <summary>
    /// List of context types that should be protected from compression.
    /// Examples: "system", "tool_definition", "tool_result"
    /// </summary>
    public List<string> ProtectedContextTypes { get; set; } = new()
    {
        "system",
        "tool_definition"
    };
}