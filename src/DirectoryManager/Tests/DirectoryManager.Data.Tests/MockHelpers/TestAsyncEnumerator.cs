namespace DirectoryManager.Data.Tests.MockHelpers
{
    internal class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
    {
        private readonly IEnumerator<T> inner;

        public TestAsyncEnumerator(IEnumerator<T> inner)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public T Current => this.inner.Current;

        public async ValueTask<bool> MoveNextAsync()
        {
            await Task.Delay(1); // Simulate an asynchronous operation
            return this.inner.MoveNext();
        }

        public ValueTask DisposeAsync()
        {
            this.inner.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
