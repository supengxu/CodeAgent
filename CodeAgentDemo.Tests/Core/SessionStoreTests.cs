using CodeAgentDemo.Core;
using CodeAgentDemo.Models;
using FluentAssertions;
using Xunit;

namespace CodeAgentDemo.Tests.Core;

public class SessionStoreTests
{
    [Fact]
    public void Messages_ShouldBeEmptyInitially()
    {
        var store = new SessionStore();

        store.Messages.Should().BeEmpty();
        store.Count.Should().Be(0);
    }

    [Fact]
    public void AddMessage_WithMessage_ShouldAddToHistory()
    {
        var store = new SessionStore();
        var message = ChatMessage.CreateText(ChatRole.User, "Hello");

        store.AddMessage(message);

        store.Count.Should().Be(1);
        store.Messages[0].Should().Be(message);
    }

    [Fact]
    public void AddMessage_WithNullMessage_ShouldThrowArgumentNullException()
    {
        var store = new SessionStore();

        var act = () => store.AddMessage((ChatMessage)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddMessage_WithRoleAndText_ShouldCreateAndAddMessage()
    {
        var store = new SessionStore();

        store.AddMessage(ChatRole.User, "Hello");

        store.Count.Should().Be(1);
        store.Messages[0].Role.Should().Be(ChatRole.User);
        store.Messages[0].Content.Should().HaveCount(1);
    }

    [Fact]
    public void AddMessage_WithRoleAndContentBlocks_ShouldCreateAndAddMessage()
    {
        var store = new SessionStore();
        var blocks = new ContentBlock[]
        {
            new TextBlock("Hello"),
            new ThinkingBlock("Thinking...")
        };

        store.AddMessage(ChatRole.Assistant, blocks);

        store.Count.Should().Be(1);
        store.Messages[0].Role.Should().Be(ChatRole.Assistant);
        store.Messages[0].Content.Should().HaveCount(2);
    }

    [Fact]
    public void Clear_ShouldRemoveAllMessages()
    {
        var store = new SessionStore();
        store.AddMessage(ChatRole.User, "Hello");
        store.AddMessage(ChatRole.Assistant, "Hi");

        store.Clear();

        store.Count.Should().Be(0);
        store.Messages.Should().BeEmpty();
    }

    [Fact]
    public void Messages_ShouldReturnReadOnlyList()
    {
        var store = new SessionStore();
        store.AddMessage(ChatRole.User, "Hello");

        var messages = store.Messages;

        messages.Should().BeAssignableTo<IReadOnlyList<ChatMessage>>();
    }

    [Fact]
    public void MultipleMessages_ShouldMaintainOrder()
    {
        var store = new SessionStore();

        store.AddMessage(ChatRole.User, "First");
        store.AddMessage(ChatRole.Assistant, "Second");
        store.AddMessage(ChatRole.User, "Third");

        store.Count.Should().Be(3);
        ((TextBlock)store.Messages[0].Content.First()).Text.Should().Be("First");
        ((TextBlock)store.Messages[1].Content.First()).Text.Should().Be("Second");
        ((TextBlock)store.Messages[2].Content.First()).Text.Should().Be("Third");
    }
}