using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IContentSnippetRepository : IDisposable
    {
        IApplicationDbContext Context { get; }

        ContentSnippet Create(ContentSnippet model);

        bool Update(ContentSnippet model);

        ContentSnippet? Get(int contentSnippetId);

        ContentSnippet Get(SiteConfigSetting snippetType);

        bool Delete(int tagId);

        IList<ContentSnippet> GetAll();
    }
}