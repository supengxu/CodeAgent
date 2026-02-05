using System;
using System.IO;
using System.Text;
using CodeAgentDemo.Core;
using Moq;
using Xunit;

namespace CodeAgentDemo.Tests.Core;

public class SplitLayoutRendererTests : IDisposable
{
    private readonly Mock<IInputHandler> _mockInputHandler;
    private readonly StringBuilder _outputCapture;
    private readonly TextWriter _originalOutput;

    public SplitLayoutRendererTests()
    {
        _mockInputHandler = new Mock<IInputHandler>();
        _outputCapture = new StringBuilder();
        _originalOutput = Console.Out;
        Console.SetOut(new StringWriter(_outputCapture));
    }

    void IDisposable.Dispose()
    {
        Console.SetOut(_originalOutput);
    }

    [Fact]
    public void Constructor_CreatesLayoutWithoutError()
    {
        var renderer = new SplitLayoutRenderer();
        Assert.NotNull(renderer);
    }

    [Fact]
    public void RenderOutput_WithContent_DoesNotThrow()
    {
        var renderer = new SplitLayoutRenderer();
        var testContent = "Test output content";
        var exception = Record.Exception(() => renderer.RenderOutput(testContent));
        Assert.Null(exception);
    }

    [Fact]
    public void RenderInput_WithPrompt_DoesNotThrow()
    {
        var renderer = new SplitLayoutRenderer();
        var testPrompt = "> ";
        var exception = Record.Exception(() => renderer.RenderInput(testPrompt, _mockInputHandler.Object));
        Assert.Null(exception);
    }

    [Fact]
    public void LockInput_AfterLockInput_DoesNotThrow()
    {
        var renderer = new SplitLayoutRenderer();
        var exception = Record.Exception(() => renderer.LockInput());
        Assert.Null(exception);
    }

    [Fact]
    public void UnlockInput_AfterLockInput_DoesNotThrow()
    {
        var renderer = new SplitLayoutRenderer();
        renderer.LockInput();
        var exception = Record.Exception(() => renderer.UnlockInput());
        Assert.Null(exception);
    }

    [Fact]
    public void RenderOutput_WithEmptyContent_DoesNotThrow()
    {
        var renderer = new SplitLayoutRenderer();
        var exception = Record.Exception(() => renderer.RenderOutput(string.Empty));
        Assert.Null(exception);
    }

    [Fact]
    public void RenderOutput_WithSpecialCharacters_DoesNotThrow()
    {
        var renderer = new SplitLayoutRenderer();
        var testContent = "Special chars: [test] <test> &test;";
        var exception = Record.Exception(() => renderer.RenderOutput(testContent));
        Assert.Null(exception);
    }

    [Fact]
    public void RenderInput_WithEmptyPrompt_DoesNotThrow()
    {
        var renderer = new SplitLayoutRenderer();
        var exception = Record.Exception(() => renderer.RenderInput(string.Empty, _mockInputHandler.Object));
        Assert.Null(exception);
    }

    [Fact]
    public void MultipleRenderOutput_Calls_DoesNotThrow()
    {
        var renderer = new SplitLayoutRenderer();
        for (int i = 0; i < 5; i++)
        {
            var exception = Record.Exception(() => renderer.RenderOutput($"Message {i}"));
            Assert.Null(exception);
        }
    }

    [Fact]
    public void LockInput_MultipleCalls_DoesNotThrow()
    {
        var renderer = new SplitLayoutRenderer();
        renderer.LockInput();
        renderer.LockInput();
        renderer.LockInput();
        var exception = Record.Exception(() => renderer.UnlockInput());
        Assert.Null(exception);
    }
}
