using Microsoft.EntityFrameworkCore;
using Moq;

namespace DirectoryManager.Data.Tests.MockHelpers
{
    public static class MockHelpers
    {
        public static Mock<DbSet<T>> GetQueryableMockDbSet<T>(IEnumerable<T> sourceList)
            where T : class
        {
            var queryable = sourceList.AsQueryable();

            var mockSet = new Mock<DbSet<T>>();
            mockSet.As<IAsyncEnumerable<T>>()
                   .Setup(m => m.GetAsyncEnumerator(CancellationToken.None))
                   .Returns(new AsyncEnumeratorMock<T>(sourceList.GetEnumerator()));

            mockSet.As<IQueryable<T>>()
                   .Setup(m => m.Provider)
                   .Returns(queryable.Provider);

            mockSet.As<IQueryable<T>>()
                   .Setup(m => m.Expression)
                   .Returns(queryable.Expression);

            mockSet.As<IQueryable<T>>()
                   .Setup(m => m.ElementType)
                   .Returns(queryable.ElementType);

            mockSet.As<IQueryable<T>>()
                   .Setup(m => m.GetEnumerator())
                   .Returns(() => queryable.GetEnumerator());

            // Additional setup for other DbSet methods if needed
            mockSet.Setup(m => m.Add(It.IsAny<T>())).Callback<T>(sourceList.ToList().Add);
            mockSet.Setup(m => m.Remove(It.IsAny<T>())).Callback<T>(t => sourceList.ToList().Remove(t));

            return mockSet;
        }

        public static IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> data)
        {
            return new AsyncEnumerableMock<T>(data);
        }

        private class AsyncEnumerableMock<T> : EnumerableQuery<T>, IAsyncEnumerable<T>
        {
            public AsyncEnumerableMock(IEnumerable<T> enumerable)
                : base(enumerable)
            {
            }

            public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            {
                return new AsyncEnumeratorMock<T>(this.AsEnumerable().GetEnumerator());
            }
        }

        private class AsyncEnumeratorMock<T> : IAsyncEnumerator<T>
        {
            private readonly IEnumerator<T> inner;

            public AsyncEnumeratorMock(IEnumerator<T> inner)
            {
                this.inner = inner;
            }

            public T Current => this.inner.Current;

            public ValueTask<bool> MoveNextAsync()
            {
                return new ValueTask<bool>(this.inner.MoveNext());
            }

            public ValueTask DisposeAsync()
            {
                this.inner.Dispose();
                return default;
            }
        }
    }
}