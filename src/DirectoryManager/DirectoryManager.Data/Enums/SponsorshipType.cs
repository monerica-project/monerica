using System.ComponentModel;

namespace DirectoryManager.Data.Enums
{
    public enum SponsorshipType
    {
        Unknown = 0,
        [Description("Main Sponsor")]
        MainSponsor = 1,
        [Description("Sub Category Sponsor")]
        SubCategorySponsor = 2,
    }
}