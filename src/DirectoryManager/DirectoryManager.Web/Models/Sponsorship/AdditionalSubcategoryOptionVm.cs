namespace DirectoryManager.Web.Models.Sponsorship
{
    public class AdditionalSubcategoryOptionVm
    {
        public int SubCategoryId { get; set; }

        // The name the listing appears under in that subcategory.
        public string ShownAsName { get; set; } = "";

        public SponsorshipTypeOptionVm Option { get; set; } = new ();
    }
}
