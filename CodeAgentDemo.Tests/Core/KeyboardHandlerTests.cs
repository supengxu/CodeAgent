using CodeAgentDemo.Core;
using FluentAssertions;
using Xunit;

namespace CodeAgentDemo.Tests.Core;

public class KeyboardHandlerTests
{
    [Fact]
    public void Constructor_ShouldRegisterDefaultShortcuts()
    {
        var handler = new KeyboardHandler();

        handler.ShortcutMap.Should().ContainKey(KeyboardShortcut.Plain(ConsoleKey.Escape));
        handler.ShortcutMap.Should().ContainKey(KeyboardShortcut.Ctrl(ConsoleKey.D));
    }

    [Fact]
    public void ProcessKey_EscapeKey_ShouldReturnCancelInput()
    {
        var handler = new KeyboardHandler();
        var keyInfo = new ConsoleKeyInfo('\0', ConsoleKey.Escape, false, false, false);

        var action = handler.ProcessKey(keyInfo);

        action.Should().Be(KeyboardAction.CancelInput);
    }

    [Fact]
    public void ProcessKey_CtrlD_ShouldReturnDeleteCurrentLine()
    {
        var handler = new KeyboardHandler();
        var keyInfo = new ConsoleKeyInfo('\0', ConsoleKey.D, false, false, true);

        var action = handler.ProcessKey(keyInfo);

        action.Should().Be(KeyboardAction.DeleteCurrentLine);
    }

    [Fact]
    public void ProcessKey_RegularKey_ShouldReturnNone()
    {
        var handler = new KeyboardHandler();
        var keyInfo = new ConsoleKeyInfo('A', ConsoleKey.A, false, false, false);

        var action = handler.ProcessKey(keyInfo);

        action.Should().Be(KeyboardAction.None);
    }

    [Fact]
    public void ProcessKey_PlainD_ShouldReturnNone()
    {
        var handler = new KeyboardHandler();
        var keyInfo = new ConsoleKeyInfo('d', ConsoleKey.D, false, false, false);

        var action = handler.ProcessKey(keyInfo);

        action.Should().Be(KeyboardAction.None);
    }

    [Fact]
    public void RegisterShortcut_NewShortcut_ShouldAddToMap()
    {
        var handler = new KeyboardHandler();
        var shortcut = KeyboardShortcut.Ctrl(ConsoleKey.S);

        handler.RegisterShortcut(shortcut, KeyboardAction.CancelInput);

        handler.ShortcutMap.Should().ContainKey(shortcut);
        handler.ShortcutMap[shortcut].Should().Be(KeyboardAction.CancelInput);
    }

    [Fact]
    public void RegisterShortcut_ExistingShortcut_ShouldOverwrite()
    {
        var handler = new KeyboardHandler();
        var shortcut = KeyboardShortcut.Plain(ConsoleKey.Escape);

        handler.RegisterShortcut(shortcut, KeyboardAction.DeleteCurrentLine);

        handler.ShortcutMap[shortcut].Should().Be(KeyboardAction.DeleteCurrentLine);
    }

    [Fact]
    public void UnregisterShortcut_ExistingShortcut_ShouldRemove()
    {
        var handler = new KeyboardHandler();
        var shortcut = KeyboardShortcut.Plain(ConsoleKey.Escape);

        var result = handler.UnregisterShortcut(shortcut);

        result.Should().BeTrue();
        handler.ShortcutMap.Should().NotContainKey(shortcut);
    }

    [Fact]
    public void UnregisterShortcut_NonExistingShortcut_ShouldReturnFalse()
    {
        var handler = new KeyboardHandler();
        var shortcut = KeyboardShortcut.Plain(ConsoleKey.F1);

        var result = handler.UnregisterShortcut(shortcut);

        result.Should().BeFalse();
    }

