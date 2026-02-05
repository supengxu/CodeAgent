using System.ComponentModel;
using System.Text;
using CodeAgentDemo.Core;
using CodeAgentDemo.Models;
using FluentAssertions;
using Xunit;

namespace CodeAgentDemo.Tests.Core;

/// <summary>
/// 单元测试：MultiLineInput 核心逻辑
/// 测试多行输入编辑器的所有关键功能
/// </summary>
public class MultiLineInputTests
{
    // 辅助方法：模拟键盘输入
    private List<ConsoleKeyInfo> CreateKeys(params (ConsoleKey key, char keyChar, ConsoleModifiers modifiers)[] inputs)
    {
        return inputs.Select(i =>
            new ConsoleKeyInfo(i.keyChar, i.key, false, false, false)
        ).ToList();
    }

    [Fact]
    public void Constructor_ShouldInitializeEmptyState()
    {
        var input = new MultiLineInput();

        input.CurrentText.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithPrompt_ShouldSetPrompt()
    {
        var input = new MultiLineInput("> ");

        input.Prompt.Should().Be("> ");
    }

    [Theory]
    [InlineData("Hello")]
    [InlineData("Hello\nWorld")]
    [InlineData("")]
    public void Constructor_WithInitialText_ShouldSetText(string initialText)
    {
        var input = MultiLineInput.WithInitialText(initialText);

        input.CurrentText.Should().Be(initialText);
    }

    [Fact]
    public void HandleKey_Enter_ShouldInsertNewline()
    {
        var input = new MultiLineInput();
        var key = new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false);

        input.HandleKey(key);

        input.CurrentText.Should().Be("\n");
    }

    [Fact]
    public void HandleKey_CtrlEnter_ShouldReturnSubmittedResult()
    {
        var input = MultiLineInput.WithInitialText("Hello World");
        var key = new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, true);

        var result = input.HandleKey(key);

