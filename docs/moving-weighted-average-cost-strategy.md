# استراتيجية تكلفة المتوسط المتحرك المرجّح (Moving Weighted Average Cost)

> **الغرض:** مرجع تصميم وتدقيق قابل لإعادة الاستخدام في أي ERP، مع خريطة للسلوك الموجود حاليًا في هذا المشروع.
>
> **نطاق الوثيقة:** مرجع محمول، مع توثيق التنفيذ بعد إصلاحات فرع `fix/inventory-costing-critical-issues`. كل فجوة باقية موسومة بوضوح كمخاطرة/توصية.

## 1. القرار المختصر

التكلفة المعتمدة يجب أن تكون Moving Weighted Average على مستوى المفتاح:

```text
(CompanyId, StoreId, ItemId)
```

وبعملة الشركة الأساسية (Base Currency). سعر الشراء أو سعر الفاتورة ليس هو متوسط التكلفة؛ بل هو **مصدر تكلفة وارد الحركة**. الحركة الخارجة تستخدم المتوسط السابق، ثم قد يعاد تقييمها عندما يصل وارد لاحق يغطي مخزونًا سالبًا أو صرفًا بلا تكلفة.

القاعدة التشغيلية المقترحة:

1. توجد حركة مخزون canonical واحدة لكل أثر كمية، ولا تسجل التقارير تكلفة مستقلة عنها.
2. يعاد تشغيل (replay) timeline الحركات بترتيب حتمي: `MovementDate`, ثم `CreatedOn`, ثم `Id` أو `StableSequence`.
3. يكتب محرك التكلفة snapshot على الحركة وحالة cost allocations، ثم يزامن قيود المخزون/تكلفة المبيعات داخل نفس المعاملة.
4. التقارير التشغيلية تعرض quantity، average cost، inventory value، COGS، وcost status من مصدر واحد، مع فصل صريح بين **التكلفة المعلومة صفرًا** و**التكلفة غير المعلومة**.
5. نختار محاسبيًا نموذج **Perpetual Inventory** عالميًا: كل وارد يزيد المخزون، وكل بيع/مرتجع بيع يرحل COGS/inventory، وكل مرتجع شراء يخفض المخزون بالقيمة المحتسبة. لا يجوز خلطه مع ترحيل شراء إلى Purchase Expense بلا تسوية واضحة.

## 2. التعريفات والنطاق

| المصطلح | التعريف |
|---|---|
| Transaction price | سعر المستند بعملة المعاملة، مثل سعر سطر الفاتورة قبل التحويل للعملة الأساسية. |
| Base unit cost | تكلفة وحدة الحركة بعد تحويلها إلى Base Currency، وتطبيع الوحدة (unit-of-measure). |
| Moving average | متوسط قيمة المخزون المتاح ÷ الكمية الموجبة بعد كل حركة واردة. |
| Inventory value | قيمة المخزون على المفتاح `(company, store, item)` بعد الحركة. |
| COGS | تكلفة الكمية الخارجة من المخزون وقت الخروج؛ في المتوسط المتحرك هي الكمية المغطاة × متوسط التكلفة السابق. |
| Advisory additional cost | تكلفة إضافية تعرض في بعض تقارير الربحية أو تسعير الصنف، لكنها لا تصبح landed cost تاريخية إلا إذا صممت حركة/تخصيص واضحًا لها. |
| Pending | خرج لم يجد واردًا يغطّي كامل الكمية، فلا توجد تكلفة نهائية لكل الوحدات. |
| PartiallyCosted | جزء من الخروج مغطى وجزء ما زال معلقًا. |
| Revalued | خرج كان معلقًا ثم غُطي بوارد لاحق؛ تغيرت تكلفته التاريخية. |
| Final | الحركة لها تكلفة نهائية ولا توجد كمية معلقة. |

كل الحسابات الكمية والقيمية تعمل في Base Currency، بينما يحتفظ المستند بتكلفة/سعر المعاملة وسعر الصرف الأصلي لغرض العرض والتسوية. يجب ألا يُستخدم سعر صرف جديد لإعادة كتابة تكلفة مخزون قديمة دون سياسة revaluation صريحة.

## 3. خريطة التنفيذ الحالي

### 3.1 النواة والقواعد

| المكوّن | السلوك المؤكد من الكود |
|---|---|
| [`InventoryCostRules`](../src/MiniErp.Domain/Entities/Inventory/InventoryCostRules.cs) | كمية scale=6، تكلفة الوحدة scale=8، القيمة scale=8، وكل التقريب `AwayFromZero`. يحسب total من quantity × unit cost، والمتوسط من value ÷ positive quantity. |
| [`ItemMovement`](../src/MiniErp.Domain/Entities/Inventory/ItemMovement.cs) | يحتفظ بالاتجاه، المرجع، التاريخ، `CostStatus`, `PendingCostQuantity`, `UnitCost`, `TotalCost`, `QuantityAfter`, `AverageCostAfter`, `InventoryValueAfter`. `ApplyCostSnapshot` يقرب القيم. |
| [`ItemStoreBalance`](../src/MiniErp.Domain/Entities/Inventory/ItemStoreBalance.cs) | snapshot واحد لكل company/store/item؛ عند quantity غير موجبة يصفر المتوسط والقيمة. لديه RowVersion. |
| [`InventoryCostAllocation`](../src/MiniErp.Domain/Entities/Inventory/InventoryCostAllocation.cs) | يربط outbound بوارد لاحق، بكمية وتكلفة وقيمة، ويمنع الكمية غير الموجبة والتكلفة السالبة. |
| [`ItemMovementConfiguration`](../src/MiniErp.Infrastructure/Persistence/Configurations/ItemMovementConfiguration.cs) | قيود SQL تمنع اتجاهين أو صفر حركة، وتمنع تكلفة/كمية معلقة خارج حدود الحركة. يوجد unique filtered index على `(CompanyId, MovementType, ReferenceId, ItemId)` للحركات الفعالة. |
| [`InventoryCostAllocationConfiguration`](../src/MiniErp.Infrastructure/Persistence/Configurations/InventoryCostAllocationConfiguration.cs) | unique على `(CompanyId, OutboundMovementId, InboundMovementId)` وفهارس الاتجاهين. |

### 3.2 محرك التكلفة

