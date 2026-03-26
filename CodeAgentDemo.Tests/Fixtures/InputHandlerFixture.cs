using CodeAgentDemo.Core;
using CodeAgentDemo.Models;
using Moq;

namespace CodeAgentDemo.Tests.Fixtures;

/// <summary>
/// IInputHandler 测试的测试夹具
/// </summary>
public static class InputHandlerFixture
{
    /// <summary>
    /// 创建带有基本设置的 Mock&lt;IInputHandler&gt;
    /// </summary>
    public static Mock<IInputHandler> CreateMock()
    {
        return new Mock<IInputHandler>();
    }

    /// <summary>
    /// 创建返回指定 InputResult 的 Mock&lt;IInputHandler&gt;
    /// </summary>
    public static Mock<IInputHandler> CreateMockReturning(InputResult result)
    {
        var mock = new Mock<IInputHandler>();
        mock.Setup(h => h.ReadInputAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return mock;
    }

    /// <summary>
    /// 创建返回带有给定文本的已提交输入的 Mock&lt;IInputHandler&gt;
    /// </summary>
    public static Mock<IInputHandler> CreateMockWithSubmittedText(string text)
    {
        return CreateMockReturning(InputResult.Submitted(text));
    }

    /// <summary>
    /// 创建返回已取消输入的 Mock&lt;IInputHandler&gt;
    /// </summary>
    public static Mock<IInputHandler> CreateMockWithCancelled()
    {
        return CreateMockReturning(InputResult.Cancelled());
    }

    /// <summary>
    /// 创建返回空输入的 Mock&lt;IInputHandler&gt;
    /// </summary>
    public static Mock<IInputHandler> CreateMockWithEmpty()
    {
        return CreateMockReturning(InputResult.Empty());
    }

    /// <summary>
    /// 创建返回结果序列的 Mock&lt;IInputHandler&gt;
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
    /// 创建抛出 OperationCanceledException 的 Mock&lt;IInputHandler&gt;
    /// </summary>
    public static Mock<IInputHandler> CreateMockThrowingCancellation()
    {
        var mock = new Mock<IInputHandler>();
        mock.Setup(h => h.ReadInputAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        return mock;
    }

    /// <summary>
    /// InputResult 的测试数据生成器
    /// </summary>
    public static class TestData
    {
        /// <summary>
        /// 生成指定长度的随机已提交 InputResult
        /// </summary>
        public static InputResult GenerateSubmitted(int textLength = 10)
        {
            var text = Guid.NewGuid().ToString("N")[..textLength];
            return InputResult.Submitted(text);
        }

        /// <summary>
        /// 生成随机非空已提交 InputResult
        /// </summary>
        public static InputResult GenerateSubmittedNonEmpty()
        {
            return InputResult.Submitted("Test input " + Guid.NewGuid().ToString("N")[..8]);
        }

        /// <summary>
        /// 返回已取消的 InputResult
        /// </summary>
        public static InputResult Cancelled() => InputResult.Cancelled();

        /// <summary>
        /// 返回空 InputResult
        /// </summary>
        public static InputResult Empty() => InputResult.Empty();

        /// <summary>
        /// 生成各种 InputResult 状态用于参数化测试
        /// </summary>
        public static IEnumerable<object[]> GenerateAllStates()
        {
            yield return new object[] { InputResult.Submitted("test") };
            yield return new object[] { InputResult.Cancelled() };
            yield return new object[] { InputResult.Empty() };
        }

        /// <summary>
        /// 生成各种不同文本模式的已提交 InputResult
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
/// InputHandler 测试的 xUnit 集合夹具
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
/// InputHandler 测试的集合定义
/// </summary>
[CollectionDefinition("InputHandlerTests")]
public class InputHandlerTestCollection : ICollectionFixture<InputHandlerCollectionFixture>
{
    // 此类作为集合定义
}