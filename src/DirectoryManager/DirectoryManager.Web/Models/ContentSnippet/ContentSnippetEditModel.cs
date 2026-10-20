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

        /// <summary>Form target action ("Create" or "Edit"); set by the view, never posted.</summary>
        [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
        public string FormAction { get; set; } = "Create";

        /// <summary>Submit button text; set by the view, never posted.</summary>
        [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
        public string SubmitLabel { get; set; } = "Create";
    }
}