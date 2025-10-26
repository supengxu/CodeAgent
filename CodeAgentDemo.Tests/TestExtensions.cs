using System.Runtime.CompilerServices;

namespace CodeAgentDemo.Tests;

/// <summary>
/// Extension methods for testing purposes.
/// </summary>
internal static class TestExtensions
{
    /// <summary>
    /// Converts an IEnumerable to IAsyncEnumerable for testing.
    /// </summary>
    public static IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source)
    {
        return new AsyncEnumerableWrapper<T>(source);
    }

    private class AsyncEnumerableWrapper<T> : IAsyncEnumerable<T>
    {
        private readonly IEnumerable<T> _source;

        public AsyncEnumerableWrapper(IEnumerable<T> source)
        {
            _source = source;
        }

        public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            foreach (var item in _source)
            {
                await Task.Yield();
                yield return item;
            }
        }
    }
}