[`InventoryCostingService`](../src/MiniErp.Infrastructure/Services/Inventory/InventoryCostingService.cs) يقوم بالآتي (مؤكد من الكود):

- يقفل المفاتيح مرتبة حسب `StoreId` ثم `ItemId`. على SQL Server يستخدم `UPDLOCK, HOLDLOCK` على `ItemStoreBalances` داخل المعاملة.
- يعيد تشغيل كامل timeline للمفتاح، مرتبًا حسب `MovementDate`, `CreatedOn`, `Id`.
- يحذف allocations الموجودة للمفتاح ويبنيها من جديد.
- يوزع `Invoice.BaseTotal` الصافي على سطور الشراء بنسبة `InvoiceLine.BaseTotal` وبترتيب حتمي، ثم يحسب تكلفة وحدة الصنف؛ لذلك يدخل خصم رأس الفاتورة في تكلفة المخزون من دون تغيير `BaseUnitPrice` التاريخي للسطر.
- يعالج `SalesReturn` المرتبط بمصدر عبر تكلفة حركة البيع المصدر؛ وإذا كانت تكلفة المصدر معلقة يستخدم pending/replay ثم override عند توفر المتوسط المشتق. المرتجع غير المرتبط يستخدم المتوسط السابق إذا كان موجبًا، وإلا يتطلب `ReturnUnitCost`.
- يعالج الوارد المرجأ (SalesReturn المرتبط بمصدر معلق) كـ`Pending` ثم يعيد replay ثانياً بعد توفر override.
- يجعل الخروج المغطى `Final`، والخروج الجزئي `PartiallyCosted`، والخروج غير المغطى `Pending`. الوارد اللاحق يخصص FIFO إلى pending outbound ويحوّله إلى `Revalued` أو `PartiallyCosted`.
- يبني closure لتبعيات التحويلات، يقفل جميع المفاتيح بترتيب عالمي، ويعيد مزامنة `TransferIn` إلى `TransferOut` باستخدام cost fingerprint وحد تكرار متناسب مع حجم الـclosure، ثم يستدعي posting synchronizer للمفاتيح التي عولجت.
- قبل تثبيت أي snapshot متغير، يمرر تاريخ الحركة الأصلي والحالي إلى `IFiscalYearPeriodGuard`؛ لذلك لا يعيد وارد لاحق تقييم حركة داخل فترة مغلقة بصمت.

التخصيص FIFO هنا يخص **تغطية الصرف المعلق**، وليس تقييمًا FIFO للمخزون الموجب. المتوسط المتحرك يظل سياسة التقييم.

### 3.3 مصادر الحركة

| المصدر | الخدمة/الأنواع | أثر التكلفة الحالي |
|---|---|---|
| فواتير البيع والشراء والمرتجعات | [`InvoiceService`](../src/MiniErp.Infrastructure/Services/Invoices/InvoiceService.cs)، [`InvoiceService.SideEffects`](../src/MiniErp.Infrastructure/Services/Invoices/InvoiceService.SideEffects.cs)؛ Sales, SalesReturn, Purchase, PurchaseReturn | الشراء ومرتجع البيع واردان؛ البيع ومرتجع الشراء خارجان حسب `InvoiceMovementRules`. تكلفة الشراء هي نصيب الصنف من `Invoice.BaseTotal` الصافي، بينما يبقى `BaseUnitPrice` سعر السطر قبل توزيع خصم رأس الفاتورة. |
| الرصيد الافتتاحي | [`StockOpeningBalanceService`](../src/MiniErp.Infrastructure/Services/StockOpeningBalances/StockOpeningBalanceService.cs) | ينشئ `OpeningBalance` واردًا بسعر السطر، ويمكن تعديل/حذف المستند مع replay. |
| التسويات | [`StockAdjustmentService`](../src/MiniErp.Infrastructure/Services/StockAdjustments/StockAdjustmentService.cs) | `AdjustmentIncrease` وارد بسعر مطلوب؛ `AdjustmentDecrease` خارج بالمتوسط السابق. |
| التحويل | [`StockTransferService`](../src/MiniErp.Infrastructure/Services/StockTransfers/StockTransferService.cs) | ينشئ `TransferOut` في المصدر و`TransferIn` في الوجهة؛ تكلفة الوجهة تُنسخ من المصدر أثناء مزامنة التكلفة. |
| الجرد | [`InventoryCountService`](../src/MiniErp.Infrastructure/Services/InventoryCounts/InventoryCountService.cs) | يولد تسويتين: زيادة بتكلفة يحددها المستخدم ونقص بالمتوسط عبر `StockAdjustmentService`. الجرد غير المسوى لا ينشئ أثر تكلفة. |
| الأرصدة الافتتاحية القديمة | [`InventoryStockService`](../src/MiniErp.Infrastructure/Services/Inventory/InventoryStockService.cs) | كمية legacy تُضاف إذا لم توجد حركة OpeningBalance مقابلة؛ لا تدخل هذه الأسطر تلقائيًا في replay المالي. |

### 3.4 العملة والترحيل

- [`Invoice`](../src/MiniErp.Domain/Entities/Invoicing/Invoice.cs) يحتفظ بـ`Currency`, `ExchangeRate`, وحقول `Base*`. `ApplyExchangeRate` يحول السطر والإجمالي إلى العملة الأساسية.
- عند تحديث سعر صرف مع `UpdateLinkedTransactions=true`، يقوم [`ExchangeRateService`](../src/MiniErp.Infrastructure/Services/ExchangeRates/ExchangeRateService.cs) أولًا بتحديث القيم الأساسية للمستندات المرتبطة، ثم يعيد حساب مفاتيح تكلفة فواتير الشراء المتأثرة، وبعدها يستخدم [`ExchangeRatePostingSynchronizer`](../src/MiniErp.Infrastructure/Services/ExchangeRates/ExchangeRatePostingSynchronizer.cs) لمزامنة القيود. لذلك يمكن لهذا الخيار أن يعيد كتابة snapshots تكلفة تاريخية وCOGS لاحقًا عبر replay؛ أما `UpdateLinkedTransactions=false` فلا ينفذ هذا الـcascade.
- [`InvoicePostingService`](../src/MiniErp.Infrastructure/Services/Invoices/InvoicePostingService.cs) يطبق Perpetual Inventory على فواتير الأصناف: الشراء مدين مخزون/دائن مورد، ومرتجع الشراء مدين مورد/دائن مخزون بالقيمة الدفترية مع فرق تكلفة gain/loss، إضافة إلى قيود COGS/inventory للبيع ومرتجع البيع. إذا كانت تكلفة مرتجع الشراء Pending/PartiallyCosted يؤجل القيد بدل تسجيل فرق ربح وهمي، ثم يعيد cost synchronizer إنشاءه عند اكتمال التكلفة.
- [`InventoryPostingService`](../src/MiniErp.Infrastructure/Services/JournalEntries/InventoryPostingService.cs) يرحل opening/adjustment إلى المخزون وحساب المقابل، بينما لا توجد في `InvoicePostingService` خطوط inventory/COGS للشراء/مرتجع الشراء.

