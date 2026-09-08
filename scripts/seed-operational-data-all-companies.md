# إضافة بيانات تشغيلية تجريبية لكل الشركات

يشغّل `seed-operational-data-all-companies.ps1` عمليات الإضافة من خلال API، وليس
بإدخال الفواتير أو السندات مباشرة في الجداول. لذلك ينفّذ التطبيق تلقائيًا حركات
المخزون، حركة العميل/المورد، سندات النقدية والقيود المحاسبية المرتبطة.

البيانات المضافة لكل شركة:

- عملاء وموردون داخل سجل الأطراف الموحد.
- سنة مالية حالية عند عدم وجود سنة مفتوحة؛ وإنشاؤها يجهز دليل الحسابات والربط
  الافتراضي تلقائيًا من خلال منطق التطبيق.
- وحدة قياس، تصنيف أصناف، مخزن وخزينة.
- أصناف.
- فواتير مشتريات ومبيعات لكل صنف.
- مردود مبيعات ومردود مشتريات مرتبطان بالفواتير الأصلية.
- سند قبض من عميل، سند صرف لمورد ومصروف إداري؛ كلها مرحلة.
- قيد يدوي وقيد تسوية؛ كلاهما مرحّل.
- سائقون وموظفون، بدون إنشاء مرتبات أو حضور أو حركات موظفين.

## المعاينة

```powershell
.\scripts\seed-operational-data-all-companies.ps1 `
  -BaseUrl "https://localhost:5001" `
  -UserName "admin" `
  -Password "YOUR_PASSWORD" `
  -Preview
```

## التنفيذ

```powershell
.\scripts\seed-operational-data-all-companies.ps1 `
  -BaseUrl "https://localhost:5001" `
  -UserName "admin" `
  -Password "YOUR_PASSWORD" `
  -CustomerCount 3 `
  -SupplierCount 3 `
  -ItemCount 5 `
  -DriverCount 2 `
  -EmployeeCount 3 `
  -ContinueOnError
```

المستخدم يجب أن يكون `Admin` ومضافًا إلى كل الشركات المطلوب تجهيزها. السكربت
يقارن الشركات المضافة للمستخدم مع كل الشركات الموجودة، ويتوقف قبل الكتابة إذا
وجد شركة غير مضافة إليه. استخدم `Prefix` مختلفًا إذا أردت مجموعة تجريبية ثانية:

```powershell
.\scripts\seed-operational-data-all-companies.ps1 `
  -Password "YOUR_PASSWORD" `
  -Prefix "AUTO-DEMO-02"
```

إعادة التشغيل بنفس `Prefix` آمنة: السكربت يبحث عن الأسماء وأرقام الفواتير
والأوصاف قبل الإضافة ويتجاوز البيانات الموجودة. ولو وُجد سند كمسودة بسبب توقف
تشغيل سابق، يكمل ترحيله بدل إنشاء سند مكرر. إذا فشلت شركة، يمكن إصلاح سبب الفشل
وإعادة الأمر نفسه لإكمال العناصر الناقصة.

إذا كانت شهادة HTTPS المحلية تجريبية، أضف `-SkipCertificateCheck`. وبعد التنفيذ
يمكن تشغيل `verify-operational-data-all-companies.sql` على SQL Server لعرض أعداد
السجلات الناتجة لكل شركة؛ الاستعلام للقراءة فقط.

ويمكن بدلًا منه تشغيل الفاحص عبر الـAPI:

```powershell
.\scripts\verify-operational-data-all-companies.ps1 `
  -BaseUrl "https://localhost:5001" `
  -UserName "admin" `
  -Password "YOUR_PASSWORD" `
  -SkipCertificateCheck
```

لا تشغّل السكربت على بيانات إنتاج حقيقية؛ البيانات الناتجة تؤثر فعليًا في
المخزون وأرصدة الأطراف والخزائن والتقارير المحاسبية.
