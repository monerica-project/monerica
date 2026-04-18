using System.ComponentModel.DataAnnotations;
using DirectoryManager.Utilities.Validation;

namespace DirectoryManager.Web.Attributes
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class MoneroAddressAttribute : ValidationAttribute
    {
        public MoneroAddressAttribute()
            : base("Please enter a valid Monero address.")
        {
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            var address = value as string;

            // Allow null/empty — use [Required] separately if the field is mandatory
            if (string.IsNullOrWhiteSpace(address))
            {
                return ValidationResult.Success;
            }

            return MoneroAddressValidator.IsValid(address)
                ? ValidationResult.Success
                : new ValidationResult(this.ErrorMessage, new[] { validationContext.MemberName! });
        }
    }
}