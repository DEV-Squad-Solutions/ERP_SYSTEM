using Microsoft.OpenApi;
using MiniErp.Api.Controllers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Api.Swagger;

public sealed class StatementsSwaggerDocumentation : IOperationFilter
{
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(StatementsController))
        {
            return;
        }

        var documentation = context.MethodInfo.Name switch
        {
            nameof(StatementsController.GetCashboxStatement) => (
                "كشف حساب الصندوق",
                """
                يعرض حركة الصندوق بطريقة مباشرة: المبلغ المقبوض، المبلغ المصروف، والرصيد بعد كل حركة.

                **طريقة قراءة الاستجابة:**
                - `cashboxName`: اسم الصندوق المحدد.
                - `currency`: عملة الصندوق.
                - `receiptAmount`: مبلغ دخل إلى الصندوق.
                - `paymentAmount`: مبلغ خرج من الصندوق.
                - `balance`: رصيد الصندوق بعد الحركة.
                - `summary`: رصيد أول المدة وإجمالي القبض والصرف ورصيد آخر المدة.

                **الفلاتر:** `cashboxId` مطلوب. ويمكن التصفية بالبحث والتاريخ واتجاه الحركة ونوعها والطرف ورقم السند.

                رصيد أول المدة يشمل كل حركات الصندوق السابقة لتاريخ البداية. الصندوق غير الموجود أو التابع لشركة أخرى يعيد `404`.
                """),
            nameof(StatementsController.GetPartnerStatement) => (
                "كشف حساب العميل أو المورد",
                """
                يجمع الرصيد الافتتاحي والفواتير وسندات النقدية في كشف واحد، بدون عرض مصطلحات مدين ودائن للمستخدم.

                **طريقة قراءة الاستجابة:**
                - `businessPartnerName`: اسم العميل أو المورد.
                - `movementName`: وصف الحركة بالعربية، مثل فاتورة بيع أو سند قبض.
                - `debitAmount`: يعرض في عمود عليه.
                - `creditAmount`: يعرض في عمود له.
                - `balanceAmount`: قيمة الرصيد بدون إشارة سالبة.
                - `balanceDescription`: يعيد عليه، أو له، أو مسدد.
                - `summary`: رصيد أول المدة ورصيد آخر المدة مع وصف عربي واضح لكل منهما.

                **الفلاتر:** `businessPartnerId` مطلوب. ويمكن التصفية بالبحث والتاريخ ومصدر الحركة ونوع الحركة.

                العميل أو المورد غير الموجود أو التابع لشركة أخرى يعيد `404`.
                """),
            nameof(StatementsController.GetDriverStatement) => (
                "كشف حساب السائق",
                """
                يعرض المبالغ المدفوعة للسائق والمستلمة منه وتكاليف الرحلات في كشف واحد، بدون استخدام مدين ودائن.

                **طريقة قراءة الاستجابة:**
                - `driverName`: اسم السائق.
                - `sourceName`: سند نقدية أو رحلة سائق.
                - `movementName`: اسم حركة النقدية، أو تكلفة رحلة.
                - `amountPaidToDriver`: مبلغ دفعته الشركة للسائق.
                - `amountReceivedFromDriver`: مبلغ استلمته الشركة من السائق.
                - `tripCost`: تكلفة الرحلة المسجلة.
                - `balanceAmount`: قيمة الرصيد بدون إشارة سالبة.
                - `balanceDescription`: يوضح هل المبلغ مطلوب من السائق أو مطلوب دفعه له، أو لا يوجد مبلغ مستحق.
                - `summary`: رصيد أول المدة والإجماليات ورصيد آخر المدة بنفس الوصف المباشر.

                **الفلاتر:** `driverId` مطلوب. ويمكن التصفية بالبحث والتاريخ واتجاه الحركة ونوعها والرحلة والفاتورة وحالة تكلفة الرحلة.

                سند الدفع قد يكون عهدة للسائق قبل الرحلة. لا يتم توزيع السندات العامة تلقائيًا على الرحلات. السائق غير الموجود أو التابع لشركة أخرى يعيد `404`.
                """),
            _ => default
        };

        if (documentation == default)
        {
            return;
        }

        operation.Summary = documentation.Item1;
        operation.Description = documentation.Item2;
        operation.OperationId = $"Statements_{context.MethodInfo.Name}";
    }
}
