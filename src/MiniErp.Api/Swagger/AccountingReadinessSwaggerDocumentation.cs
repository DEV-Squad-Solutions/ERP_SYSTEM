using Microsoft.OpenApi;
using MiniErp.Api.Controllers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Api.Swagger;

public sealed class AccountingReadinessSwaggerDocumentation : IOperationFilter
{
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType !=
            typeof(AccountingReadinessController))
        {
            return;
        }

        var documentation = context.MethodInfo.Name switch
        {
            nameof(AccountingReadinessController.Get) => (
                "مراقبة الجاهزية المحاسبية",
                SwaggerOperationDescription.Create(
                    "يفحص سنة مالية ويعرض مؤشرات اكتمال دفتر الأستاذ الموحد بدون تعديل البيانات.",
                    "أرسل `fiscalYearId` في query.",
                    "يعرض المصادر بلا قيود، والقيود الآلية بلا مصدر أو المكررة أو غير المتوازنة، وتكاليف المخزون المعلقة، والروابط المحاسبية الناقصة أو غير الصالحة. المرتبات تظهر كعدد مؤجل ولا تمنع الجاهزية الحالية.",
                    "`isReady=true` تعني عدم وجود مشكلة مانعة في المصادر غير المؤجلة. تعرض `issues` التفاصيل اللازمة للتصحيح.")),
            nameof(AccountingReadinessController.Backfill) => (
                "إنشاء أو تحديث قيود البيانات القديمة",
                SwaggerOperationDescription.Create(
                    "للمسؤول فقط. يعيد حساب تكلفة المخزون ثم ينشئ أو يحدّث القيود التلقائية للمصادر القديمة داخل السنة، ويعيد تقرير الجاهزية بعد التنفيذ.",
                    "أرسل `fiscalYearId` لسنة مفتوحة في query. لا يوجد body.",
                    "العملية ذرية: عند فشل أي مصدر يتم Rollback ولا تظل قيود جزئية. إعادة نفس الطلب لا تكرر القيود، والمرتبات لا تدخل في العملية.",
                    "ترجع أعداد `processedSources`, `createdJournals`, `updatedJournals` وتقرير `readiness` النهائي. السنة المغلقة أو نقص الربط يرجع استجابة الخطأ الموحدة.")),
            _ => default
        };

        if (documentation == default)
        {
            return;
        }

        operation.Summary = documentation.Item1;
        operation.Description = documentation.Item2;
        operation.OperationId =
            $"AccountingReadiness_{context.MethodInfo.Name}";
    }
}
