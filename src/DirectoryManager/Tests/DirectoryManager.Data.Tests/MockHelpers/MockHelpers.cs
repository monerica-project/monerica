namespace DirectoryManager.Data.Tests.MockHelpers
{
    public static class MockHelpers
    {
        public static IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> data)
        {
            return new AsyncEnumerableMock<T>(data);
        }

        private class AsyncEnumerableMock<T> : EnumerableQuery<T>, IAsyncEnumerable<T>
        {
            public AsyncEnumerableMock(IEnumerable<T> enumerable) : base(enumerable) { }

            public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            {
                return new AsyncEnumeratorMock<T>(this.AsEnumerable().GetEnumerator());
            }
        }

        private class AsyncEnumeratorMock<T> : IAsyncEnumerator<T>
        {
            private readonly IEnumerator<T> _inner;

            public AsyncEnumeratorMock(IEnumerator<T> inner)
            {
                _inner = inner;
            }

            public ValueTask<bool> MoveNextAsync()
            {
                return new ValueTask<bool>(_inner.MoveNext());
            }

            public T Current => _inner.Current;

            public ValueTask DisposeAsync()
            {
                _inner.Dispose();
                return new ValueTask();
            }
        }
    }
}