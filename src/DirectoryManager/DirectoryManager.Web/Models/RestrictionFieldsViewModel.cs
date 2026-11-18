using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models
{
    /// <summary>
    /// Values for the shared "Restrictions" form section (Views/Shared/_RestrictionFields.cshtml),
    /// used by the admin listing form, the public submission forms and the admin submission review.
    /// The inputs post as RestrictedCountries / VpnPolicy / TorPolicy / OtherRestrictions, which is
    /// the property name on every one of those models.
    /// </summary>
    public class RestrictionFieldsViewModel
    {
        /// <summary>The country text box as it should be shown (names, codes, or the visitor's own text after an error).</summary>
        public string? RestrictedCountries { get; set; }

        public VpnPolicy VpnPolicy { get; set; }

        public TorPolicy TorPolicy { get; set; }

        public string? OtherRestrictions { get; set; }

        /// <summary>Class for the collapsible section (the public submission form groups use "ui-fieldset").</summary>
        public string DetailsCssClass { get; set; } = "form-group";

        /// <summary>When true, lists what every VPN / Tor option means (the public submission form).</summary>
        public bool ShowOptionHelp { get; set; }
    }
}