## 4. الصيغ وقواعد replay الحتمية

ليكن:

```text
q0       = quantity قبل الحركة
v0       = inventory value قبل الحركة
a0       = average cost قبل الحركة
qin      = inbound quantity
qout     = outbound quantity
c        = base unit cost للوارد
roundQ  = التقريب إلى 6 منازل
roundU  = التقريب إلى 8 منازل
roundV  = التقريب إلى 8 منازل
```

### 4.1 الوارد الموجب

عندما لا يكون المخزون سالبًا:

```text
q1 = roundQ(q0 + qin)
v1 = roundV(v0 + roundV(qin × c))
a1 = roundU(v1 / q1)       إذا q1 > 0
```

إذا كان `q0 <= 0` ووُجدت كمية خروج معلقة، يغطى pending أولًا بتكلفة `c`، ثم يبدأ المخزون الموجب من الكمية المتبقية وقيمتها. لا تُحمل قيمة الوارد كله إلى المخزون الموجب بعد تخصيص الجزء المغطي للخروج.

### 4.2 الخارج

```text
covered = roundQ(min(qout, max(q0, 0)))
pending = roundQ(qout - covered)
costed  = roundV(covered × a0)
q1      = roundQ(q0 - qout)
```

إذا بقي `q1 > 0`:

```text
a1 = a0
v1 = roundV(v0 - costed)
```

إذا أصبح `q1 <= 0`:

```text
a1 = 0
v1 = 0
```

حالة الحركة: `Final` إذا `pending=0`، و`Pending` إذا `covered=0`، و`PartiallyCosted` غير ذلك. تكلفة الوحدة في الخروج النهائي هي `a0`، وإلا تكون null حتى يكتمل التقييم.

### 4.3 التخصيص المستقبلي

يحتفظ replay بطابور pending outbound مرتب بالأقدمية. كل inbound لاحق:

```text
while availableInbound > 0 and pendingQueue not empty:
    x = min(availableInbound, pending.remaining)
    create allocation(outbound, inbound, x, inboundCost)
    pending.accumulatedCost += roundV(x × inboundCost)
    pending.remaining -= x
    if pending.remaining == 0:
        pending status = Revalued
```

إذا بقي جزء من pending، يبقى `PartiallyCosted`. لا يجوز إنشاء allocation بكمية صفر، ولا allocation بين حركتين مختلفتي الشركة/المخزن/الصنف.

### 4.4 الترتيب

ترتيب النظام الحالي هو:

```text
ORDER BY MovementDate ASC, CreatedOn ASC, Id ASC
```

النسخة المحمولة الأفضل تضيف `StableSequence` (تزايدي immutable) أو `EffectiveAt` بدقة كافية؛ لا تعتمد على ساعة الجهاز أو ترتيب SQL غير المحدد. يجب تسجيل فرق واضح بين ترتيب التكلفة وترتيب إنشاء المستند.

### 4.5 المرتجعات والتحويلات

- **مرتجع بيع مرتبط:** تكلفة الوارد هي تكلفة حركة البيع المصدر إذا كانت نهائية/معاد تقييمها؛ وإذا كانت معلقة ينتظر المصدر ثم يعاد replay. يجب التحقق من نفس الشركة/المخزن/الصنف، وأن المصدر يسبق المرتجع، وأن الكمية المرتجعة لا تتجاوز الكمية المباعة غير المرتجعة.
- **مرتجع بيع غير مرتبط:** يستخدم `averageCostBefore` متى كانت `quantityBefore > 0` حتى لو كان المتوسط صفرًا؛ وإذا لم توجد كمية موجبة يستخدم `ReturnUnitCost` أو يرفض العملية. يظل إضافة `CostSourceType=KnownZero` توصية تدقيقية مستقبلية.
- **مرتجع شراء:** هو خروج في الكود الحالي ويكلف بالمتوسط السابق. إذا كانت سياسة الأعمال تتطلب رد سعر المورد الأصلي، يجب حفظ source link/cost override صريح، لا استنتاجه من المتوسط.
- **TransferOut:** خروج من المصدر بالمتوسط السابق؛ `TransferIn` يأخذ cost snapshot الخارج. النقل لا يجب أن يولد ربحًا محاسبيًا.
- **TransferIn غير متزامن:** لا يسمح بتكلفة صفر صامتة إذا كان المصدر غير موجود أو مفقودًا؛ يُسجل dependency error أو pending.

### 4.6 التعديل والإضافة والحذف بأثر رجعي

أي إنشاء/تعديل/حذف يغيّر حركة بتاريخ أقدم أو يغيّر الكمية/السعر/المخزن/الصنف يعلّم المفتاح dirty ويعيد replay من أول boundary متأثر. إذا تغير المستند، يُحافظ على `MovementId` قدر الإمكان ويُحدّث snapshot والallocations والقيود التابعة داخل معاملة واحدة.

### 4.7 التقريب والـ residual

يجب تقريب كل كمية قبل المقارنة، وكل تكلفة وحدة قبل ضربها، وكل قيمة بعد الضرب والجمع. مع وجود فروق rounding:

```text
inventoryValueAfter ≈ sum(inboundCost) - sum(outboundCost)
```

ويجب ترحيل residual صغير إلى آخر allocation/سطر محدد deterministic بدل ترك عدم توازن عشوائي. امنع overflow قبل `quantity × unitCost`، واختبر الحدود القصوى لـ`decimal` والـ precision الخاص بقاعدة البيانات.

