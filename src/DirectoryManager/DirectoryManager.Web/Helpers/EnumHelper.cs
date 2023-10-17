using System.ComponentModel;
using System.Reflection;

namespace DirectoryManager.Web.Helpers
{
    public class EnumHelper
    {
        public static string GetDescription(Enum value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            FieldInfo? fieldInfo = value.GetType().GetField(value.ToString());

            if (fieldInfo != null)
            {
                object[] attributes = fieldInfo.GetCustomAttributes(typeof(DescriptionAttribute), false);

                if (attributes.Length > 0 && attributes[0] is DescriptionAttribute descriptionAttribute)
                {
                    return descriptionAttribute.Description;
                }
            }

            return value.ToString();
        }

        public static T ParseStringToEnum<T>(string value)
            where T : struct, IConvertible
        {
            if (!typeof(T).IsEnum)
            {
                throw new ArgumentException("T must be an enumerated type.");
            }

            if (Enum.TryParse(value, ignoreCase: true, out T result))
            {
                return result;
            }
            else
            {
                throw new ArgumentException($"Unable to parse '{value}' as enum of type '{typeof(T).Name}'.");
            }
        }
    }
}
