using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace MiniErp.Api.ModelBinding;

public sealed class FlexibleDateOnlyModelBinderProvider
    : IModelBinderProvider
{
    private static readonly IModelBinder Binder =
        new FlexibleDateOnlyModelBinder();

    public IModelBinder? GetBinder(
        ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var modelType =
            Nullable.GetUnderlyingType(context.Metadata.ModelType) ??
            context.Metadata.ModelType;

        return modelType == typeof(DateOnly)
            ? Binder
            : null;
    }
}
