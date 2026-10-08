using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bingo.Web.Boards;

// This HTML number input posts invariant decimals regardless of the page language.
public sealed class ManualEhbInputBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var value = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (value == ValueProviderResult.None) return Task.CompletedTask;
        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, value);
        var input = value.FirstValue;
        if (string.IsNullOrWhiteSpace(input))
            bindingContext.Result = ModelBindingResult.Success(null);
        else if (decimal.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out var amount))
            bindingContext.Result = ModelBindingResult.Success(amount);
        else
            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName,
                bindingContext.ModelMetadata.ModelBindingMessageProvider.AttemptedValueIsInvalidAccessor(
                    input, bindingContext.ModelMetadata.GetDisplayName()));
        return Task.CompletedTask;
    }
}
