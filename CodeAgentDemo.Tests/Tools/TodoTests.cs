using System.Text.Json;
using CodeAgentDemo.Core;
using CodeAgentDemo.Models;
using CodeAgentDemo.Tools;
using FluentAssertions;
using Xunit;

namespace CodeAgentDemo.Tests.Tools;

public class TodoToolTests
{
    private readonly TodoManager _manager;
    private readonly TodoTool _tool;

    public TodoToolTests()
    {
        _manager = new TodoManager();
        _tool = new TodoTool(_manager);
    }

    [Fact]
    public void Name_ShouldBeTodo()
    {
        _tool.Name.Should().Be("todo");
    }

    [Fact]
    public void Description_ShouldBeSet()
    {
        _tool.Description.Should().Contain("任务管理");
    }

    [Fact]
    public void InputSchema_ShouldBeValidJson()
    {
        var schema = _tool.InputSchema;

        schema.ValueKind.Should().Be(JsonValueKind.Object);
        schema.TryGetProperty("properties", out _).Should().BeTrue();
    }

    [Fact]
    public void RequiresConfirmation_ShouldReturnFalse()
    {
        var args = JsonDocument.Parse("{\"action\":\"list\"}").RootElement;

        _tool.RequiresConfirmation(args).Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithListAction_ShouldReturnTodos()
    {
        _manager.Update(new[]
        {
            new TodoItem { Id = "1", Text = "Test task", Status = TodoStatus.Pending }
        });

        var args = JsonDocument.Parse("{\"action\":\"list\"}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeTrue();
        result.Output.Should().Contain("Test task");
    }

    [Fact]
    public async Task ExecuteAsync_WithAddAction_ShouldAddTodo()
    {
        var args = JsonDocument.Parse("{\"action\":\"add\",\"text\":\"New task\",\"priority\":\"high\"}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeTrue();
        result.Output.Should().Contain("New task");
        _manager.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithAddAction_MissingText_ShouldReturnFailure()
    {
        var args = JsonDocument.Parse("{\"action\":\"add\"}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeFalse();
        result.Output.Should().Contain("required");
    }

    [Fact]
    public async Task ExecuteAsync_WithUpdateAction_ShouldUpdateTodo()
    {
        _manager.Update(new[]
        {
            new TodoItem { Id = "test123", Text = "Original text", Status = TodoStatus.Pending }
        });

        var args = JsonDocument.Parse("{\"action\":\"update\",\"id\":\"test123\",\"text\":\"Updated text\",\"status\":\"in_progress\"}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeTrue();
        result.Output.Should().Contain("Updated text");
    }

    [Fact]
    public async Task ExecuteAsync_WithUpdateAction_MissingId_ShouldReturnFailure()
    {
        var args = JsonDocument.Parse("{\"action\":\"update\",\"text\":\"New text\"}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeFalse();
        result.Output.Should().Contain("id is required");
    }

    [Fact]
    public async Task ExecuteAsync_WithRemoveAction_ShouldRemoveTodo()
    {
        _manager.Update(new[]
        {
            new TodoItem { Id = "to_delete", Text = "Delete me", Status = TodoStatus.Pending }
        });

        var args = JsonDocument.Parse("{\"action\":\"remove\",\"id\":\"to_delete\"}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeTrue();
        _manager.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WithClearAction_ShouldClearAllTodos()
    {
        _manager.Update(new[]
        {
            new TodoItem { Id = "1", Text = "Task 1", Status = TodoStatus.Pending },
            new TodoItem { Id = "2", Text = "Task 2", Status = TodoStatus.Pending }
        });

        var args = JsonDocument.Parse("{\"action\":\"clear\"}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeTrue();
        _manager.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyList_ShouldReturnDefaultMessage()
    {
        var args = JsonDocument.Parse("{\"action\":\"list\"}").RootElement;

        var result = await _tool.ExecuteAsync(args);

        result.Success.Should().BeTrue();
        result.Output.Should().Contain("No todo");
    }

    [Fact]
    public void Constructor_WithNullManager_ShouldThrow()
    {
        var act = () => new TodoTool(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}

public class TodoManagerTests
{
    [Fact]
    public void Update_WithEmptyList_ShouldClearItems()
    {
        var manager = new TodoManager();

        manager.Update(Array.Empty<TodoItem>());

        manager.Items.Should().BeEmpty();
    }

    [Fact]
    public void Update_WithItems_ShouldStoreItems()
    {
        var manager = new TodoManager();

        var result = manager.Update(new[]
        {
            new TodoItem { Id = "1", Text = "Task 1", Status = TodoStatus.Pending }
        });

        manager.Items.Should().HaveCount(1);
        manager.Items[0].Text.Should().Be("Task 1");
    }

    [Fact]
    public void Update_WithMultipleInProgress_ShouldThrow()
    {
        var manager = new TodoManager();

        var act = () => manager.Update(new[]
        {
            new TodoItem { Id = "1", Text = "Task 1", Status = TodoStatus.InProgress },
            new TodoItem { Id = "2", Text = "Task 2", Status = TodoStatus.InProgress }
        });

        act.Should().Throw<ArgumentException>()
            .WithMessage("*in_progress*");
    }

    [Fact]
    public void Update_ShouldResetRoundCounter()
    {
        var manager = new TodoManager();
        manager.IncrementRound();
        manager.IncrementRound();

        manager.Update(new[] { new TodoItem { Id = "1", Text = "Task", Status = TodoStatus.Pending } });

        manager.RoundsSinceLastUpdate.Should().Be(0);
    }

    [Fact]
    public void IncrementRound_ShouldIncreaseCounter()
    {
        var manager = new TodoManager();

        manager.IncrementRound();
        manager.IncrementRound();

        manager.RoundsSinceLastUpdate.Should().Be(2);
    }

    [Fact]
    public void ResetRound_ShouldResetCounter()
    {
        var manager = new TodoManager();
        manager.IncrementRound();
        manager.IncrementRound();

        manager.ResetRound();

        manager.RoundsSinceLastUpdate.Should().Be(0);
    }

    [Fact]
    public void ShouldNag_WithNoItems_ShouldReturnFalse()
    {
        var manager = new TodoManager();

        manager.ShouldNag(0, 3).Should().BeFalse();
    }

    [Fact]
    public void ShouldNag_WithItemsAndFewRounds_ShouldReturnFalse()
    {
        var manager = new TodoManager();
        manager.Update(new[] { new TodoItem { Id = "1", Text = "Task", Status = TodoStatus.Pending } });

        manager.ShouldNag(2, 3).Should().BeFalse();
    }

    [Fact]
    public void ShouldNag_WithItemsAndEnoughRounds_ShouldReturnTrue()
    {
        var manager = new TodoManager();
        manager.Update(new[] { new TodoItem { Id = "1", Text = "Task", Status = TodoStatus.Pending } });

        manager.ShouldNag(3, 3).Should().BeTrue();
    }

    [Fact]
    public void CurrentInProgressItem_WithNoInProgress_ShouldReturnNull()
    {
        var manager = new TodoManager();

        manager.CurrentInProgressItem.Should().BeNull();
    }

    [Fact]
    public void CurrentInProgressItem_WithInProgress_ShouldReturnItem()
    {
        var manager = new TodoManager();
        manager.Update(new[]
        {
            new TodoItem { Id = "1", Text = "Task 1", Status = TodoStatus.Pending },
            new TodoItem { Id = "2", Text = "Task 2", Status = TodoStatus.InProgress }
        });

        manager.CurrentInProgressItem.Should().NotBeNull();
        manager.CurrentInProgressItem!.Text.Should().Be("Task 2");
    }

    [Fact]
    public void Render_WithEmptyList_ShouldReturnDefaultMessage()
    {
        var manager = new TodoManager();

        var result = manager.Render();

        result.Should().Contain("No todo");
    }

    [Fact]
    public void Render_WithItems_ShouldFormatCorrectly()
    {
        var manager = new TodoManager();
        manager.Update(new[]
        {
            new TodoItem { Id = "1", Text = "Task 1", Status = TodoStatus.Pending },
            new TodoItem { Id = "2", Text = "Task 2", Status = TodoStatus.InProgress },
            new TodoItem { Id = "3", Text = "Task 3", Status = TodoStatus.Completed }
        });

        var result = manager.Render();

        result.Should().Contain("[ ]");
        result.Should().Contain("[>]");
        result.Should().Contain("[x]");
    }

    [Fact]
    public void ToJson_ShouldReturnValidJson()
    {
        var manager = new TodoManager();
        manager.Update(new[] { new TodoItem { Id = "1", Text = "Task", Status = TodoStatus.Pending } });

        var result = manager.ToJson();

        result.Should().Contain("Task");
        result.Should().Contain("status");
    }
}

public class TodoItemTests
{
    [Fact]
    public void Constructor_WithRequiredProperties_ShouldSetProperties()
    {
        var item = new TodoItem { Id = "test", Text = "Test task" };

        item.Id.Should().Be("test");
        item.Text.Should().Be("Test task");
        item.Status.Should().Be(TodoStatus.Pending);
        item.Priority.Should().Be("medium");
    }

    [Fact]
    public void Constructor_DefaultStatus_ShouldBePending()
    {
        var item = new TodoItem { Id = "1", Text = "Task" };

        item.Status.Should().Be(TodoStatus.Pending);
    }

    [Fact]
    public void Constructor_DefaultPriority_ShouldBeMedium()
    {
        var item = new TodoItem { Id = "1", Text = "Task" };

        item.Priority.Should().Be("medium");
    }
}