using System;
using System.Collections.Generic;
using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models.API
{

    public sealed class PublicSubcategoryDto
    {
        public string Name { get; set; } = string.Empty;

        public string SubCategoryKey { get; set; } = string.Empty;

        public PublicCategoryDto? Category { get; set; }
    }
}
