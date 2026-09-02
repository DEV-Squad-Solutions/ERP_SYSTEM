using Microsoft.OpenApi;
using MiniErp.Api.Controllers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Api.Swagger;

public sealed class AccountMappingsSwaggerDocumentation : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(AccountMappingsController))
        {
            return;
        }

        operation.OperationId = $"AccountMappings_{context.MethodInfo.Name}";
        (operation.Summary, operation.Description) = context.MethodInfo.Name switch
        {
            nameof(AccountMappingsController.Get) => (
                "عرض الربط المحاسبي الافتراضي",
                "يعرض كل روابط السنة المالية مع اسم المصدر والحساب. يمكن القراءة حتى لو كانت السنة مغلقة. أرسل `fiscalYearId` في query."),
            nameof(AccountMappingsController.Replace) => (
                "حفظ الربط المحاسبي الافتراضي",
                "يستبدل كل روابط السنة المالية دفعة واحدة وبشكل ذري. أرسل `fiscalYearId` في query و`mappings` في body. أنواع الربط التي تحتاج `sourceId` هي Cashbox وCashMovementType، أما باقي الأنواع فهي مفردة بدون مصدر. الحساب يجب أن يكون فعالًا وقابلًا للتسجيل ومتوافقًا مع نوع الربط، وترجع كل الأخطاء داخل `errors` عند وجود أكثر من خطأ."),
            _ => (operation.Summary, operation.Description)
        };
    }
}
