using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models
{
    /// <summary>
    /// An extra subcategory a listing also appears in, besides its main SubCategoryId (up to
    /// <see cref="Common.Constants.IntegerConstants.MaxAdditionalSubcategories"/> per listing).
    /// Every override is optional: a blank field falls back to the main listing's value. Status,
    /// country, KYC, tags, reviews and the profile page are always shared with the main listing.
    /// </summary>
    public class DirectoryEntryAdditionalSubcategory : StateInfo
    {
        [Key]
        public int DirectoryEntryAdditionalSubcategoryId { get; set; }

        public int DirectoryEntryId { get; set; }

        public DirectoryEntry DirectoryEntry { get; set; } = null!;

        public int SubCategoryId { get; set; }

        public Subcategory SubCategory { get; set; } = null!;

        [MaxLength(255)]
        public string? Name { get; set; }

        [MaxLength(500)]
        public string? Link { get; set; }

        [MaxLength(500)]
        public string? LinkA { get; set; }

        [MaxLength(500)]
        public string? Link2 { get; set; }

        [MaxLength(500)]
        public string? Link2A { get; set; }

        [MaxLength(500)]
        public string? Link3 { get; set; }

        [MaxLength(500)]
        public string? Link3A { get; set; }

        public string? Description { get; set; }

        /// <summary>
        /// The listing as it should appear in this subcategory: a copy of <paramref name="main"/>
        /// with this row's non-blank overrides applied and SubCategory switched to this one.
        /// The copy is for display only and must never be saved.
        /// </summary>
        public DirectoryEntry ApplyTo(DirectoryEntry main)
        {
            var copy = main.ShallowCopy();

            copy.Name = Pick(this.Name, main.Name)!;
            copy.Link = Pick(this.Link, main.Link)!;
            copy.LinkA = Pick(this.LinkA, main.LinkA);
            copy.Link2 = Pick(this.Link2, main.Link2);
            copy.Link2A = Pick(this.Link2A, main.Link2A);
            copy.Link3 = Pick(this.Link3, main.Link3);
            copy.Link3A = Pick(this.Link3A, main.Link3A);
            copy.Description = Pick(this.Description, main.Description);

            copy.SubCategoryId = this.SubCategoryId;
            copy.SubCategory = this.SubCategory ?? main.SubCategory;
            copy.ShownViaAdditionalSubcategoryId = this.DirectoryEntryAdditionalSubcategoryId;

            // An alternative's own link must not inherit the main affiliate link (it would point
            // to the wrong product); only fall back to the main affiliate when the link is shared.
            if (!string.IsNullOrWhiteSpace(this.Link) && string.IsNullOrWhiteSpace(this.LinkA))
            {
                copy.LinkA = null;
            }

            if (!string.IsNullOrWhiteSpace(this.Link2) && string.IsNullOrWhiteSpace(this.Link2A))
            {
                copy.Link2A = null;
            }

            if (!string.IsNullOrWhiteSpace(this.Link3) && string.IsNullOrWhiteSpace(this.Link3A))
            {
                copy.Link3A = null;
            }

            return copy;
        }

        private static string? Pick(string? overrideValue, string? mainValue) =>
            string.IsNullOrWhiteSpace(overrideValue) ? mainValue : overrideValue.Trim();
    }
}
