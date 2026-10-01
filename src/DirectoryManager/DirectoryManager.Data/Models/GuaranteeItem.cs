using DirectoryManager.Data.Enums;

namespace DirectoryManager.Data.Models
{
    /// <summary>
    /// One deposit guarantee: a proof link, an amount, and the amount's currency. This is the
    /// in-memory shape used by the Submission JSON carrier and for display; the persisted per-entry
    /// form is the <see cref="DirectoryEntryGuarantee"/> table.
    /// </summary>
    public sealed class GuaranteeItem
    {
        // Link/Amount are nullable on purpose: this is the form-bound shape, and empty guarantee
        // rows must bind cleanly (a non-nullable string gets an implicit "required" and a
        // non-nullable number rejects an empty value). Blank rows are dropped on normalize.
        public string? Link { get; set; }

        public decimal? Amount { get; set; }

        public Currency Currency { get; set; } = Currency.USD;
    }
}
