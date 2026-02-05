using CodeAgentDemo.Core;
using FluentAssertions;
using Xunit;

namespace CodeAgentDemo.Tests.Core;

public class InputHistoryTests
{
    [Fact]
    public void Add_WithValidInput_ShouldAddToHistory()
    {
        var history = new InputHistory();

        history.Add("hello");

        history.Count.Should().Be(1);
    }

    [Fact]
    public void Add_WithNullOrEmpty_ShouldNotAdd()
    {
        var history = new InputHistory();

        history.Add(null!);
        history.Add("");
        history.Add("   ");

        history.Count.Should().Be(0);
    }

    [Fact]
    public void Add_WithDuplicateOfLastEntry_ShouldNotAdd()
    {
        var history = new InputHistory();

        history.Add("hello");
        history.Add("world");
        history.Add("hello");

        history.Count.Should().Be(2);
    }

    [Fact]
    public void Add_WithMultiLineInput_ShouldStore()
    {
        var history = new InputHistory();
        var multiLine = "line1\nline2\nline3";

        history.Add(multiLine);

        history.Count.Should().Be(1);
    }

    [Fact]
    public void Previous_FromEmptyHistory_ShouldReturnNull()
    {
        var history = new InputHistory();

        var result = history.Previous();

        result.Should().BeNull();
    }

    [Fact]
    public void Previous_WithHistory_ShouldReturnPreviousEntry()
    {
        var history = new InputHistory();
        history.Add("first");
        history.Add("second");

        var result = history.Previous();

        result.Should().Be("second");
    }

    [Fact]
    public void Previous_MultipleTimes_ShouldNavigateBackwards()
    {
        var history = new InputHistory();
        history.Add("first");
        history.Add("second");
        history.Add("third");

        history.Previous().Should().Be("third");
        history.Previous().Should().Be("second");
        history.Previous().Should().Be("first");
    }

    [Fact]
    public void Previous_AtBeginning_ShouldStayAtFirst()
    {
        var history = new InputHistory();
        history.Add("first");
        history.Add("second");

        history.Previous();
        history.Previous();
        var result = history.Previous();

        result.Should().Be("first");
    }

    [Fact]
    public void Next_FromEmptyHistory_ShouldReturnNull()
    {
        var history = new InputHistory();

        var result = history.Next();

        result.Should().BeNull();
    }

    [Fact]
    public void Next_AfterPrevious_ShouldReturnNextEntry()
    {
        var history = new InputHistory();
        history.Add("first");
        history.Add("second");

        history.Previous();
        history.Previous();
        var result = history.Next();

        result.Should().Be("second");
    }

    [Fact]
    public void Next_AtEnd_ShouldReturnNull()
    {
        var history = new InputHistory();
        history.Add("first");
        history.Add("second");

        var result = history.Next();

        result.Should().BeNull();
    }

    [Fact]
    public void Next_AfterPreviousBeyondEnd_ShouldReturnNull()
    {
        var history = new InputHistory();
        history.Add("first");
        history.Add("second");

        history.Previous();
        history.Next();
        history.Next();
        var result = history.Next();

        result.Should().BeNull();
    }

    [Fact]
    public void ResetNavigation_ShouldMoveToEnd()
    {
        var history = new InputHistory();
        history.Add("first");
        history.Add("second");

        history.Previous();
        history.Previous();
        history.ResetNavigation();

        var nextResult = history.Next();
        nextResult.Should().BeNull();
    }

    [Fact]
    public void Clear_ShouldRemoveAllHistory()
    {
        var history = new InputHistory();
        history.Add("first");
        history.Add("second");

        history.Clear();

        history.Count.Should().Be(0);
        history.HasHistory.Should().BeFalse();
    }

    [Fact]
    public void Clear_ThenAdd_ShouldWorkNormally()
    {
        var history = new InputHistory();
        history.Add("first");
        history.Clear();
        history.Add("new");

        history.Count.Should().Be(1);
        history.HasHistory.Should().BeTrue();
    }

    [Fact]
    public void HasHistory_WhenEmpty_ShouldBeFalse()
    {
        var history = new InputHistory();

        history.HasHistory.Should().BeFalse();
    }

    [Fact]
    public void HasHistory_WhenNotEmpty_ShouldBeTrue()
    {
        var history = new InputHistory();
        history.Add("something");

        history.HasHistory.Should().BeTrue();
    }

    [Fact]
    public void NavigationCycle_ShouldWorkCorrectly()
    {
        var history = new InputHistory();
        history.Add("cmd1");
        history.Add("cmd2");
        history.Add("cmd3");

        // Go back through history
        history.Previous().Should().Be("cmd3");
        history.Previous().Should().Be("cmd2");
        history.Previous().Should().Be("cmd1");

        // Go forward through history
        history.Next().Should().Be("cmd2");
        history.Next().Should().Be("cmd3");
        history.Next().Should().BeNull();
    }
}