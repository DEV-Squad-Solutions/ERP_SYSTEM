# SignalR للفرونت — المختصر المفيد

## المطلوب في سطر واحد

اعمل اتصال SignalR واحد بعد تسجيل الدخول، استمع لحدث واحد اسمه
`entityChanged`، واعمل Refresh للصفحات المتأثرة حسب `resource`.

## معلومات مهمة

```text
API:   https://localhost:7067/api/v1
Hub:   https://localhost:7067/hubs/updates
Event: entityChanged
```

مهم: رابط الـHub لا يحتوي على `/api/v1`.

## الخطوات

### 1. ثبت المكتبة

```bash
npm install @microsoft/signalr
```

### 2. استخدم الـAccess Token الصحيح

بعد Login واختيار الشركة، استخدم `accessToken` النهائي.

لا تستخدم `selectionToken` أو `refreshToken`.

### 3. اعمل اتصال واحد في جذر التطبيق

```ts
import { HubConnectionBuilder } from "@microsoft/signalr";

const connection = new HubConnectionBuilder()
  .withUrl("https://localhost:7067/hubs/updates", {
    accessTokenFactory: () => accessToken,
    withCredentials: false
  })
  .withAutomaticReconnect()
  .build();
```

لا تعمل اتصالًا جديدًا داخل كل صفحة.

### 4. استمع للحدث قبل تشغيل الاتصال

```ts
connection.on("entityChanged", notification => {
  handleRealtimeChange(notification);
});

await connection.start();
```

اسم الحدث ثابت لكل الصفحات: `entityChanged`.

### 5. اعرف ما الذي تغير

الباك إند يرسل:

```json
{
  "eventId": "...",
  "changes": [
    {
      "resource": "Invoice",
      "action": "Updated",
      "entityId": "148",
      "storeIds": [3]
    }
  ]
}
```

- `resource`: اسم الـEntity مثل `Invoice` أو `Item`.
- `action`: `Added` أو `Updated` أو `Deleted`.
- `entityId`: رقم السجل، وقد يكون `null` عند الإضافة.
- `storeIds`: المخازن المرتبطة بالتغيير، وقد تكون فارغة.

### 6. اربط الـEntity بالصفحات التي تحتاج Refresh

```ts
const resourceToPages: Record<string, string[]> = {
  Invoice: ["invoices", "inventory", "partnerStatement"],
  InvoiceLine: ["invoices", "inventory", "partnerStatement"],
  ItemMovement: ["inventory", "storeAvailability"],
  ItemStoreBalance: ["inventory", "storeAvailability"],
  Item: ["items", "inventory", "storeAvailability"],
  Store: ["stores", "inventory", "storeAvailability"]
};

function handleRealtimeChange(notification: any) {
  const pages = new Set<string>();

  for (const change of notification.changes) {
    const affectedPages = resourceToPages[change.resource];

    if (!affectedPages) {
      // Entity جديدة غير موجودة في الخريطة: Refresh شامل.
      pages.add("all");
      continue;
    }

    for (const page of affectedPages) {
      pages.add(page);
    }
  }

  for (const page of pages) {
    refreshPage(page);
  }
}
```

استخدم `Set` حتى لا تعمل Refresh لنفس الصفحة أكثر من مرة.

### 7. كل صفحة تعيد تحميل بياناتها

مثال React:

```tsx
const realtimeVersion = useRealtimeVersion("invoices");

useEffect(() => {
  void loadInvoices();
}, [loadInvoices, realtimeVersion]);
```

أو إذا كنت تستخدم React Query، نفذ `invalidateQueries` للصفحة المتأثرة.

### 8. الإضافة والتعديل والحذف تظل REST

احفظ البيانات بالطريقة العادية:

```ts
await apiRequest(apiBase, "/Invoices", {
  method: "POST",
  token: accessToken,
  body: invoice
});
```

لا تستدعِ SignalR بعد الحفظ. الباك إند يرسل الحدث تلقائيًا بعد نجاح العملية.

### 9. بعد Reconnect اعمل Refresh شامل

```ts
connection.onreconnected(() => {
  refreshPage("all");
});
```

الأحداث التي حدثت وقت انقطاع المتصفح لا يعاد إرسالها له.

### 10. عند تغيير الشركة أو Logout

عند تغيير الشركة:

1. أوقف الاتصال القديم.
2. امسح بيانات وCache الشركة القديمة.
3. أنشئ اتصالًا جديدًا بالـAccess Token الجديد.

عند Logout:

```ts
await connection.stop();
```

## اختبار سريع

1. افتح نافذتين على نفس الشركة.
2. أضف أو عدل أو احذف من النافذة الأولى.
3. تأكد أن النافذة الثانية عملت Refresh.
4. افتح نافذة على شركة أخرى وتأكد أنها لا تستقبل تغييرات الشركة الأولى.
5. افصل الإنترنت ثم أعده وتأكد من حدوث Refresh شامل.

## Checklist

- [ ] اتصال واحد فقط في جذر التطبيق.
- [ ] رابط الـHub هو `/hubs/updates`.
- [ ] استخدام `accessToken` النهائي.
- [ ] الاستماع إلى `entityChanged`.
- [ ] وجود خريطة `resourceToPages`.
- [ ] أي `resource` غير معروف يعمل Refresh شامل.
- [ ] منع Refresh المكرر باستخدام `Set` وDebounce.
- [ ] Refresh شامل بعد Reconnect.
- [ ] إيقاف الاتصال عند تغيير الشركة أو Logout.
- [ ] اختبار نافذتين على نفس الشركة وشركة مختلفة.

للتفاصيل والمثال الكامل:
[`FRONTEND_SIGNALR_GUIDE_AR.md`](FRONTEND_SIGNALR_GUIDE_AR.md).

