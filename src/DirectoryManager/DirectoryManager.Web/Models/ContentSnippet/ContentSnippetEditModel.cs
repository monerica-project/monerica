using DirectoryManager.Data.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace DirectoryManager.Web.Models.ContentSnippet
{
    public class ContentSnippetEditModel
    {
        public int ContentSnippetId { get; set; }

        public SiteConfigSetting SnippetType { get; set; }

        public string? Content { get; set; }

        public List<SelectListItem> UnusedSnippetTypes { get; set; } = new List<SelectListItem>();
    }
}