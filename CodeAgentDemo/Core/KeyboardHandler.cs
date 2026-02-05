using System.Diagnostics.CodeAnalysis;

namespace CodeAgentDemo.Core;

/// <summary>
/// 键盘快捷键定义
/// </summary>
public readonly record struct KeyboardShortcut(ConsoleKey Key, ConsoleModifiers Modifiers = 0)
{
    /// <summary>
    /// 创建无修饰键的快捷键
    /// </summary>
    public static KeyboardShortcut Plain(ConsoleKey key) => new(key, 0);

    /// <summary>
    /// 创建 Ctrl 组合键
    /// </summary>
    public static KeyboardShortcut Ctrl(ConsoleKey key) => new(key, ConsoleModifiers.Control);

    /// <summary>
    /// 创建 Alt 组合键
    /// </summary>
    public static KeyboardShortcut Alt(ConsoleKey key) => new(key, ConsoleModifiers.Alt);

    /// <summary>
    /// 创建 Shift 组合键
    /// </summary>
    public static KeyboardShortcut Shift(ConsoleKey key) => new(key, ConsoleModifiers.Shift);
}

/// <summary>
/// 键盘动作类型
/// </summary>
public enum KeyboardAction
{
    /// <summary>
    /// 无动作
    /// </summary>
    None,

    /// <summary>
    /// 取消输入
    /// </summary>
    CancelInput,

    /// <summary>
    /// 删除当前行
    /// </summary>
    DeleteCurrentLine
}

/// <summary>
/// 键盘快捷键处理器
/// 提供快捷键映射和处理功能
/// </summary>
public class KeyboardHandler
{
    private readonly Dictionary<KeyboardShortcut, KeyboardAction> _shortcutMap;

    /// <summary>
    /// 获取快捷键映射表（只读）
    /// </summary>
    public IReadOnlyDictionary<KeyboardShortcut, KeyboardAction> ShortcutMap => _shortcutMap;

    /// <summary>
    /// 初始化键盘处理器，注册默认快捷键
    /// </summary>
    public KeyboardHandler()
    {
        _shortcutMap = new Dictionary<KeyboardShortcut, KeyboardAction>();
        RegisterDefaultShortcuts();
    }

    private void RegisterDefaultShortcuts()
    {
        RegisterShortcut(KeyboardShortcut.Plain(ConsoleKey.Escape), KeyboardAction.CancelInput);
        RegisterShortcut(KeyboardShortcut.Ctrl(ConsoleKey.D), KeyboardAction.DeleteCurrentLine);
    }

    /// <summary>
    /// 注册自定义快捷键
    /// </summary>
    /// <param name="shortcut">快捷键定义</param>
    /// <param name="action">关联的动作</param>
    public void RegisterShortcut(KeyboardShortcut shortcut, KeyboardAction action)
    {
        _shortcutMap[shortcut] = action;
    }

    /// <summary>
    /// 移除快捷键
    /// </summary>
    /// <param name="shortcut">要移除的快捷键</param>
    /// <returns>是否成功移除</returns>
    public bool UnregisterShortcut(KeyboardShortcut shortcut)
    {
        return _shortcutMap.Remove(shortcut);
    }

    /// <summary>
    /// 处理键盘输入，返回对应的动作
    /// </summary>
    /// <param name="keyInfo">键盘输入信息</param>
    /// <returns>匹配的键盘动作，若无匹配则返回 None</returns>
    public KeyboardAction ProcessKey(ConsoleKeyInfo keyInfo)
    {
        var shortcut = CreateShortcutFromKeyInfo(keyInfo);

        if (_shortcutMap.TryGetValue(shortcut, out var action))
        {
            return action;
        }

        return KeyboardAction.None;
    }

    /// <summary>
    /// 检查按键是否匹配指定快捷键
    /// </summary>
    /// <param name="keyInfo">键盘输入信息</param>
    /// <param name="shortcut">目标快捷键</param>
    /// <returns>是否匹配</returns>
    public static bool MatchesShortcut(ConsoleKeyInfo keyInfo, KeyboardShortcut shortcut)
    {
        return keyInfo.Key == shortcut.Key && keyInfo.Modifiers == shortcut.Modifiers;
    }

    /// <summary>
    /// 从 ConsoleKeyInfo 创建 KeyboardShortcut
    /// </summary>
    /// <param name="keyInfo">键盘输入信息</param>
    /// <returns>对应的快捷键定义</returns>
    [SuppressMessage("Performance", "CA1851:Possible multiple enumerations of 'IEnumerable' collection", Justification = "HasFlag is more readable")]
    private static KeyboardShortcut CreateShortcutFromKeyInfo(ConsoleKeyInfo keyInfo)
    {
        var modifiers = keyInfo.Modifiers;
        var relevantModifiers = ConsoleModifiers.None;

        if (modifiers.HasFlag(ConsoleModifiers.Control))
        {
            relevantModifiers |= ConsoleModifiers.Control;
        }

        if (modifiers.HasFlag(ConsoleModifiers.Alt))
        {
            relevantModifiers |= ConsoleModifiers.Alt;
        }

        // Shift as modifier only for non-letter keys (for letter keys, Shift indicates uppercase)
        if (modifiers.HasFlag(ConsoleModifiers.Shift) && !char.IsLetter(keyInfo.KeyChar))
        {
            relevantModifiers |= ConsoleModifiers.Shift;
        }

        return new KeyboardShortcut(keyInfo.Key, relevantModifiers);
    }

    /// <summary>
    /// 获取所有已注册的快捷键描述
    /// </summary>
    /// <returns>快捷键描述列表</returns>
    public IEnumerable<(KeyboardShortcut Shortcut, KeyboardAction Action)> GetAllShortcuts()
    {
        return _shortcutMap.Select(kvp => (kvp.Key, kvp.Value));
    }

    /// <summary>
    /// 格式化快捷键为可读字符串
    /// </summary>
    /// <param name="shortcut">快捷键定义</param>
    /// <returns>格式化后的字符串</returns>
    public static string FormatShortcut(KeyboardShortcut shortcut)
    {
        var parts = new List<string>();

        if (shortcut.Modifiers.HasFlag(ConsoleModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (shortcut.Modifiers.HasFlag(ConsoleModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (shortcut.Modifiers.HasFlag(ConsoleModifiers.Shift))
        {
            parts.Add("Shift");
        }

        parts.Add(shortcut.Key.ToString());

        return string.Join("+", parts);
    }
}