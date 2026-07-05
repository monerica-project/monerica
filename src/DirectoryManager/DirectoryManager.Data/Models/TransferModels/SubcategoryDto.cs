using DirectoryManager.Common.Helpers;

namespace DirectoryManager.Data.Models.TransferModels
{
    public class SubcategoryDto
    {
        public int SubcategoryId { get; set; }
        public string Name { get; set; } = "";
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = "";
        public string Key { get; set; } = "";
        public bool IsEnabled { get; set; }

        public string DisplayName
        {
            get
            {
                return CategoryFormatter.Format(this.CategoryName, this.Name);
            }
        }
    }
}