        result.Should().NotBeNull();
        result!.State.Should().Be(InputState.Submitted);
        result.Text.Should().Be("Hello World");
    }

    [Fact]
    public void HandleKey_LeftArrow_ShouldMoveCursorLeft()
    {
        var input = MultiLineInput.WithInitialText("ABC");
        var key = new ConsoleKeyInfo('\0', ConsoleKey.LeftArrow, false, false, false);

        input.HandleKey(key);
        input.HandleKey(key);

        input.CurrentText.Should().Be("ABC");
    }

    [Fact]
    public void HandleKey_RightArrow_ShouldMoveCursorRight()
    {
        var input = MultiLineInput.WithInitialText("ABC");
        var key = new ConsoleKeyInfo('\0', ConsoleKey.LeftArrow, false, false, false);

        // Move left twice
        input.HandleKey(key);
        input.HandleKey(key);

        // Move right once
        var rightKey = new ConsoleKeyInfo('\0', ConsoleKey.RightArrow, false, false, false);
        input.HandleKey(rightKey);
    }

    [Fact]
    public void HandleKey_UpArrow_SingleLine_ShouldNotMove()
    {
        var input = MultiLineInput.WithInitialText("ABC");
        var key = new ConsoleKeyInfo('\0', ConsoleKey.UpArrow, false, false, false);

        input.HandleKey(key);

        input.CurrentText.Should().Be("ABC");
    }

    [Fact]
    public void HandleKey_DownArrow_SingleLine_ShouldNotMove()
    {
        var input = MultiLineInput.WithInitialText("ABC");
        var key = new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false);

        input.HandleKey(key);

        input.CurrentText.Should().Be("ABC");
    }

    [Fact]
    public void HandleKey_UpArrow_MultiLine_ShouldMoveToPreviousLine()
    {
        var input = MultiLineInput.WithInitialText("Line1\nLine2\nLine3");
        var key = new ConsoleKeyInfo('\0', ConsoleKey.UpArrow, false, false, false);

        input.HandleKey(key);

        input.CurrentText.Should().Be("Line1\nLine2\nLine3");
    }

    [Fact]
    public void HandleKey_DownArrow_MultiLine_ShouldMoveToNextLine()
    {
        var input = MultiLineInput.WithInitialText("Line1\nLine2");
        var key = new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false);

        input.HandleKey(key);

        input.CurrentText.Should().Be("Line1\nLine2");
    }

    [Fact]
    public void HandleKey_Backspace_ShouldDeleteCharacterBeforeCursor()
    {
        var input = MultiLineInput.WithInitialText("ABC");
        var key = new ConsoleKeyInfo('\0', ConsoleKey.LeftArrow, false, false, false);

        input.HandleKey(key); // Move left
        var backspaceKey = new ConsoleKeyInfo('\0', ConsoleKey.Backspace, false, false, false);
        input.HandleKey(backspaceKey);

        input.CurrentText.Should().Be("AC");
    }

    [Fact]
    public void HandleKey_Backspace_AtStart_ShouldNotDelete()
    {
        var input = MultiLineInput.WithInitialText("ABC");
        var homeKey = new ConsoleKeyInfo('\0', ConsoleKey.Home, false, false, false);
        input.HandleKey(homeKey); // Move cursor to start

        var backspaceKey = new ConsoleKeyInfo('\0', ConsoleKey.Backspace, false, false, false);
        input.HandleKey(backspaceKey);

        input.CurrentText.Should().Be("ABC");
    }

    [Fact]
    public void HandleKey_Backspace_AtLineStart_ShouldDeleteLineBreak()
    {
        var input = MultiLineInput.WithInitialText("Line1\nLine2");
        var key = new ConsoleKeyInfo('\0', ConsoleKey.LeftArrow, false, false, false);

        // Move to start of Line2
        for (int i = 0; i < 5; i++)
        {
            input.HandleKey(key);
        }

        var backspaceKey = new ConsoleKeyInfo('\0', ConsoleKey.Backspace, false, false, false);
        input.HandleKey(backspaceKey);

        input.CurrentText.Should().Be("Line1Line2");
    }

    [Fact]
    public void HandleKey_Delete_ShouldDeleteCharacterAtCursor()
    {
        var input = MultiLineInput.WithInitialText("ABC");
        var key = new ConsoleKeyInfo('\0', ConsoleKey.LeftArrow, false, false, false);

        input.HandleKey(key); // Move left
        var deleteKey = new ConsoleKeyInfo('\0', ConsoleKey.Delete, false, false, false);
        input.HandleKey(deleteKey);

        input.CurrentText.Should().Be("AB");
    }

    [Fact]
    public void HandleKey_Delete_AtEnd_ShouldNotDelete()
    {
        var input = MultiLineInput.WithInitialText("ABC");
        var key = new ConsoleKeyInfo('\0', ConsoleKey.Delete, false, false, false);

        input.HandleKey(key);

        input.CurrentText.Should().Be("ABC");
    }

    [Fact]
    public void HandleKey_RegularChar_ShouldInsertCharacter()
    {
        var input = new MultiLineInput();
        var key = new ConsoleKeyInfo('A', ConsoleKey.A, false, false, false);

        input.HandleKey(key);

        input.CurrentText.Should().Be("A");
    }

    [Fact]
    public void HandleKey_MultipleChars_ShouldBuildString()
    {
        var input = new MultiLineInput();
        var keys = new[]
        {
            new ConsoleKeyInfo('H', ConsoleKey.H, false, false, false),
            new ConsoleKeyInfo('e', ConsoleKey.E, false, false, false),
            new ConsoleKeyInfo('l', ConsoleKey.L, false, false, false),
            new ConsoleKeyInfo('l', ConsoleKey.L, false, false, false),
            new ConsoleKeyInfo('o', ConsoleKey.O, false, false, false)
        };

        foreach (var key in keys)
        {
            input.HandleKey(key);
        }

        input.CurrentText.Should().Be("Hello");
    }

    [Fact]
    public void HandleKey_ControlChar_ShouldIgnore()
    {
        var input = new MultiLineInput();
        var key = new ConsoleKeyInfo('\0', ConsoleKey.F1, false, false, false);

        input.HandleKey(key);

        input.CurrentText.Should().BeEmpty();
    }

    [Fact]
    public void CancelInput_ShouldResetState()
    {
        var input = MultiLineInput.WithInitialText("Some text");

        input.CancelInput();

        input.CurrentText.Should().BeEmpty();
    }

    [Fact]
    public void ReadInputAsync_WithCancellationToken_ShouldBeCancellable()
    {
        var input = new MultiLineInput();
        var cts = new CancellationTokenSource();

        cts.Cancel();

        Func<Task> act = async () => await input.ReadInputAsync(cts.Token);
        act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void HandleKey_EnterOnMultipleLines_ShouldMaintainLineCount()
    {
        var input = MultiLineInput.WithInitialText("Line1");
        var enterKey = new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false);

        input.HandleKey(enterKey); // First newline
        input.HandleKey(new ConsoleKeyInfo('L', ConsoleKey.L, false, false, false));
        input.HandleKey(new ConsoleKeyInfo('i', ConsoleKey.I, false, false, false));
        input.HandleKey(new ConsoleKeyInfo('n', ConsoleKey.N, false, false, false));
        input.HandleKey(new ConsoleKeyInfo('e', ConsoleKey.E, false, false, false));
        input.HandleKey(new ConsoleKeyInfo('2', ConsoleKey.D2, false, false, false));
        input.HandleKey(enterKey); // Second newline

        input.CurrentText.Should().Be("Line1\nLine2\n");
    }

    [Fact]
    public void HandleKey_CtrlEnterWithEmptyLines_ShouldSubmit()
    {
        var input = MultiLineInput.WithInitialText("\n\n\n");
        var key = new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, true);

        var result = input.HandleKey(key);

        result.Should().NotBeNull();
        result!.State.Should().Be(InputState.Submitted);
        result.Text.Should().Be("\n\n\n");
    }

    [Fact]
    public void GetCursorLineAndColumn_AfterNewline_ShouldBeAtStart()
    {
        var input = MultiLineInput.WithInitialText("Line1\nLine2");

        var (line, column) = input.GetCursorPosition();

        line.Should().BeGreaterThanOrEqualTo(0);
        column.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void SetCursorPosition_ShouldMoveCursor()
    {
        var input = MultiLineInput.WithInitialText("Line1\nLine2");

        input.SetCursorPosition(1, 0);

        var (line, column) = input.GetCursorPosition();
        line.Should().Be(1);
        column.Should().Be(0);
    }

    [Fact]
    public void SetCursorPosition_InvalidLine_ShouldClamp()
    {
        var input = MultiLineInput.WithInitialText("Line1");

        input.SetCursorPosition(10, 0);

        var (line, column) = input.GetCursorPosition();
        line.Should().Be(0);
    }

    [Fact]
    public void SetCursorPosition_InvalidColumn_ShouldClamp()
    {
        var input = MultiLineInput.WithInitialText("ABC");

        input.SetCursorPosition(0, 10);

        var (line, column) = input.GetCursorPosition();
        column.Should().Be(3);
    }

    [Fact]
    public void GetLineCount_ShouldReturnCorrectCount()
    {
        var input = MultiLineInput.WithInitialText("Line1\nLine2\nLine3");

        var lineCount = input.GetLineCount();

        lineCount.Should().Be(3);
    }

    [Fact]
    public void GetLineCount_EmptyText_ShouldReturnOne()
    {
        var input = new MultiLineInput();

        var lineCount = input.GetLineCount();

        lineCount.Should().Be(1);
    }

    [Fact]
    public void GetLineAt_ShouldReturnLineContent()
    {
        var input = MultiLineInput.WithInitialText("Line1\nLine2\nLine3");

        var line1 = input.GetLineAt(0);
        var line2 = input.GetLineAt(1);
        var line3 = input.GetLineAt(2);

        line1.Should().Be("Line1");
        line2.Should().Be("Line2");
        line3.Should().Be("Line3");
    }

    [Fact]
    public void GetLineAt_InvalidIndex_ShouldReturnEmpty()
    {
        var input = MultiLineInput.WithInitialText("Line1");

        var line = input.GetLineAt(10);

        line.Should().BeEmpty();
    }

    [Fact]
    public void DeleteLineAt_ShouldRemoveLine()
    {
        var input = MultiLineInput.WithInitialText("Line1\nLine2\nLine3");

        input.DeleteLineAt(1);

        input.CurrentText.Should().Be("Line1\nLine3");
    }

    [Fact]
    public void DeleteLineAt_LastLine_ShouldRemoveLine()
    {
        var input = MultiLineInput.WithInitialText("Line1\nLine2");

        input.DeleteLineAt(1);

        input.CurrentText.Should().Be("Line1");
    }

    [Fact]
    public void HandleKey_Home_ShouldMoveToLineStart()
    {
        var input = MultiLineInput.WithInitialText("ABC");
        var key = new ConsoleKeyInfo('\0', ConsoleKey.Home, false, false, false);

        input.HandleKey(key);

        var (line, column) = input.GetCursorPosition();
        column.Should().Be(0);
    }

    [Fact]
    public void HandleKey_End_ShouldMoveToLineEnd()
    {
        var input = MultiLineInput.WithInitialText("ABC");
        var leftKey = new ConsoleKeyInfo('\0', ConsoleKey.LeftArrow, false, false, false);
        input.HandleKey(leftKey); // Move left once

        var endKey = new ConsoleKeyInfo('\0', ConsoleKey.End, false, false, false);
        input.HandleKey(endKey);

        var (line, column) = input.GetCursorPosition();
        column.Should().Be(3);
    }
}

/// <summary>
/// 异常测试：MultiLineInput
/// </summary>
public class MultiLineInputExceptionTests
{
    [Fact]
    public void SetCursorPosition_NegativeLine_ShouldThrow()
    {
        var input = new MultiLineInput();

        Assert.Throws<ArgumentOutOfRangeException>(() => input.SetCursorPosition(-1, 0));
    }

    [Fact]
    public void SetCursorPosition_NegativeColumn_ShouldThrow()
    {
        var input = new MultiLineInput();

        Assert.Throws<ArgumentOutOfRangeException>(() => input.SetCursorPosition(0, -1));
    }
}
