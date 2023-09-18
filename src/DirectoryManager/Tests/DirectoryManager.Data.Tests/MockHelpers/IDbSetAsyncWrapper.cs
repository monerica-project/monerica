
namespace DirectoryManager.Data.Tests.MockHelpers
{
    public interface IDbSetAsyncWrapper<T> where T : class
    {
        Task<List<T>> ToListAsync(CancellationToken cancellationToken = default);
        // ... any other async methods you might want to use ...
    }

}
