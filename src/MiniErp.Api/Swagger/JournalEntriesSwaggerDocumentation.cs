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
                "يعرض رأس القيد وكل أسطر المدين والدائن ومصدر القيد إن كان تلقائيًا."),
            nameof(JournalEntriesController.Create) => (
                "إضافة وترحيل قيد فورًا",
                "يحفظ القيد كقيد مرحل مباشرة بدون مسودة أو خطوة ترحيل مستقلة. يجب أن يتساوى إجمالي المدين والدائن، وأن تكون السنة مفتوحة والحسابات تفصيلية وفعالة."),
            nameof(JournalEntriesController.Update) => (
                "تعديل قيد يدوي مباشرة",
                "يعدل القيود اليومية والتسويات والافتتاحيات مباشرة. القيود التلقائية تتحدث من الحركة المصدر فقط."),
            nameof(JournalEntriesController.Delete) => (
                "حذف قيد يدوي مباشرة",
                "يحذف القيود اليومية والتسويات والافتتاحيات مباشرة. القيود التلقائية تُحذف من الحركة المصدر فقط."),
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
