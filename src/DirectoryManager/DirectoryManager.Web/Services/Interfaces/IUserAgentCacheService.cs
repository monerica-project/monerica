namespace DirectoryManager.Web.Services.Interfaces
{
    public interface IUserAgentCacheService
    {
        bool IsUserAgentExcluded(string userAgent);
    }
}