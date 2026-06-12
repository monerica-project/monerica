using DirectoryManager.Utilities.Validation;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DirectoryManager.Web.ModelBinding
{
    /// <summary>
    /// Binds a string value and cleans it as single-line user text. Selected per-property
    /// (or per-parameter) via <see cref="CleanSingleLineAttribute"/>. Relies on the
    /// framework-default BinderTypeModelBinderProvider, so no registration in Program.cs
    /// is required.
    /// </summary>
    public sealed class SingleLineTextModelBinder : IModelBinder
    {
        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            ArgumentNullException.ThrowIfNull(bindingContext);

            var valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
            if (valueProviderResult == ValueProviderResult.None)
            {
                // No incoming value: leave the property at its default. Required/range
                // validation (if any) still runs afterwards.
                return Task.CompletedTask;
            }

            bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueProviderResult);

            var cleaned = UnicodeSanitizer.CleanSingleLine(valueProviderResult.FirstValue);
            bindingContext.Result = ModelBindingResult.Success(cleaned);
            return Task.CompletedTask;
        }
    }
}
