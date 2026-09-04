using Microsoft.OpenApi;
using MiniErp.Api.Controllers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Api.Swagger;

public sealed class DashboardSwaggerDocumentation : IOperationFilter
{
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(DashboardController))
        {
            return;
        }

        operation.Summary = "ملخص لوحة التحكم الرئيسية";
        operation.Description = SwaggerOperationDescription.Create(
            "يعرض ملخصًا تشغيليًا وماليًا للشركة الحالية في طلب واحد.",
            "أرسل `fromDate` و`toDate` اختياريًا بصيغة YYYY-MM-DD. عند عدم إرسالهما تُستخدم السنة المالية الحالية، أو أحدث سنة عند عدم تحديد سنة حالية، ويجب أن تكون الفترة داخل سنة مالية واحدة وألا تتجاوز 366 يومًا.",
            "المبيعات والمشتريات والمبالغ المستحقة والربحية بالعملة الأساسية. أرصدة الخزائن منفصلة حسب العملة ولا تُجمع عملات مختلفة. قيمة المخزون والأصناف ذات الرصيد لقطة حالية وليست لقطة تاريخية. العملاء والموردون يُحددان من نوع الفواتير التي تعاملوا بها.",
            "يرجع بطاقات الملخص، حالات الفواتير، النشاط الشهري، أرصدة الخزائن، الجاهزية المحاسبية والتنبيهات. المرتبات ليست جزءًا من هذه اللوحة.");
        operation.OperationId = "Dashboard_Get";
    }
}
