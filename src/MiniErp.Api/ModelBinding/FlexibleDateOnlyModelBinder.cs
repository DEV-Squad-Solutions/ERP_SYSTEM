using Microsoft.AspNetCore.Mvc.ModelBinding;
using MiniErp.Application.Common.Parsing;

namespace MiniErp.Api.ModelBinding;

public sealed class FlexibleDateOnlyModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var valueResult = bindingContext.ValueProvider.GetValue(
            bindingContext.ModelName);
        if (valueResult == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(
            bindingContext.ModelName,
            valueResult);
        var value = valueResult.FirstValue;

        if (string.IsNullOrWhiteSpace(value))
        {
            if (Nullable.GetUnderlyingType(
                    bindingContext.ModelType) is not null)
            {
                bindingContext.Result =
                    ModelBindingResult.Success(null);
            }
            else
            {
                AddInvalidDateError(bindingContext);
            }

            return Task.CompletedTask;
        }

        if (FlexibleDateOnlyParser.TryParse(value, out var date))
        {
            bindingContext.Result = ModelBindingResult.Success(date);
        }
        else
        {
            AddInvalidDateError(bindingContext);
        }

        return Task.CompletedTask;
    }

    private static void AddInvalidDateError(
        ModelBindingContext bindingContext) =>
        bindingContext.ModelState.TryAddModelError(
            bindingContext.ModelName,
            "تنسيق التاريخ غير صحيح.");
}
