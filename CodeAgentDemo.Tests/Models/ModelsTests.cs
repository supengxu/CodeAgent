using System.Text.Json;
using CodeAgentDemo.Models;
using FluentAssertions;
using Xunit;

namespace CodeAgentDemo.Tests.Models;

public class ChatRoleTests
{
    [Fact]
    public void ChatRole_ShouldHaveFourValues()
    {
        var values = Enum.GetValues<ChatRole>();
        values.Should().HaveCount(4);
        values.Should().Contain(ChatRole.System);
        values.Should().Contain(ChatRole.User);
        values.Should().Contain(ChatRole.Assistant);
        values.Should().Contain(ChatRole.Tool);
    }
}

public class ChatMessageTests
{
    [Fact]
    public void CreateText_ShouldCreateMessageWithSingleTextBlock()
    {
        var message = ChatMessage.CreateText(ChatRole.User, "Hello");

        message.Role.Should().Be(ChatRole.User);
        message.Content.Should().HaveCount(1);
        message.Content.First().Should().BeOfType<TextBlock>();
        ((TextBlock)message.Content.First()).Text.Should().Be("Hello");
    }

    [Fact]
    public void Constructor_ShouldAcceptContentBlocks()
    {
        var blocks = new ContentBlock[]
        {
            new TextBlock("Hello"),
            new ThinkingBlock("Thinking...")
        };

        var message = new ChatMessage(ChatRole.Assistant, blocks);

        message.Role.Should().Be(ChatRole.Assistant);
        message.Content.Should().HaveCount(2);
    }
}

public class ChatOptionsTests
{
    [Fact]
    public void DefaultValues_ShouldBeSet()
    {
        var options = new ChatOptions();

        options.MaxTokens.Should().Be(4096);
        options.EnableThinking.Should().BeFalse();
        options.ThinkingBudgetTokens.Should().Be(0);
        options.SystemPrompt.Should().BeNull();
        options.Tools.Should().BeNull();
    }

    [Fact]
    public void Properties_ShouldBeSettable()
    {
        var schema = JsonDocument.Parse("{}").RootElement;
        var tools = new[] { new ToolDefinition("test", "desc", schema) };

        var options = new ChatOptions
        {
            SystemPrompt = "You are helpful",
            MaxTokens = 2048,
            EnableThinking = true,
            ThinkingBudgetTokens = 10000,
            Tools = tools
        };

        options.SystemPrompt.Should().Be("You are helpful");
        options.MaxTokens.Should().Be(2048);
        options.EnableThinking.Should().BeTrue();
        options.ThinkingBudgetTokens.Should().Be(10000);
        options.Tools.Should().HaveCount(1);
    }
}

public class ToolDefinitionTests
{
    [Fact]
    public void Constructor_ShouldSetProperties()
    {
        var schema = JsonDocument.Parse("{\"type\":\"object\"}").RootElement;

        var definition = new ToolDefinition("bash", "Execute bash", schema);

        definition.Name.Should().Be("bash");
        definition.Description.Should().Be("Execute bash");
        definition.InputSchema.ToString().Should().Contain("object");
    }
}

public class ContentBlockTests
{
    [Fact]
    public void TextBlock_ShouldSetText()
    {
        var block = new TextBlock("Hello World");

        block.Text.Should().Be("Hello World");
    }

    [Fact]
    public void ThinkingBlock_ShouldSetThinking()
    {
        var block = new ThinkingBlock("Deep thought...");

        block.Thinking.Should().Be("Deep thought...");
    }

    [Fact]
    public void ToolUseBlock_ShouldSetProperties()
    {
        var input = JsonDocument.Parse("{\"arg\":\"value\"}").RootElement;

        var block = new ToolUseBlock("tool-123", "bash", input);

        block.Id.Should().Be("tool-123");
        block.Name.Should().Be("bash");
        block.Input.ToString().Should().Contain("arg");
    }

    [Fact]
    public void ToolResultBlock_ShouldSetProperties()
    {
        var block = new ToolResultBlock("tool-123", "Result output", false);

        block.ToolUseId.Should().Be("tool-123");
        block.Content.Should().Be("Result output");
        block.IsError.Should().BeFalse();
    }

    [Fact]
    public void ToolResultBlock_WithError_ShouldSetIsError()
    {
        var block = new ToolResultBlock("tool-456", "Error occurred", true);

        block.IsError.Should().BeTrue();
    }
}

public class StreamChunkTests
{
    [Fact]
    public void Constructor_ShouldSetAllProperties()
    {
        var toolDelta = new ToolCallDelta("id-1", "bash", "{\"cmd\":");

        var chunk = new StreamChunk("text delta", "thinking delta", toolDelta);

        chunk.TextDelta.Should().Be("text delta");
        chunk.ThinkingDelta.Should().Be("thinking delta");
        chunk.ToolCallDelta.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNulls_ShouldAcceptNulls()
    {
        var chunk = new StreamChunk(null, null, null);

        chunk.TextDelta.Should().BeNull();
        chunk.ThinkingDelta.Should().BeNull();
        chunk.ToolCallDelta.Should().BeNull();
    }
}

public class ToolCallDeltaTests
{
    [Fact]
    public void Constructor_ShouldSetProperties()
    {
        var delta = new ToolCallDelta("tool-1", "bash", "{\"cmd\":\"ls\"}");

        delta.Id.Should().Be("tool-1");
        delta.Name.Should().Be("bash");
        delta.ArgumentsDelta.Should().Be("{\"cmd\":\"ls\"}");
    }

    [Fact]
    public void Constructor_WithNulls_ShouldAcceptNulls()
    {
        var delta = new ToolCallDelta(null, null, null);

        delta.Id.Should().BeNull();
        delta.Name.Should().BeNull();
        delta.ArgumentsDelta.Should().BeNull();
    }
}

public class StreamResponseTests
{
    [Fact]
    public void Constructor_ShouldSetProperties()
    {
        var content = new ContentBlock[] { new TextBlock("Hello") };
        var toolCalls = new List<ToolCall>
        {
            new ToolCall("id-1", "bash", JsonDocument.Parse("{}").RootElement)
        };

        var response = new StreamResponse(content, toolCalls, "end_turn");

        response.Content.Should().HaveCount(1);
        response.ToolCalls.Should().HaveCount(1);
        response.StopReason.Should().Be("end_turn");
    }
}

public class ToolCallTests
{
    [Fact]
    public void Constructor_ShouldSetProperties()
    {
        var args = JsonDocument.Parse("{\"command\":\"ls\"}").RootElement;

        var toolCall = new ToolCall("call-123", "bash", args);

        toolCall.Id.Should().Be("call-123");
        toolCall.Name.Should().Be("bash");
        toolCall.Arguments.ToString().Should().Contain("command");
    }
}