## 5. الحالات الخاصة وحالات التكلفة

| الحالة | الدلالة التشغيلية | ما يجوز للتقارير والمحاسبة |
|---|---|---|
| `Final` | كل الكمية الخارجة مغطاة أو الوارد له تكلفة صريحة | يظهر COGS/قيمة المخزون، ويدخل الربح النهائي. |
| `PartiallyCosted` | جزء من الخارج مغطى والجزء الباقي pending | اعرض cost known للجزء المغطى وpending quantity منفصلة؛ لا تعرض هامشًا نهائيًا كاملًا. |
| `Pending` | لا توجد تغطية تكلفة | اعرض القيمة المعلومة صفرًا إن كانت policy تسمح، لكن لا تساوِها بـunknown؛ لا تجعل financial readiness جاهزًا. |
| `Revalued` | تكلفة الخروج تغيرت بعد inbound لاحق | احتفظ بالقيمة الأصلية وقيمة revaluation/audit؛ حدّث COGS وقيد الفاتورة أو سجّل variance حسب سياسة الفترة. |
| كمية موجبة وتكلفة صفر | مخزون معروف التكلفة صفرًا، مثل هبة أو promotion | صالح فقط مع `CostSourceType=KnownZero`، ويظل reconciliation صحيحًا. |
| كمية سالبة | يسمح بها فقط في وضع stock-check `None` أو سياسة negative stock | يجب ألا تُخفى؛ تحفظ pending/negative value وتحدد سياسة إعادة التقييم والفترة المغلقة. |

## 6. تدقيق الحواف والمخاطر في التطبيق الحالي

الجدول التالي يفصل ما رأيناه في الكود عن أثره المحتمل. **Risk** لا تعني أن الخطأ وقع في كل البيانات، بل تعني أن التصميم الحالي يسمح به أو لا يثبت السياسة المطلوبة.

| الأولوية | الملاحظة | التصنيف والأثر | التوصية المحمولة |
|---|---|---|---|
| Fixed | كان وارد الشراء يستخدم `InvoiceLine.BaseUnitPrice` ولا يوزع خصم رأس الفاتورة. | **أُصلح في هذا الفرع.** يوزع `BaseTotal` الصافي proportional مع residual حتمي على آخر سطر. | شغّل backfill/reconciliation للبيانات التاريخية قبل الاعتماد المالي. |
| Fixed | كان ترحيل Purchase/PurchaseReturn يخلط periodic مع inventory subledger. | **أُصلح في هذا الفرع.** فواتير الأصناف تستخدم Perpetual Inventory، وفرق مرتجع الشراء يذهب إلى adjustment gain/loss. | راجع mapping الحسابات وشغّل backfill للقيود التاريخية. |
| Fixed | كان مرتجع البيع غير المرتبط يخلط المتوسط المعروف صفرًا مع التكلفة المجهولة. | **أُصلح سلوكيًا.** الكمية الموجبة تسمح بمتوسط صفر. | إضافة `CostSourceType` ما زالت تحسينًا تدقيقيًا مستقبليًا. |
| Fixed | كان `PendingCostQuantity` في تقرير التكلفة يستبعد inbound pending. | **أُصلح في هذا الفرع.** الملخص يجمع كل حركة pending. | يمكن إضافة breakdown inbound/outbound لاحقًا للعرض فقط. |
| Fixed | legacy opening lines قد توجد بلا canonical `OpeningBalance` movement. | **أُصلح في accounting readiness/backfill.** تظهر issue مانعة، ويُنشئ backfill الحركة المفقودة من `line.Quantity`. | يلزم تشغيل readiness/backfill ثم reconciliation على قواعد البيانات القائمة. |
| High | Additional item pricing expenses تقرأ حاليًا من `ItemPricingExpenses` عند تشغيل الربحية/توازن الصنف؛ ليست landed-cost snapshot تاريخية، ولا تدخل المخزون/GL. | **مؤكد.** تعديل الإعداد اليوم يعيد كتابة ربحية الماضي. | إما سمّها advisory بوضوح ولا تدخل reconciliation، أو أنشئ `LandedCostAllocation` بتاريخ/مرجع وقيد فعلي، ولا تعيد تفسير التاريخ. |
| Fixed | كان stock validation يرتب وارد نفس اليوم قبل الصادر بصرف النظر عن `CreatedOn, Id`. | **أُصلح في هذا الفرع.** التحقق وcost replay يستخدمان ترتيب التاريخ ثم الإنشاء ثم الهوية، مع أولوية مستقلة فقط للرصيد legacy. | يظل `StableSequence` immutable أفضل من وقت الإنشاء في النسخ المحمولة. |
| Fixed | كان وارد لاحق قادرًا على إعادة تقييم COGS داخل فترة مغلقة. | **أُصلح بالحظر.** أي snapshot تكلفة متغير يمر على period guard للتاريخ الأصلي والحالي. | سياسة current-period variance تظل تطويرًا مستقلًا إذا احتاج العمل إعادة التقييم بعد الإقفال بدل الرفض. |
| Fixed | كان transfer dependency propagation بلا closure/iteration guard صريح. | **أُصلح في هذا الفرع.** closure، global lock order، input fingerprint، وحد iterations. | راقب حجم closure وزمن replay في الإنتاج. |
| Fixed | كان `recalculatedKeys` لا يمنع إعادة معالجة transfer key بلا تغير. | **أُصلح بفصل posting set عن fingerprint المعالجة.** | لا يزال full replay مقصودًا للسلامة. |
| Medium | Full timeline replay يعيد كل حركات المفتاح ويحذف allocations ويبنيها من جديد. | **مؤكد + scalability risk.** يزداد الزمن والـlocks مع تاريخ طويل. | ابدأ بالـfull replay للسلامة؛ بعد قياس صحيح أضف checkpoint/dirty-from boundary مع hash/fingerprint واختبار equivalence. |
| Medium | `decimal` extreme quantity × unit cost قد يسبب overflow أو truncation عند حدود precision. | **Risk.** فشل runtime أو residual صامت. | Validate upper bounds قبل الحفظ، واحسب high precision/decimal context ثم round مرة واحدة وفق policy. |
| Medium | قيود SQL تمنع zero-direction في `ItemMovement`، لكن أي import/legacy path يجب أن يحافظ على ذلك. | **مؤكد كقيد DB + migration risk.** | اختبر bulk import وsoft-delete، وارفض zero-quantity بدل إسقاطها من التقرير. |
| Medium | unique الحركة مبني على `(company,type,reference,item)`؛ إعادة إنشاء نفس المستند أو تكرار source line قد يصطدم، لكن صحة المصدر/الفرع ليست uniqueness كاملة. | **مؤكد + duplicate/source risk.** | أضف source-line id أو operation id immutable، وفحصًا قبل الحفظ؛ لا تعتمد على reference number وحده. |
| Medium | وجود مصدر حركة مفقود يجعل `ResolveInboundUnitCostAsync` يفشل لبعض الواردات، بينما بعض legacy paths قد تترك movement بدون source. | **مؤكد في missing-source errors + data risk.** | اجعل source link إلزاميًا أو خزّن `ExplicitCost`; أنشئ exception queue قابلة للإصلاح لا قيمة صفر صامتة. |
| Medium | تجاوز المرتجع للكمية المباعة، cross-store/cross-company link، أو source بعد return يحتاج تحققًا دائمًا. | **حماية موجودة جزئيًا في linked returns + integrity risk.** | FK/unique/business validations على company/store/item/date والكمية المتبقية، مع اختبار الحذف/التعديل والـsoft-delete. |
| High | تعديل سعر الصرف مع `UpdateLinkedTransactions=true` يحدث `BaseUnitPrice` لفواتير الشراء، ثم يعيد حساب مفاتيح التكلفة ويزامن القيود؛ وقد تنتقل النتيجة إلى COGS تاريخي عبر replay. | **مؤكد من `ExchangeRateService` + closed-period risk.** قد تتغير تكلفة المخزون والربحية وفترة قديمة من تعديل rate واحد. | طبّق فحص الفترة المغلقة على كامل الـcascade، وسجل الأثر؛ بعد الإقفال استخدم مستند variance في الفترة الحالية بدل إعادة كتابة التاريخ، أو ارفض التحديث. |
| Medium | تعدد الوحدات يعتمد على `Count × Weight` وتطبيع `ItemUnit`; أي اختلاف في factor بين حركة قديمة وجديدة يغير الكمية. | **Risk.** mismatch كمية/تكلفة. | احفظ `BaseQuantity` وunit conversion version وقت الحركة، ولا تعيد حساب التاريخ من تعريف الوحدة الحالي. |
| Low | soft deletes تدخل في query filters وتستثنى في بعض locks/queries، ما قد يجعل replay/legacy enumeration مختلفًا عن التقارير. | **Risk.** اختلاف audit snapshot. | اجعل active timeline وaudit timeline منفصلين، وسجل deleted-at/source reason. |
| Low | `InventoryCostAllocation.CreatedOn` وقت replay وليس وقت الحركة الأصلية. | **مؤكد من factory + audit limitation.** | احفظ `EffectiveAt` للحساب و`GeneratedAt` للـallocation، ولا تستخدم GeneratedAt للترتيب المحاسبي. |

