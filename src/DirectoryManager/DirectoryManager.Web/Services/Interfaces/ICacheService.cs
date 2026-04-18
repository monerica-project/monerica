using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Services.Interfaces
{
    public interface ICacheService
    {
 
        void ClearSnippetCache(SiteConfigSetting snippetType);

        Task<string> GetSnippetAsync(SiteConfigSetting snippetType);
    }
}