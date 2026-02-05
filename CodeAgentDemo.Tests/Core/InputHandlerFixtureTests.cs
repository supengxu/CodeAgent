using CodeAgentDemo.Core;
using CodeAgentDemo.Models;
using CodeAgentDemo.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace CodeAgentDemo.Tests.Core;

public class InputHandlerFixtureTests
{
    [Fact]
    public void CreateMock_ShouldReturnValidMock()
    {
        var mock = InputHandlerFixture.CreateMock();
        mock.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateMockWithSubmittedText_ShouldReturnSubmittedResult()
    {
        var mock = InputHandlerFixture.CreateMockWithSubmittedText("test input");
        var handler = mock.Object;

        var result = await handler.ReadInputAsync(default);

        result.State.Should().Be(InputState.Submitted);
        result.Text.Should().Be("test input");
    }

    [Fact]
    public async Task CreateMockWithCancelled_ShouldReturnCancelledResult()
    {
        var mock = InputHandlerFixture.CreateMockWithCancelled();
        var handler = mock.Object;

        var result = await handler.ReadInputAsync(default);

        result.State.Should().Be(InputState.Cancelled);
    }

    [Fact]
    public async Task CreateMockWithEmpty_ShouldReturnEmptyResult()
    {
        var mock = InputHandlerFixture.CreateMockWithEmpty();
        var handler = mock.Object;

        var result = await handler.ReadInputAsync(default);

        result.State.Should().Be(InputState.Empty);
    }

    [Fact]
    public void TestData_GenerateSubmitted_ShouldReturnValidResult()
    {
        var result = InputHandlerFixture.TestData.GenerateSubmitted(5);

        result.State.Should().Be(InputState.Submitted);
        result.Text.Should().HaveLength(5);
    }

    [Theory]
    [MemberData(nameof(InputData))]
    public async Task CreateMockReturning_ShouldReturnSpecifiedResult(InputResult expected)
    {
        var mock = InputHandlerFixture.CreateMockReturning(expected);
        var handler = mock.Object;

        var result = await handler.ReadInputAsync(default);

        result.Should().Be(expected);
    }

    public static IEnumerable<object[]> InputData()
    {
        yield return new object[] { InputResult.Submitted("test") };
        yield return new object[] { InputResult.Cancelled() };
        yield return new object[] { InputResult.Empty() };
    }
}