## 7. المعاملة، التزامن، الإعادة، والإيديمبوتنسي

### 7.1 قواعد المعاملة

عملية المستند التي تؤثر على التكلفة يجب أن تكون atomic:

```text
begin Serializable transaction
  validate company/store/item/date/links/fiscal policy
  lock all old + new costing keys in global order
  persist document and canonical movements (stable IDs)
  replay affected keys and persist snapshots/allocations
  synchronize inventory/COGS journal sources
  run reconciliation assertions
commit
```

إذا فشل replay أو posting أو fiscal-year check: rollback ثم clear tracked state. لا تعرض مستندًا محفوظًا دون snapshot/قيد معرّف بالحالة.

### 7.2 ترتيب الأقفال ومنع deadlock

استخدم ترتيبًا عالميًا `(CompanyId, StoreId, ItemId)`، ثم source/destination transfer dependency، ثم document row. لا تقفل source في ترتيب الطلب وdestination في ترتيب مختلف. معاملات متعددة الأطراف يجب أن تبني union للمفاتيح وتقفلها مرة واحدة.

### 7.3 الإعادة والإيديمبوتنسي

- لكل canonical movement هوية ثابتة `(SourceType, SourceId, SourceLineId, MovementRole)` أو `MovementId` لا تتغير عند تعديل السعر.
- لكل journal source key فريد، مثل `(CompanyId, JournalEntrySourceType, SourceId)`؛ `CreateOrUpdate` يستبدل نفس المصدر ولا يضيف قيدًا ثانيًا.
- replay يعيد allocations deterministically ويستخدم unique key للزوج outbound/inbound.
- Job إعادة التقييم يحمل `ReplayRunId`, version، وcheckpoint؛ يستطيع إعادة التشغيل بعد timeout دون duplication.
- `recalculatedKeys` ليست وحدها idempotency؛ يجب التحقق من cost fingerprint قبل إصدار posting.

### 7.4 الفترات المغلقة والـaudit

لا تسمح بتغيير حركة مؤثرة في fiscal period مغلق دون واحدة من السياسات الثلاث المذكورة. احتفظ بـ:

- original cost/status/value،
- revalued cost/status/value،
- reason، user، timestamp، source document، replay run،
- journal adjustment/variance source عند اختلاف الفترة.

التعديل الوظيفي لا يمس history بصمت؛ سجل correction event أو مستند عكسي.

## 8. مصفوفة التقارير والأثر المحاسبي

المبدأ: **الحركة/التكلفة هي مصدر التكلفة، والقيد هو مصدر القيمة المحاسبية، والتقرير يقرأ الاثنين مع reconciliation.**

