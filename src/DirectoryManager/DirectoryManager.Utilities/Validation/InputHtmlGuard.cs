using System.Collections;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace DirectoryManager.Utilities.Validation
{
    /// <summary>
    /// Rejects any HTML, CSS, or JavaScript in plain-text input fields.
    ///
    /// One central place so that EVERY string field on a form is covered automatically
    /// (including fields added later) without per-field attributes. Detection reuses the
    /// existing parser/regex utilities:
    ///   - HtmlValidation.ContainsHtmlTag        -> any HTML tag, incl. encoded variants
    ///   - ScriptValidation.ContainsSuspiciousMarkup -> &lt;script&gt;/&lt;style&gt; and other
    ///       executable tags, inline on* event handlers, and javascript:/vbscript:/data: URIs
    ///       (this is where CSS injection is caught: &lt;style&gt; and style= live inside tags).
    ///
    /// A property opts out (i.e. is allowed to contain markup) by decorating it with
    /// <see cref="AllowHtmlAttribute"/>.
    /// </summary>
    public static class InputHtmlGuard
    {
        public static bool ContainsMarkup(string? input)
        {
            return HtmlValidation.ContainsHtmlTag(input)
                || ScriptValidation.ContainsSuspiciousMarkup(input);
        }

        public static IEnumerable<ValidationResult> Validate(object? model)
        {
            if (model is null)
            {
                yield break;
            }

            var props = model.GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public);

            foreach (var prop in props)
            {
                if (!prop.CanRead || prop.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                if (prop.IsDefined(typeof(AllowHtmlAttribute), inherit: true))
                {
                    continue;
                }

                if (prop.PropertyType == typeof(string))
                {
                    var value = prop.GetValue(model) as string;
                    if (ContainsMarkup(value))
                    {
                        yield return Violation(prop);
                    }
                }
                else if (typeof(IEnumerable<string>).IsAssignableFrom(prop.PropertyType))
                {
                    if (prop.GetValue(model) is IEnumerable<string> strings)
                    {
                        foreach (var str in strings)
                        {
                            if (ContainsMarkup(str))
                            {
                                yield return Violation(prop);
                                break;
                            }
                        }
                    }
                }
            }
        }

        private static ValidationResult Violation(PropertyInfo prop)
        {
            var label = GetDisplayName(prop);
            var message =
                $"{label} may not contain HTML, CSS, or JavaScript. " +
                "Please remove any tags (e.g. <b>, <img>, <style>, <script>), " +
                "style/script blocks, on… event handlers, and javascript:/data: links.";

            return new ValidationResult(message, new[] { prop.Name });
        }

        private static string GetDisplayName(PropertyInfo prop)
        {
            var display = prop.GetCustomAttribute<DisplayAttribute>();
            if (display?.GetName() is { Length: > 0 } displayName)
            {
                return displayName;
            }

            var displayNameAttr = prop.GetCustomAttribute<DisplayNameAttribute>();
            if (!string.IsNullOrWhiteSpace(displayNameAttr?.DisplayName))
            {
                return displayNameAttr!.DisplayName;
            }

            return prop.Name;
        }
    }
}