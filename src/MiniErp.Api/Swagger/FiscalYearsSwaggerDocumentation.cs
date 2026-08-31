using Microsoft.OpenApi;
using MiniErp.Api.Controllers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Api.Swagger;

public sealed class FiscalYearsSwaggerDocumentation : IOperationFilter
{
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType !=
            typeof(FiscalYearsController))
        {
            return;
        }

        var documentation = context.MethodInfo.Name switch
        {
            nameof(FiscalYearsController.GetAll) => (
                "عرض السنوات المالية",
                SwaggerOperationDescription.Create(
                    "يعرض السنوات المالية المفتوحة والمغلقة للشركة الحالية مع دعم البحث والتصفية والترقيم.",
                    "اختياري: `pageNumber`, `pageSize`, `search`, `status` بقيمة 1 للمفتوحة أو 2 للمغلقة، و`isCurrent`.",
                    "`pageNumber` أكبر من صفر و`pageSize` من 1 إلى 100. البحث لا يتجاوز 200 حرف.",
                    "النتائج تخص الشركة الحالية فقط، والسنوات المحذوفة لا تظهر. يمكن عرض السنة المغلقة للرجوع إلى بياناتها.")),
            nameof(FiscalYearsController.GetSelect) => (
                "تحميل السنوات المالية للاختيار",
                SwaggerOperationDescription.Create(
                    "يعيد قائمة خفيفة بالسنوات المالية لاستخدامها في القوائم المنسدلة، مرتبة بحيث تظهر السنة الحالية أولاً.",
                    "لا توجد حقول مطلوبة.",
                    "لا توجد قواعد تحقق إضافية.",
                    "القائمة تخص الشركة الحالية فقط، وقد تحتوي على سنوات مفتوحة ومغلقة.")),
            nameof(FiscalYearsController.GetCurrent) => (
                "عرض السنة المالية الحالية",
                SwaggerOperationDescription.Create(
                    "يعيد السنة المحددة حاليًا للشركة، وهي السنة الافتراضية للعمليات المحاسبية الجديدة.",
                    "لا توجد حقول مطلوبة.",
                    "لا توجد قواعد تحقق إضافية.",
                    "إذا لم يتم تحديد سنة حالية يرجع 404 بالرمز `FiscalYears.CurrentNotFound`.")),
            nameof(FiscalYearsController.GetById) => (
                "عرض سنة مالية",
                SwaggerOperationDescription.Create(
                    "يعرض سنة مالية واحدة مملوكة للشركة الحالية.",
                    "معرّف موجب في المسار `id`.",
                    "يجب أن يكون `id` أكبر من صفر.",
                    "السجل غير الموجود أو المحذوف أو التابع لشركة أخرى يرجع 404.")),
            nameof(FiscalYearsController.Create) => (
                "إضافة سنة مالية",
                SwaggerOperationDescription.Create(
                    "للمسؤول فقط. ينشئ فترة مالية جديدة للشركة الحالية، ويجعلها حالية عند طلب ذلك.",
                    "`name`, `startDate`, `endDate`, واختياريًا `isCurrent`، وتكون قيمته الافتراضية true.",
                    "الاسم مطلوب وبحد أقصى 200 حرف. يجب أن يكون تاريخ البداية قبل النهاية، ولا يجوز تداخل الفترة مع سنة أخرى.",
                    "أول سنة للشركة تصبح حالية تلقائيًا. عند اختيار سنة حالية جديدة، تُلغى الحالية السابقة فقط؛ لا تتغير حالة السنوات المفتوحة الأخرى.")),
            nameof(FiscalYearsController.Update) => (
                "تعديل سنة مالية",
                SwaggerOperationDescription.Create(
                    "للمسؤول فقط. يعدل سنة مالية مفتوحة باستخدام الحماية من التعارض المتزامن.",
                    "`id` موجب، و`name`, `startDate`, `endDate`, `isCurrent`, و`rowVersion` المطابق لآخر استجابة.",
                    "السنة المغلقة لا يمكن تعديلها. يجب أن تكون الفترة صحيحة وغير متداخلة، ويجب أن يكون `rowVersion` بطول 8 بايت عند فك ترميزه.",
                    "إرسال إصدار قديم أو تعديل سجل تابع لشركة أخرى يرجع 409 أو 404. جعل السنة الحالية يغيّر علامة الحالية ولا يفتح سنة مغلقة تلقائيًا.")),
            nameof(FiscalYearsController.Close) => (
                "إغلاق سنة مالية",
                SwaggerOperationDescription.Create(
                    "للمسؤول فقط. يغلق السنة المالية ويمنع تعديلها أو حذفها حتى تتم إعادة فتحها.",
                    "معرّف السنة في المسار `id`.",
                    "يجب أن يكون `id` أكبر من صفر وأن يكون السجل تابعًا للشركة الحالية.",
                    "إغلاق سنة مغلقة بالفعل يرجع 409 بالرمز `FiscalYears.AlreadyClosed`. الإغلاق لا يغير السنة الحالية.")),
            nameof(FiscalYearsController.Reopen) => (
                "فتح سنة مالية سابقة مرة أخرى",
                SwaggerOperationDescription.Create(
                    "للمسؤول فقط. يعيد فتح سنة مالية مغلقة مؤقتًا للسماح بتعديل الحركات التابعة لتاريخها.",
                    "معرّف السنة المغلقة في المسار `id`.",
                    "يجب أن يكون `id` أكبر من صفر وأن تكون السنة مغلقة.",
                    "إعادة الفتح لا تجعل السنة هي الحالية؛ تظل السنة الحالية كما هي. بعد انتهاء التعديل يمكن إغلاق السنة مرة أخرى. السنة المفتوحة بالفعل ترجع 409 بالرمز `FiscalYears.AlreadyOpen`.")),
            nameof(FiscalYearsController.Delete) => (
                "حذف سنة مالية",
                SwaggerOperationDescription.Create(
                    "للمسؤول فقط. يحذف حذفًا منطقيًا سنة مالية مفتوحة وغير حالية.",
                    "معرّف السنة في المسار `id`.",
                    "يجب أن يكون `id` أكبر من صفر.",
                    "لا يمكن حذف السنة الحالية أو المغلقة أو سنة لها تشكيل أو روابط قوائم مالية. السجل غير الموجود أو التابع لشركة أخرى يرجع 404.")),
            _ => default
        };

        if (documentation == default)
        {
            return;
        }

        operation.Summary = documentation.Item1;
        operation.Description = documentation.Item2;
        operation.OperationId = $"FiscalYears_{context.MethodInfo.Name}";
    }
}
