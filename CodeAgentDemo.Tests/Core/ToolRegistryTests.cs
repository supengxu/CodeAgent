using System.Text.Json;
using CodeAgentDemo.Core;
using CodeAgentDemo.Tools;
using FluentAssertions;
using Moq;
using Xunit;

namespace CodeAgentDemo.Tests.Core;

public class ToolRegistryTests
{
    [Fact]
    public void Count_ShouldBeZeroInitially()
    {
        var registry = new ToolRegistry();

        registry.Count.Should().Be(0);
    }

    [Fact]
    public void Register_ShouldAddTool()
    {
        var registry = new ToolRegistry();
        var tool = CreateMockTool("bash", "Execute bash commands");

        registry.Register(tool.Object);

        registry.Count.Should().Be(1);
        registry.GetTool("bash").Should().NotBeNull();
    }

    [Fact]
    public void Register_WithNullTool_ShouldThrowArgumentNullException()
    {
        var registry = new ToolRegistry();

        var act = () => registry.Register(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Register_WithDuplicateName_ShouldThrowArgumentException()
    {
        var registry = new ToolRegistry();
        var tool1 = CreateMockTool("bash", "First bash");
        var tool2 = CreateMockTool("bash", "Second bash");

        registry.Register(tool1.Object);

        var act = () => registry.Register(tool2.Object);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*bash*already registered*");
    }

    [Fact]
    public void GetTool_WhenExists_ShouldReturnTool()
    {
        var registry = new ToolRegistry();
        var tool = CreateMockTool("bash", "Execute bash");
        registry.Register(tool.Object);

        var result = registry.GetTool("bash");

        result.Should().NotBeNull();
        result!.Name.Should().Be("bash");
    }

    [Fact]
    public void GetTool_WhenNotExists_ShouldReturnNull()
    {
        var registry = new ToolRegistry();

        var result = registry.GetTool("nonexistent");

        result.Should().BeNull();
    }

    [Fact]
    public void HasTool_WhenExists_ShouldReturnTrue()
    {
        var registry = new ToolRegistry();
        var tool = CreateMockTool("bash", "Execute bash");
        registry.Register(tool.Object);

        registry.HasTool("bash").Should().BeTrue();
    }

    [Fact]
    public void HasTool_WhenNotExists_ShouldReturnFalse()
    {
        var registry = new ToolRegistry();

        registry.HasTool("nonexistent").Should().BeFalse();
    }

    [Fact]
    public void GetAllTools_ShouldReturnAllTools()
    {
        var registry = new ToolRegistry();
        var bashTool = CreateMockTool("bash", "Execute bash");
        var fileTool = CreateMockTool("file", "Read files");

        registry.Register(bashTool.Object);
        registry.Register(fileTool.Object);

        var tools = registry.GetAllTools().ToList();

        tools.Should().HaveCount(2);
        tools.Select(t => t.Name).Should().Contain(new[] { "bash", "file" });
    }

    [Fact]
    public void GetAllTools_WhenEmpty_ShouldReturnEmpty()
    {
        var registry = new ToolRegistry();

        var tools = registry.GetAllTools();

        tools.Should().BeEmpty();
    }

    private static Mock<ITool> CreateMockTool(string name, string description)
    {
        var mock = new Mock<ITool>();
        mock.SetupGet(t => t.Name).Returns(name);
        mock.SetupGet(t => t.Description).Returns(description);
        mock.SetupGet(t => t.InputSchema).Returns(JsonDocument.Parse("{}").RootElement);
        mock.Setup(t => t.ExecuteAsync(It.IsAny<JsonElement>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResult(true, "Success"));
        return mock;
    }
}