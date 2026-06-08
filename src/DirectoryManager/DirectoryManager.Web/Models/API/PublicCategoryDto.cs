using System;
using System.Collections.Generic;
using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models.API
{

    public sealed class PublicCategoryDto
    {
        public string Name { get; set; } = string.Empty;

        public string CategoryKey { get; set; } = string.Empty;
    }
}