    [Fact]
    public void MatchesShortcut_MatchingKey_ShouldReturnTrue()
    {
        var keyInfo = new ConsoleKeyInfo('\0', ConsoleKey.Escape, false, false, false);
        var shortcut = KeyboardShortcut.Plain(ConsoleKey.Escape);

        var result = KeyboardHandler.MatchesShortcut(keyInfo, shortcut);

        result.Should().BeTrue();
    }

    [Fact]
    public void MatchesShortcut_NonMatchingKey_ShouldReturnFalse()
    {
        var keyInfo = new ConsoleKeyInfo('\0', ConsoleKey.A, false, false, false);
        var shortcut = KeyboardShortcut.Plain(ConsoleKey.Escape);

        var result = KeyboardHandler.MatchesShortcut(keyInfo, shortcut);

        result.Should().BeFalse();
    }

    [Fact]
    public void MatchesShortcut_MatchingCtrlKey_ShouldReturnTrue()
    {
        var keyInfo = new ConsoleKeyInfo('\0', ConsoleKey.D, false, false, true);
        var shortcut = KeyboardShortcut.Ctrl(ConsoleKey.D);

        var result = KeyboardHandler.MatchesShortcut(keyInfo, shortcut);

        result.Should().BeTrue();
    }

    [Fact]
    public void MatchesShortcut_NonMatchingModifier_ShouldReturnFalse()
    {
        var keyInfo = new ConsoleKeyInfo('\0', ConsoleKey.D, false, false, false);
        var shortcut = KeyboardShortcut.Ctrl(ConsoleKey.D);

        var result = KeyboardHandler.MatchesShortcut(keyInfo, shortcut);

        result.Should().BeFalse();
    }

    [Fact]
    public void GetAllShortcuts_ShouldReturnAllRegisteredShortcuts()
    {
        var handler = new KeyboardHandler();

        var shortcuts = handler.GetAllShortcuts().ToList();

        shortcuts.Should().HaveCount(2);
        shortcuts.Should().Contain(s => s.Shortcut.Key == ConsoleKey.Escape);
        shortcuts.Should().Contain(s => s.Shortcut.Key == ConsoleKey.D);
    }

    [Fact]
    public void FormatShortcut_PlainKey_ShouldReturnKeyName()
    {
        var shortcut = KeyboardShortcut.Plain(ConsoleKey.Escape);

        var result = KeyboardHandler.FormatShortcut(shortcut);

        result.Should().Be("Escape");
    }

    [Fact]
    public void FormatShortcut_CtrlKey_ShouldReturnCtrlPlusKeyName()
    {
        var shortcut = KeyboardShortcut.Ctrl(ConsoleKey.D);

        var result = KeyboardHandler.FormatShortcut(shortcut);

        result.Should().Be("Ctrl+D");
    }

    [Fact]
    public void FormatShortcut_AltKey_ShouldReturnAltPlusKeyName()
    {
        var shortcut = KeyboardShortcut.Alt(ConsoleKey.F1);

        var result = KeyboardHandler.FormatShortcut(shortcut);

        result.Should().Be("Alt+F1");
    }

    [Fact]
    public void FormatShortcut_ShiftKey_ShouldReturnShiftPlusKeyName()
    {
        var shortcut = KeyboardShortcut.Shift(ConsoleKey.Tab);

        var result = KeyboardHandler.FormatShortcut(shortcut);

        result.Should().Be("Shift+Tab");
    }

    [Fact]
    public void FormatShortcut_CtrlAltKey_ShouldReturnCombinedModifiers()
    {
        var shortcut = new KeyboardShortcut(ConsoleKey.D, ConsoleModifiers.Control | ConsoleModifiers.Alt);

        var result = KeyboardHandler.FormatShortcut(shortcut);

        result.Should().Be("Ctrl+Alt+D");
    }

