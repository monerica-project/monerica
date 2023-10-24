using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Services.Interfaces
{
    public interface ICacheService
    {
        string GetSnippet(SiteConfigSetting snippetType);

        void ClearSnippetCache(SiteConfigSetting snippetType);
    }
}