| التقرير/الاستخدام | مصدر الحقيقة الحالي/المقترح | الحقول الأساسية | Pending/Partial/Revalued | اختبار المصالحة |
|---|---|---|---|---|
| Inventory cost report | `ItemMovement` + `InventoryCostAllocation` + `ItemStoreBalance`؛ [`InventoryCostReportService`](../src/MiniErp.Infrastructure/Services/Inventory/InventoryCostReportService.cs) | date, type, qty in/out, unit cost, total cost, qty/value after, allocations | يعرض status وpending وحركة revalued، ويجمع pending inbound وoutbound في summary. | opening value + inbound cost − outbound cost = closing value لكل key. |
| Stock balance report | كمية من [`InventoryStockService`](../src/MiniErp.Infrastructure/Services/Inventory/InventoryStockService.cs)، تكلفة من `GetSnapshotsAsync` | balance, average cost, inventory value, base currency | يجب أن يظهر `CostStatus`/unknown flag عند وجود pending؛ الحالي يعرض snapshot دون status صريح في row. | quantity report = sum canonical quantity + legacy bridge فقط أثناء migration؛ value = ItemStoreBalance. |
| Invoice item balance/pricing | [`InvoiceInventoryService.GetItemBalanceAsync`](../src/MiniErp.Infrastructure/Services/Invoices/InvoiceInventoryService.cs) | balance, average cost, inventory value, pricing expenses, total advisory cost | current pricing expenses تظهر منفصلة، ولا ينبغي تسميتها landed cost. | item balance as-of = stock timeline excluding current invoice؛ average/value من نفس as-of movement. |
| Invoice details/item cost | [`InvoiceQueryService`](../src/MiniErp.Infrastructure/Services/Invoices/InvoiceQueryService.cs) | `CostStatus`, pending qty, unit/total cost, average after, inventory value after | `Pending`/`Partial` يمنع cost final؛ `Revalued` يظهر كإعادة تقييم. | line total cost = matching movement total cost؛ missing movement = data issue لا zero. |
| Return-source UI | [`InvoiceQueryService.ReturnSources`](../src/MiniErp.Infrastructure/Services/Invoices/InvoiceQueryService.ReturnSources.cs) | source line, original qty, returned qty, available qty, source cost/status | source pending يفرض policy: disabled/pending or explicit cost; لا يرجع سعر صفر كأنه known. | available = sold quantity − active linked returns، بنفس company/store/item. |
| Invoice profitability | [`ProfitabilityReportService`](../src/MiniErp.Infrastructure/Services/ProfitabilityReports/ProfitabilityReportService.cs) | base revenue, discount allocation, inventory recognized cost, advisory additional cost, status, profit | لا يحسب gross profit النهائي إذا line غير final؛ additional expense الحالي advisory وقد يغيّر التاريخ. | sales/returns recognized cost = movement cost signed؛ net profit = net revenue − recognized cost. |
| Item profitability | نفس الخدمة، `BuildItem` | sales/return/net qty, average cost, total cost, COGS, profit | group status aggregation: pending/partial/revalued؛ average null إذا cost unknown. | net quantity and signed costs reconcile to invoice lines and movement totals. |
| Dashboard inventory | [`DashboardService`](../src/MiniErp.Infrastructure/Services/Dashboard/DashboardService.cs) | total ItemStoreBalances inventory value, items with stock, pending movement count | الحالي يعد `PendingCostQuantity > 0` دون فصل inbound/outbound؛ يجب أن يوضح unknown/revalued. | dashboard inventory value = sum active ItemStoreBalances = stock report total. |
| Accounting readiness | [`AccountingReadinessService`](../src/MiniErp.Infrastructure/Services/AccountingReadiness/AccountingReadinessService.cs) | pending/partial movement issues, missing/duplicate/unbalanced journals, mappings | أي pending/partial داخل الفترة blocking؛ revalued يحتاج variance/fiscal policy. | ready فقط إذا لا pending، القيود متوازنة، sources/journal unique، mappings مكتملة. |
| Financial statements | [`FinancialStatementService.FinancialReports`](../src/MiniErp.Infrastructure/Services/Statements/FinancialStatementService.FinancialReports.cs) | journal debit/credit, account mappings, readiness flag | القوائم تقرأ JournalEntryLines؛ cost pending يجعل `IsReadyForReporting=false` حاليًا. | Trial balance balanced؛ inventory GL = inventory subledger بعد اختيار perpetual/periodic واحد. |
| Automatic invoice journals | [`InvoicePostingService`](../src/MiniErp.Infrastructure/Services/Invoices/InvoicePostingService.cs) | invoice amount in base/transaction currency, partner, payments, Sales/SalesReturn cost lines | الحالي يضيف COGS/inventory لـSales/SalesReturn فقط؛ purchase mismatch risk. | لكل source قيد واحد متوازن؛ cost line = movement total cost signed. |
| Opening/adjustment journals | [`InventoryPostingService`](../src/MiniErp.Infrastructure/Services/JournalEntries/InventoryPostingService.cs) | inventory value, opening equity, adjustment gain/loss | amount <= 0 يحذف قيد المصدر؛ known zero يجب أن يكون قرارًا واضحًا. | opening/adjustment movement cost = journal inventory line. |
| Transfer response | [`StockTransferService`](../src/MiniErp.Infrastructure/Services/StockTransfers/StockTransferService.cs) | source/destination qty/unit cost/status/value after | destination pending إذا dependency source غير محسومة؛ لا transfer gain/loss. | source out cost = destination in cost، quantity and item/store pair identical. |

### 8.1 عقود التقرير المقترحة

كل response يعتمد contract موحدًا:

```text
CostStatus: Final | PartiallyCosted | Pending | Revalued
CostKnown: bool
CostSourceType: Purchase | Opening | Adjustment | Transfer | SalesSource | Explicit | KnownZero | Unknown
PendingCostQuantity: decimal
UnitCostBase: decimal?
TotalCostBase: decimal
OriginalTotalCostBase: decimal?
RevaluedTotalCostBase: decimal?
InventoryValueAfterBase: decimal
CostAsOf: EffectiveAt
```

لا يعيد endpoint `0` عندما تكون القيمة غير معروفة إلا مع `CostKnown=false` أو `CostSourceType=KnownZero`.

## 9. نموذج المحاسبة: الاختيار والتسوية

### 9.1 النموذجان الممكنان

**Perpetual Inventory (الموصى به):**

```text
Purchase:       Dr Inventory          Cr Supplier
PurchaseReturn: Dr Supplier           Cr Inventory
Sale:           Dr Customer/Cash      Cr Sales Revenue
                Dr COGS               Cr Inventory
SalesReturn:    Dr Sales Return       Cr Customer/Cash
                Dr Inventory          Cr COGS
Adjustment+:    Dr Inventory          Cr Inventory Gain/Equity
Adjustment-:    Dr Inventory Loss      Cr Inventory
Transfer:       لا ربح؛ نقل قيمة بين stores
```

