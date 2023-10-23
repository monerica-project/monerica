using DirectoryManager.Data.Constants;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class ContentSnippetRepository : IContentSnippetRepository
    {
        public ContentSnippetRepository(IApplicationDbContext context)
        {
            this.Context = context;
        }

        public IApplicationDbContext Context { get; private set; }

        public ContentSnippet Create(ContentSnippet model)
        {
            try
            {
                this.Context.ContentSnippets.Add(model);
                this.Context.SaveChanges();

                return model;
            }
            catch (Exception ex)
            {
                throw new Exception(StringConstants.DBErrorMessage, ex.InnerException);
            }
        }

        public void Dispose()
        {
            this.Context.Dispose();
        }

        public ContentSnippet Get(int contentSnippetId)
        {
            try
            {
                var contentSnippet = this.Context.ContentSnippets.Find(contentSnippetId);

                if (contentSnippet == null)
                {
                    throw new ArgumentNullException($"No ContentSnippet found with ID {contentSnippetId}");
                }

                return contentSnippet;
            }
            catch (Exception ex)
            {
                throw new Exception(StringConstants.DBErrorMessage, ex.InnerException);
            }
        }

        public ContentSnippet Get(SiteConfigSetting snippetType)
        {
            try
            {
                var contentSnippet = this.Context.ContentSnippets.FirstOrDefault(x => x.SnippetType == snippetType);

                if (contentSnippet == null)
                {
                    throw new ArgumentNullException($"No ContentSnippet found with {snippetType}");
                }

                return contentSnippet;
            }
            catch (Exception ex)
            {
                throw new Exception(StringConstants.DBErrorMessage, ex.InnerException);
            }
        }

        public bool Update(ContentSnippet model)
        {
            try
            {
                this.Context.ContentSnippets.Update(model);
                this.Context.SaveChanges();

                return true;
            }
            catch (Exception ex)
            {
                throw new Exception(StringConstants.DBErrorMessage, ex.InnerException);
            }
        }

        public bool Delete(int contentSnippetId)
        {
            try
            {
                var entry = this.Context.ContentSnippets.Find(contentSnippetId) ??
                    throw new ArgumentNullException($"No ContentSnippet found with ID {contentSnippetId}");
                this.Context.ContentSnippets.Remove(entry);
                this.Context.SaveChanges();

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public IList<ContentSnippet> GetAll()
        {
            try
            {
                return this.Context.ContentSnippets.ToList();
            }
            catch (Exception ex)
            {
                throw new Exception(StringConstants.DBErrorMessage, ex.InnerException);
            }
        }
    }
}