using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.ModelBinding
{
    /// <summary>
    /// Marks a bound string as multi-line user text (review/reply bodies, descriptions,
    /// notes). During model binding the value is run through
    /// <see cref="DirectoryManager.Utilities.Validation.UnicodeSanitizer.CleanMultiLine"/>:
    /// invisible / control / direction-changing characters are stripped and the text is
    /// NFC-normalized, but line breaks are preserved (normalized to "\n").
    ///
    /// Do NOT apply to URLs, PGP keys, CSS, or HTML-snippet fields.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Property | AttributeTargets.Parameter,
        AllowMultiple = false,
        Inherited = true)]
    public sealed class CleanMultiLineAttribute : ModelBinderAttribute
    {
        public CleanMultiLineAttribute()
            : base(typeof(MultiLineTextModelBinder))
        {
        }
    }
}
