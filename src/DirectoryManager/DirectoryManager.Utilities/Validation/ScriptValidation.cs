namespace DirectoryManager.Utilities.Validation
{
    public class ScriptValidation
    {
        public static bool ContainsScriptTag(object obj)
        {
            var properties = obj.GetType().GetProperties();
            foreach (var property in properties)
            {
                if (property.PropertyType == typeof(string))
                {
                    var value = property.GetValue(obj) as string;
                    if (!string.IsNullOrEmpty(value))
                    {
                        var decodedValue = System.Net.WebUtility.HtmlDecode(value);
                        var normalizedValue = System.Text.RegularExpressions.Regex.Replace(decodedValue, @"\s+", " ").ToLower();
                        if (normalizedValue.Contains("<script") || normalizedValue.Contains("< script") || normalizedValue.Contains("&lt;script&gt;"))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}