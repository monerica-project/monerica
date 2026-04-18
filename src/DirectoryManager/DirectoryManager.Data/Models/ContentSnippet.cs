using System.ComponentModel.DataAnnotations.Schema;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models
{
    public class ContentSnippet : StateInfo
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ContentSnippetId { get; set; }

        public SiteConfigSetting SnippetType { get; set; }

        public string? Content { get; set; }
    }
}