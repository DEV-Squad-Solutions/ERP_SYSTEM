using Microsoft.OpenApi;
using MiniErp.Api.Controllers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Api.Swagger;

public sealed class JournalEntriesSwaggerDocumentation : IOperationFilter
{
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(JournalEntriesController))
        {
            return;
        }

        var documentation = context.MethodInfo.Name switch
        {
            nameof(JournalEntriesController.GetAll) => (
                "عرض القيود اليومية",
                "يعرض القيود المرحلة تلقائيًا مع البحث والتصفية بالسنة المالية والنوع والحالة والتاريخ."),
            nameof(JournalEntriesController.GetById) => (
                "تفاصيل قيد يومية",
                "يعرض رأس القيد وكل أسطر المدين والدائن وروابط العكس إن وجدت."),
            nameof(JournalEntriesController.Create) => (
                "إضافة وترحيل قيد فورًا",
                "يحفظ القيد كقيد مرحل مباشرة بدون مسودة أو خطوة ترحيل مستقلة. يجب أن يتساوى إجمالي المدين والدائن، وأن تكون السنة مفتوحة والحسابات تفصيلية وفعالة."),
            nameof(JournalEntriesController.Reverse) => (
                "عكس قيد مرحل",
                "ينشئ قيدًا عكسيًا مرحلًا تلقائيًا ويعلّم القيد الأصلي كمعكوس. لا يتم تعديل القيد الأصلي أو حذفه."),
            _ => (string.Empty, string.Empty)
        };

        if (string.IsNullOrEmpty(documentation.Item1))
        {
            return;
        }

        operation.Summary = documentation.Item1;
        operation.Description = documentation.Item2;
        operation.OperationId = $"JournalEntries_{context.MethodInfo.Name}";
    }
}
