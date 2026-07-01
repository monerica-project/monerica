using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models.Reviews
{
    public class ReviewTag : UserStateInfo
    {
        [Key]
        public int ReviewTagId { get; set; }

        [Required]
        [MaxLength(64)]
        public string Name { get; set; } = string.Empty; // display

        [Required]
        [MaxLength(64)]
        public string Slug { get; set; } = string.Empty; // unique key for stable references

        [MaxLength(256)]
        public string? Description { get; set; }

        // Controls badge "severity / color"
        public ReviewTagLevel Level { get; set; } = ReviewTagLevel.Neutral;

        public bool IsEnabled { get; set; } = true;

        // =========================================================
        // Optional money-range metadata.
        //
        // Only populated for "money band" tags (e.g. Under $50, $1K-5K, Over $100K).
        // When set, the auto-moderation worker uses these bounds to map a swap's
        // computed USD value onto exactly one band tag. Bounds are inclusive-lower,
        // exclusive-upper:  match  <=>  (MinUsd is null || value >= MinUsd)
        //                                && (MaxUsd is null || value <  MaxUsd).
        //
        // A tag is treated as a money band  <=>  MinUsd or MaxUsd is non-null.
        // Examples:
        //   under-50   -> Min = null,    Max = 50
        //   1k-to-5k   -> Min = 1000,    Max = 5000
        //   over-100k  -> Min = 100000,  Max = null
        // =========================================================
        [Column(TypeName = "decimal(18,2)")]
        public decimal? MinUsd { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? MaxUsd { get; set; }

        /// <summary>True when this tag represents a USD money band.</summary>
        [NotMapped]
        public bool IsMoneyBand => this.MinUsd.HasValue || this.MaxUsd.HasValue;

        /// <summary>Returns true if <paramref name="usdValue"/> falls inside this band.</summary>
        /// <returns></returns>
        public bool Matches(decimal usdValue)
        {
            if (!this.IsMoneyBand)
            {
                return false;
            }

            var aboveMin = !this.MinUsd.HasValue || usdValue >= this.MinUsd.Value;
            var belowMax = !this.MaxUsd.HasValue || usdValue < this.MaxUsd.Value;
            return aboveMin && belowMax;
        }

        public ICollection<DirectoryEntryReviewTag> ReviewLinks { get; set; } = new List<DirectoryEntryReviewTag>();
    }
}
