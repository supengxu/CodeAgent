using CodeAgentDemo.Core;
using CodeAgentDemo.Models;
using Moq;

namespace CodeAgentDemo.Tests.Fixtures;

/// <summary>
/// Test fixtures for IInputHandler testing
/// </summary>
public static class InputHandlerFixture
{
    /// <summary>
    /// Creates a Mock&lt;IInputHandler&gt; with basic setup
    /// </summary>
    public static Mock<IInputHandler> CreateMock()
    {
        return new Mock<IInputHandler>();
    }

    /// <summary>
    /// Creates a Mock&lt;IInputHandler&gt; that returns the specified InputResult
    /// </summary>
    public static Mock<IInputHandler> CreateMockReturning(InputResult result)
    {
        var mock = new Mock<IInputHandler>();
        mock.Setup(h => h.ReadInputAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return mock;
    }

    /// <summary>
    /// Creates a Mock&lt;IInputHandler&gt; that returns submitted input with the given text
    /// </summary>
    public static Mock<IInputHandler> CreateMockWithSubmittedText(string text)
    {
        return CreateMockReturning(InputResult.Submitted(text));
    }

    /// <summary>
    /// Creates a Mock&lt;IInputHandler&gt; that returns cancelled input
    /// </summary>
    public static Mock<IInputHandler> CreateMockWithCancelled()
    {
        return CreateMockReturning(InputResult.Cancelled());
    }

    /// <summary>
    /// Creates a Mock&lt;IInputHandler&gt; that returns empty input
    /// </summary>
    public static Mock<IInputHandler> CreateMockWithEmpty()
    {
        return CreateMockReturning(InputResult.Empty());
    }

    /// <summary>
    /// Creates a Mock&lt;IInputHandler&gt; that returns a sequence of results
    /// </summary>
    public static Mock<IInputHandler> CreateMockWithSequence(params InputResult[] results)
    {
        var mock = new Mock<IInputHandler>();
        var index = 0;
        mock.Setup(h => h.ReadInputAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => results[index++ % results.Length]);
        return mock;
    }

    /// <summary>
    /// Creates a Mock&lt;IInputHandler&gt; that throws OperationCanceledException
    /// </summary>
    public static Mock<IInputHandler> CreateMockThrowingCancellation()
    {
        var mock = new Mock<IInputHandler>();
        mock.Setup(h => h.ReadInputAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        return mock;
    }

    /// <summary>
    /// Test data generator for InputResult
    /// </summary>
    public static class TestData
    {
        /// <summary>
        /// Generates a random submitted InputResult with text of specified length
        /// </summary>
        public static InputResult GenerateSubmitted(int textLength = 10)
        {
            var text = Guid.NewGuid().ToString("N")[..textLength];
            return InputResult.Submitted(text);
        }

        /// <summary>
        /// Generates a random non-empty submitted InputResult
        /// </summary>
        public static InputResult GenerateSubmittedNonEmpty()
        {
            return InputResult.Submitted("Test input " + Guid.NewGuid().ToString("N")[..8]);
        }

        /// <summary>
        /// Returns cancelled InputResult
        /// </summary>
        public static InputResult Cancelled() => InputResult.Cancelled();

        /// <summary>
        /// Returns empty InputResult
        /// </summary>
        public static InputResult Empty() => InputResult.Empty();

        /// <summary>
        /// Generates various InputResult states for parameterized tests
        /// </summary>
        public static IEnumerable<object[]> GenerateAllStates()
        {
            yield return new object[] { InputResult.Submitted("test") };
            yield return new object[] { InputResult.Cancelled() };
            yield return new object[] { InputResult.Empty() };
        }

        /// <summary>
        /// Generates various submitted InputResults with different text patterns
        /// </summary>
        public static IEnumerable<object[]> GenerateSubmittedVariations()
        {
            yield return new object[] { InputResult.Submitted("") };
            yield return new object[] { InputResult.Submitted("   ") };
            yield return new object[] { InputResult.Submitted("a") };
            yield return new object[] { InputResult.Submitted("Hello World") };
            yield return new object[] { InputResult.Submitted(new string('x', 1000)) };
        }
    }
}

/// <summary>
/// xUnit collection fixture for InputHandler tests
/// </summary>
public class InputHandlerCollectionFixture : IDisposable
{
    public Mock<IInputHandler> MockInputHandler { get; } = InputHandlerFixture.CreateMock();

    public void Dispose()
    {
        MockInputHandler.Reset();
    }
}

/// <summary>
/// Collection definition for InputHandler tests
/// </summary>
[CollectionDefinition("InputHandlerTests")]
public class InputHandlerTestCollection : ICollectionFixture<InputHandlerCollectionFixture>
{
    // This class serves as the collection definition
}