    [Fact]
    public void KeyboardShortcut_Plain_ShouldCreateShortcutWithoutModifiers()
    {
        var shortcut = KeyboardShortcut.Plain(ConsoleKey.A);

        shortcut.Key.Should().Be(ConsoleKey.A);
        shortcut.Modifiers.Should().Be(0);
    }

    [Fact]
    public void KeyboardShortcut_Ctrl_ShouldCreateShortcutWithCtrlModifier()
    {
        var shortcut = KeyboardShortcut.Ctrl(ConsoleKey.A);

        shortcut.Key.Should().Be(ConsoleKey.A);
        shortcut.Modifiers.Should().Be(ConsoleModifiers.Control);
    }

    [Fact]
    public void KeyboardShortcut_Alt_ShouldCreateShortcutWithAltModifier()
    {
        var shortcut = KeyboardShortcut.Alt(ConsoleKey.A);

        shortcut.Key.Should().Be(ConsoleKey.A);
        shortcut.Modifiers.Should().Be(ConsoleModifiers.Alt);
    }

    [Fact]
    public void KeyboardShortcut_Shift_ShouldCreateShortcutWithShiftModifier()
    {
        var shortcut = KeyboardShortcut.Shift(ConsoleKey.A);

        shortcut.Key.Should().Be(ConsoleKey.A);
        shortcut.Modifiers.Should().Be(ConsoleModifiers.Shift);
    }

    [Fact]
    public void KeyboardShortcut_Equality_ShouldWorkCorrectly()
    {
        var shortcut1 = KeyboardShortcut.Ctrl(ConsoleKey.D);
        var shortcut2 = KeyboardShortcut.Ctrl(ConsoleKey.D);
        var shortcut3 = KeyboardShortcut.Plain(ConsoleKey.D);

        shortcut1.Should().Be(shortcut2);
        shortcut1.Should().NotBe(shortcut3);
    }
}

public class KeyboardHandlerWithMultiLineInputTests
{
    [Fact]
    public void CancelInput_WhenEscPressed_ShouldCancelInput()
    {
        var input = new MultiLineInput();
        input.HandleKey(new ConsoleKeyInfo('H', ConsoleKey.H, false, false, false));
        input.HandleKey(new ConsoleKeyInfo('i', ConsoleKey.I, false, false, false));

        input.CancelInput();

        input.CurrentText.Should().BeEmpty();
    }

    [Fact]
    public void DeleteCurrentLine_WhenCtrlDPressed_ShouldRemoveLine()
    {
        var input = MultiLineInput.WithInitialText("Line1\nLine2\nLine3");

        input.DeleteLineAt(1);

        input.CurrentText.Should().Be("Line1\nLine3");
    }

    [Fact]
    public void Integration_EscapeWithHandler_ShouldTriggerCancel()
    {
        var handler = new KeyboardHandler();
        var input = MultiLineInput.WithInitialText("Hello World");
        var escKey = new ConsoleKeyInfo('\0', ConsoleKey.Escape, false, false, false);

        var action = handler.ProcessKey(escKey);

        action.Should().Be(KeyboardAction.CancelInput);
        if (action == KeyboardAction.CancelInput)
        {
            input.CancelInput();
        }
        input.CurrentText.Should().BeEmpty();
    }

    [Fact]
    public void Integration_CtrlDWithHandler_ShouldTriggerDeleteLine()
    {
        var handler = new KeyboardHandler();
        var input = MultiLineInput.WithInitialText("Line1\nLine2\nLine3");
        var ctrlD = new ConsoleKeyInfo('\0', ConsoleKey.D, false, false, true);

        var action = handler.ProcessKey(ctrlD);

        action.Should().Be(KeyboardAction.DeleteCurrentLine);
        if (action == KeyboardAction.DeleteCurrentLine)
        {
            var (line, _) = input.GetCursorPosition();
            input.DeleteLineAt(line);
        }
        // Cursor starts at last line (Line3), so DeleteLineAt removes Line3
        input.CurrentText.Should().Be("Line1\nLine2");
    }
}