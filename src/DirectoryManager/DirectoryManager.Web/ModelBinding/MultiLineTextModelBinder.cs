using DirectoryManager.Utilities.Validation;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DirectoryManager.Web.ModelBinding
{
    /// <summary>
    /// Binds a string value and cleans it as multi-line user text (line breaks preserved).
    /// Selected per-property (or per-parameter) via <see cref="CleanMultiLineAttribute"/>.
    /// Relies on the framework-default BinderTypeModelBinderProvider, so no registration in
    /// Program.cs is required.
    /// </summary>
    public sealed class MultiLineTextModelBinder : IModelBinder
    {
        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            ArgumentNullException.ThrowIfNull(bindingContext);

            var valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
            if (valueProviderResult == ValueProviderResult.None)
            {
                return Task.CompletedTask;
            }

            bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueProviderResult);

            var cleaned = UnicodeSanitizer.CleanMultiLine(valueProviderResult.FirstValue);
            bindingContext.Result = ModelBindingResult.Success(cleaned);
            return Task.CompletedTask;
        }
    }
}