**Periodic Inventory:**

يُرحل الشراء إلى Purchase/Expense، ولا تسجل inventory/COGS لكل حركة؛ تُحسب closing inventory وCOGS عند الإقفال. هذا النموذج يتطلب count/valuation journal مركزيًا.

### 9.2 القرار للمشروع

تم اعتماد **Perpetual** لفواتير الأصناف في هذا الفرع لأن النظام يبني `ItemMovement`, `ItemStoreBalance`, allocations، وcosting snapshots لحظيًا، ولأن تقارير الربحية تعتمد على COGS لكل فاتورة. يلزم تشغيل backfill ثم reconciliation يومي للبيانات القديمة:

```text
GL Inventory balance
  = Σ ItemStoreBalance.InventoryValue
  ± approved revaluation/rounding/period-variance journals
```

أي فرق غير مصرح به يوقف accounting readiness.

## 10. خوارزمية محمولة (Pseudocode)

### 10.1 canonical movement

```text
Movement {
  CompanyId, StoreId, ItemId, ItemUnitId
  MovementId, SourceType, SourceId, SourceLineId, MovementRole
  EffectiveAt, StableSequence, CreatedOn
  QuantityIn, QuantityOut, BaseUnitCostInput?
  CostSourceType, ReturnSourceMovementId?
  CostStatus, PendingCostQuantity
  UnitCostBase?, OriginalUnitCostBase?, RevaluedUnitCostBase?
  TotalCostBase, QuantityAfter, AverageCostAfter, InventoryValueAfter
  IsDeleted, RowVersion
}
```

### 10.2 replay

```text
replay(key, movements):
  assert all movement keys == key
  sort by EffectiveAt, StableSequence, MovementId
  q = 0; value = 0; avg = 0
  pending = FIFO queue
  for m in movements:
    normalize quantity/unit conversion
    if m.in > 0:
      cost = resolveInboundCost(m, source links, explicit policy)
      if cost is Unknown:
        add known quantity with CostStatus=Pending
        continue
      allocateToPending(m.in, cost, pending)
      q += m.in
      value = value + m.in * cost - allocationsToPendingValue
      avg = q > 0 ? roundU(value / q) : 0
      snapshot m as Final (or revalue affected outbounds)
    else if m.out > 0:
      covered = min(max(q, 0), m.out)
      m.cost = covered * avg
      m.pending = m.out - covered
      q -= m.out
      if q <= 0: q=roundQ(q); value=0; avg=0
      else: value -= m.cost; avg stays previous
      enqueue pending remainder
  persist snapshots and allocations deterministically
  return closing(q, avg, value), affected movements, dirty period
```

### 10.3 linked returns and source edits

```text
validateReturn(source, return):
  same company/store/item and source date <= return date
  active returned qty <= sold qty
  source line cannot be removed/replaced below returned quantity

on source price/cost edit:
  preserve source movement identity
  update linked return financial source only if policy says return price follows source
  replay affected source and return keys
  synchronize partner/return journal and COGS
  if closed period: create current-period variance or reject
```

## 11. المراقبة ومعادلات المصالحة

راقب يوميًا وعلى مستوى الشركة/المخزن/الصنف:

```text
Quantity equation:
  closingQty = openingQty + Σ quantityIn - Σ quantityOut

Value equation (after rounding policy):
  closingValue = openingValue + Σ inboundCost - Σ outboundCost

Average equation:
  closingAvg = closingQty > 0 ? closingValue / closingQty : 0

Allocation equation:
  Σ allocation.qty per outbound <= outbound.pending/original qty
  Σ allocation.qty per inbound <= inbound quantity available

Accounting equation:
  GL inventory = subledger inventory ± approved variance
  GL COGS = Σ signed Final/Revalued sales movement costs ± approved variance
```

Metrics مقترحة:

- `pending_cost_quantity_by_direction`, `pending_movement_count`, `revalued_value_by_period`.
- `replay_duration`, `movements_replayed`, `allocation_count`, `dependency_iterations`.
- `gl_subledger_inventory_delta`, `gl_subledger_cogs_delta`, `missing_source_count`, `duplicate_movement_count`.
- عمر أقدم pending، وعدد الحركات التي غيرت فترة مغلقة.

## 12. مصفوفة الاختبارات الشاملة

### 12.1 Unit tests

- inbound واحد، inbound بسعرين، outbound داخل الرصيد، outbound إلى صفر، negative stock.
- zero-cost known مقابل unknown cost.
- rounding عند 6/8 منازل وresidual على آخر allocation.
- pending outbound يغطيه inbound واحد أو عدة inbounds FIFO.
- linked/unlinked sales return، purchase return، transfer source/destination.
- source line discount/quantity/price update مع linked return.
- decimal boundaries وzero quantity rejection.

### 12.2 Integration/database

- create/update/delete backdated لكل مصدر حركة يعيد كل snapshots والallocations.
- legacy opening line مع/بدون canonical OpeningBalance.
- inventory count يولد adjustment صحيح التكلفة والقيد.
- journal source idempotency: retry لا ينشئ duplicate.
- exchange-rate update مع `UpdateLinkedTransactions=true` يحدث القيم الأساسية، ويعيد تكلفة مفاتيح مشتريات متأثرة، ثم يزامن القيود؛ اختبر المنع/variance عند عبور فترة مغلقة. ومع `false` لا يحدث cascade للمستندات المرتبطة.
- fiscal closed: reject أو current-period variance وفق القرار.

### 12.3 Concurrency

- طلبان يعدلان نفس `(store,item)`؛ واحد ينجح والثاني RowVersion/concurrency error.
- transfer متقابل بين stores، مع lock order ثابت.
- replay job وinvoice edit متزامنان؛ لا double allocation ولا deadlock.
- cancellation/timeout بعد movement save وقبل posting؛ rollback كامل.

### 12.4 Property-based

لأي timeline صالح:

- الكمية بعد كل حركة تساوي cumulative in-out.
- لا allocation سالبة أو زائدة.
- إذا quantity <= 0 فـaverage/value = 0.
- إعادة replay مرتين تنتج نفس snapshots/allocations/journal fingerprint.
- تغيير حركة أقدم لا يؤثر على مفتاح آخر غير dependent transfer.

### 12.5 Reconciliation and reports

- Inventory cost report summary = ItemStoreBalance snapshot.
- stock report quantity/value = canonical timeline.
- invoice/item profitability cost = signed movement cost.
- dashboard value = sum balances.
- accounting readiness يمنع pending/partial وmissing/duplicate/unbalanced journals.
- financial statements trial balance وinventory/COGS GL يطابقان subledger وفق perpetual policy.

## 13. خطة تبنٍ قابلة للنقل إلى مشروع آخر

1. **Discovery:** احصر كل مصادر الكمية، العملات، الوحدات، soft deletes، والقيود المحاسبية. حدد هل النظام perpetual أم periodic.
2. **Canonical schema:** أضف movement identity/source link/effective sequence/cost source/status/original-revalued values.
3. **Normalize units/currency:** خزّن BaseQuantity وBaseUnitCost وقت الحركة، مع exchange-rate id/version.
4. **Build pure engine:** اختبر replay خارج قاعدة البيانات على timelines صغيرة قبل ربط EF/SQL.
5. **Persist snapshots:** ItemStoreBalance وmovement snapshot وallocations مع unique keys وrow version.
6. **Integrate writes:** كل create/update/delete يحدد dirty keys، يقفلها بترتيب ثابت، ثم replay وposting داخل transaction.
7. **Integrate reports:** استخدم cost contract الموحد، وافصل unknown/known-zero، وأضف statuses/reconciliation fields.
8. **Choose accounting model:** طبّق mapping واحدًا على purchase/return/sale/adjustment/transfer، ثم backfill journals قبل cutover.
9. **Migration:** أنشئ OpeningBalance movements من legacy quantities/values، اربط كل source line، وحقق equations قبل فتح الإنتاج.
10. **Closed periods:** ثبّت سياسة revaluation وdocument current-period variance.
11. **Observability:** فعل metrics/alerts وdaily reconciliation job.
12. **Load testing:** قس full replay أولًا؛ أضف checkpoints فقط بعد إثبات equivalence.
13. **Rollout:** shadow replay، compare reports/GL، ثم feature flag، ثم منع المسارات القديمة.
14. **Governance:** لا تعدّل cost snapshots يدويًا؛ أي correction يكون مستندًا عكسيًا أو replay run موثقًا.

## 14. حالة تنفيذ الإصلاحات وما تبقى

### P0 — نُفذ في هذا الفرع

1. اعتماد Perpetual لفواتير الأصناف وربط Purchase/PurchaseReturn بالمخزون وفرق التكلفة.
2. توزيع خصم رأس فاتورة الشراء على التكلفة الصافية للصنف مع residual حتمي.
3. تضمين inbound pending في `PendingCostQuantity` لتقرير تكلفة المخزون.
4. كشف legacy opening المفقود وإنشاء canonical movements أثناء accounting backfill.
5. منع replay من تغيير snapshots داخل فترة مغلقة.

### P1 — نُفذ في هذا الفرع

1. توحيد ترتيب stock validation مع cost replay على `MovementDate, CreatedOn, Id`، مع إبقاء legacy opening أولًا.
2. إضافة transfer dependency closure وglobal locking وfingerprint وحد iterations.
3. قبول known-zero average للمرتجع غير المرتبط متى كانت الكمية السابقة موجبة.
4. إضافة اختبارات لسلسلة التحويلات، الفترة المغلقة، خصم الشراء، pending inbound، والترتيب في اليوم نفسه.

### P2 — تحسينات مستقبلية باقية

1. إضافة `StableSequence` immutable بدل الاعتماد على `CreatedOn` كترتيب مستدام.
2. حفظ original/revalued cost وreplay run/audit metadata و`CostSourceType` الصريح.
3. إضافة daily GL/subledger reconciliation وmetrics لعمر pending وrevaluation.
4. بعد قياس الأداء، تصميم checkpoints/dirty-from boundary مع property test يثبت تطابقه مع full replay.
5. إبقاء additional item pricing expenses على مستوى الصنف كتكلفة advisory؛ ولا تحول إلى valuation/GL إلا عبر landed-cost allocation موثق.
6. تثبيت `SourceLineId` و`MovementRole` كهوية canonical بدل الاعتماد على `(type,reference,item)` فقط.

## 15. ملاحظات التدقيق المؤكد مقابل التوصيات

### سلوك مؤكد من الكود

- التكلفة والتقريب والـtimeline والحالات الأربع كما في الأقسام السابقة.
- `InventoryCostingService` يعيد replay كاملًا ويحذف allocations للمفتاح ثم يبنيها من جديد.
- purchase inbound يأخذ نصيبه النسبي من `Invoice.BaseTotal` الصافي، مع residual على آخر سطر، ولا يغيّر `BaseUnitPrice` المعروض.
- item purchase/purchase-return يستخدمان Inventory mapping؛ مرتجع الشراء يخرج بالقيمة الدفترية ويسجل الفرق في InventoryAdjustmentGain/Loss.
- `InventoryStockService` لديه مسار legacy opening quantity منفصل عن cost snapshots.
- `ProfitabilityReportService` يضيف `ItemPricingExpenses` وقت القراءة، ويمنع gross profit النهائي للحركة غير النهائية.
- `AccountingReadinessService` وfinancial statement readiness يعتبران Pending/PartiallyCosted عائقًا.

### سياسات لا يثبتها الكود ويجب اعتمادها إداريًا

- هل خصم الشراء جزء من landed cost؟
- هل يسمح negative stock؟ وكيف يعالج بعد إقفال الفترة؟
- هل مرتجع الشراء يعكس سعر المورد أم المتوسط؟
- هل additional pricing expenses advisory أم valuation/GL؟
- هل revaluation التاريخي يعاد في نفس الفترة أم variance في الفترة الحالية؟
- هل `StableSequence` يساوي ترتيب الإنشاء أم posting sequence أم وقت فعلي يختاره المستخدم؟

هذه القرارات يجب أن تصبح configuration/versioned policy، لا سلوكًا ضمنيًا في خدمة واحدة.
