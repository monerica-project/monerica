using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.ModelBinding
{
    /// <summary>
    /// Marks a bound string as single-line user text (names, titles, contacts, tags).
    /// During model binding the value is run through
    /// <see cref="DirectoryManager.Utilities.Validation.UnicodeSanitizer.CleanSingleLine"/>:
    /// invisible / control / direction-changing characters are stripped, the text is
    /// NFC-normalized, line breaks become spaces, and whitespace is collapsed.
    ///
    /// Do NOT apply to URLs, PGP keys, CSS, or HTML-snippet fields — those have their own
    /// validators and must keep their raw bytes.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Property | AttributeTargets.Parameter,
        AllowMultiple = false,
        Inherited = true)]
    public sealed class CleanSingleLineAttribute : ModelBinderAttribute
    {
        public CleanSingleLineAttribute()
            : base(typeof(SingleLineTextModelBinder))
        {
        }
    }
}
