using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models.ContentSnippet
{
    public class ContentSnippetDisplayModel
    {
        public SiteConfigSetting SnippetType { get; set; }

        public string Content { get; set; } = string.Empty;
    }
}