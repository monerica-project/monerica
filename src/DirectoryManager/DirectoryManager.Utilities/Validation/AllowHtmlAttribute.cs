namespace DirectoryManager.Utilities.Validation
{
    /// <summary>
    /// Opt-out marker for <see cref="InputHtmlGuard"/>. Decorate a string property
    /// with this when it is *supposed* to contain markup (e.g. an admin-authored
    /// content snippet or HTML email body). Every string property NOT decorated
    /// with this is treated as plain-text and rejected if it contains HTML/CSS/JS.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class AllowHtmlAttribute : Attribute
    {
    }
}
