using Microsoft.OpenApi;
using MiniErp.Api.Controllers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniErp.Api.Swagger;

public sealed class AccountingSetupSwaggerDocumentation : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var controllerType = context.MethodInfo.DeclaringType;
        var methodName = context.MethodInfo.Name;
        var documentation = controllerType switch
        {
            var type when type == typeof(AccountsController) =>
                GetAccountsDocumentation(methodName),
            var type when type == typeof(FinancialStatementLinesController) =>
                GetLinesDocumentation(methodName),
            var type when type == typeof(AccountStatementMappingsController) =>
                GetMappingsDocumentation(methodName),
            _ => default
        };

        if (documentation == default)
        {
            return;
        }

        operation.Summary = documentation.Item1;
        operation.Description = documentation.Item2;
        operation.OperationId = $"{controllerType!.Name.Replace("Controller", string.Empty)}_{methodName}";
    }

    private static (string, string) GetAccountsDocumentation(string methodName) =>
        methodName switch
        {
            nameof(AccountsController.GetAll) => (
                "عرض دليل الحسابات",
                SwaggerOperationDescription.Create(
                    "يعرض حسابات الشركة الحالية بصفحات مع البحث والتصفية حسب النوع والطبيعة والحساب الأب وحالة التسجيل والتفعيل.",
                    "`pageNumber`, `pageSize`، وباقي الفلاتر اختيارية.",
                    "أنواع الحساب: Asset, Liability, Equity, Revenue, Expense. طبيعة الرصيد: Debit أو Credit.",
                    "الحسابات المحذوفة أو التابعة لشركة أخرى لا تظهر.")),
            nameof(AccountsController.GetTree) => (
                "تحميل شجرة الحسابات",
                SwaggerOperationDescription.Create(
                    "يعيد دليل الحسابات كاملًا في صورة شجرة لاستخدامه في شاشة الإدارة.",
                    "لا توجد حقول مطلوبة.",
                    "لا توجد قواعد تحقق إضافية.",
                    "كل عنصر يحتوي على `children` و`rowVersion` للتعديل.")),
            nameof(AccountsController.GetSelect) => (
                "تحميل الحسابات القابلة للتسجيل",
                SwaggerOperationDescription.Create(
                    "يعيد الحسابات الفعالة التي تسمح بالتسجيل فقط للقوائم المنسدلة والربط.",
                    "لا توجد حقول مطلوبة.",
                    "لا توجد قواعد تحقق إضافية.",
                    "يعيد `id`, `code`, `name`, `accountType`.")),
            nameof(AccountsController.GetJournalSelect) => (
                "تحميل حسابات القيود اليدوية",
                SwaggerOperationDescription.Create(
                    "يعيد الحسابات الفرعية الفعالة المتاحة للقيد اليدوي أو التسوية أو القيد الافتتاحي في سنة مالية محددة.",
                    "أرسل `fiscalYearId` في query.",
                    "السنة يجب أن تكون موجودة في الشركة الحالية. الحسابات المرتبطة بعناصر تشغيلية لا تظهر حتى لا يتم تجاوز الحركة المصدر.",
                    "القائمة مخصصة لشاشة القيود اليدوية، أما القيود التلقائية فتستخدم روابط المستندات الداخلية.")),
            nameof(AccountsController.GetById) => (
                "عرض حساب",
                "يعرض حسابًا واحدًا من الشركة الحالية بواسطة `id`."),
            nameof(AccountsController.Create) => (
                "إضافة حساب",
                SwaggerOperationDescription.Create(
                    "للمسؤول فقط. يضيف حسابًا رئيسيًا أو فرعيًا إلى دليل الشركة.",
                    "`name`, `accountType`, `normalBalance`, `isPosting`، واختياريًا `parentAccountId` و`isActive`. يمكن ترك `code` فارغًا أو إرساله بقيمة null؛ ينشئ الخادم كودًا رقميًا هرميًا فريدًا داخل الشركة (مثل 1000 للحساب الرئيسي و1100 للحساب الفرعي). إذا أُرسل code عند الإضافة يتم تجاهله.",
                    "عند اختيار أب يجب أن يكون فعالًا ورئيسيًا، ويأخذ الحساب الفرعي نوع الحساب وطبيعة الرصيد من الأب تلقائيًا بغض النظر عن القيم المرسلة.",
                    "الحساب القابل للتسجيل لا يمكن أن يصبح أبًا لحسابات أخرى.")),
            nameof(AccountsController.Update) => (
                "تعديل حساب",
                SwaggerOperationDescription.Create(
                    "للمسؤول فقط. يعدل الحساب مع حماية `rowVersion`.",
                    "نفس حقول الإضافة مع `rowVersion` من آخر استجابة. عند اختيار أب تُورث منه `accountType` و`normalBalance` تلقائيًا.",
                    "يُمنع تكوين دورة في الشجرة أو جعل حساب ذي أبناء قابلًا للتسجيل. الحساب المربوط بقائمة لا يمكن تغيير نوعه أو تعطيله.",
                    "الإصدار القديم يرجع 409.")),
            nameof(AccountsController.Delete) => (
                "حذف حساب",
                "للمسؤول فقط. يحذف الحساب منطقيًا إذا لم تكن له حسابات فرعية أو روابط قوائم حالية أو تاريخية."),
            _ => default
        };

    private static (string, string) GetLinesDocumentation(string methodName) =>
        methodName switch
        {
            nameof(FinancialStatementLinesController.GetAll) => (
                "عرض بنود قائمة مالية",
                "يعرض بنود سنة ونوع قائمة محددين بصفحات. `fiscalYearId` و`statementType` مطلوبان."),
            nameof(FinancialStatementLinesController.GetTree) => (
                "تحميل تشكيل قائمة مالية كشجرة",
                "يعيد شجرة بنود القائمة حسب `fiscalYearId` و`statementType`: FinancialPosition أو IncomeStatement أو CashFlow."),
            nameof(FinancialStatementLinesController.GetSelect) => (
                "تحميل بنود القائمة القابلة للربط",
                "يعيد البنود الفعالة التي تسمح بربط الحسابات فقط لنفس السنة ونوع القائمة."),
            nameof(FinancialStatementLinesController.GetById) => (
                "عرض بند قائمة مالية",
                "يعرض بندًا واحدًا من الشركة الحالية بواسطة `id`."),
            nameof(FinancialStatementLinesController.Create) => (
                "إضافة بند قائمة مالية",
                SwaggerOperationDescription.Create(
                    "للمسؤول فقط. يضيف عنوانًا تجميعيًا أو بندًا قابلًا لربط الحسابات.",
                    "`fiscalYearId`, `statementType`, `code`, `name`, `displayOrder`, `isAssignable`، واختياريًا `parentLineId` و`isActive`.",
                    "السنة يجب أن تكون مفتوحة. الأب يجب أن يكون من نفس السنة والنوع وألا يكون قابلًا للربط.",
                    "الكود فريد داخل السنة ونوع القائمة.")),
            nameof(FinancialStatementLinesController.Update) => (
                "تعديل بند قائمة مالية",
                "للمسؤول فقط. يعدل البند باستخدام `rowVersion`. يُمنع التعديل على سنة مغلقة أو تعطيل بند مرتبط بحسابات."),
            nameof(FinancialStatementLinesController.Delete) => (
                "حذف بند قائمة مالية",
                "للمسؤول فقط. يحذف البند منطقيًا من سنة مفتوحة إذا لم تكن له بنود فرعية أو حسابات مرتبطة."),
            _ => default
        };

    private static (string, string) GetMappingsDocumentation(string methodName) =>
        methodName switch
        {
            nameof(AccountStatementMappingsController.Get) => (
                "عرض ربط الحسابات بقائمة مالية",
                "يعرض روابط الحسابات للسنة ونوع القائمة المحددين، ويمكن العرض حتى لو كانت السنة مغلقة."),
            nameof(AccountStatementMappingsController.Replace) => (
                "حفظ ربط الحسابات بقائمة مالية",
                SwaggerOperationDescription.Create(
                    "للمسؤول فقط. يستبدل جميع روابط السنة ونوع القائمة دفعة واحدة وبشكل ذري.",
                    "في query: `fiscalYearId`, `statementType`. في body: `mappings` وكل عنصر يحتوي `accountId` و`financialStatementLineId`.",
                    "السنة يجب أن تكون مفتوحة. الحساب فعال وقابل للتسجيل ومتوافق مع نوع القائمة، والبند فعال وقابل للربط. لإلغاء كل الروابط أرسل قائمة فارغة.",
                    "في حالة وجود أكثر من خطأ ترجع جميع الأخطاء داخل `errors` بنفس شكل الاستجابة الموحد.")),
            _ => default
        };
}
