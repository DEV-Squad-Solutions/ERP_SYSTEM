# دليل ربط SignalR بالفرونت في MiniErp

هذا الملف موجه لمطور الفرونت المسؤول عن التطبيق الفعلي. المشروع الموجود في
`F:\client\client` هو مشروع تجريبي لاختبار الـAPI، وليس مطلوبًا نسخه كما هو.

الهدف من هذا الدليل هو تنفيذ اتصال SignalR واحد في التطبيق، ثم تحديد الصفحات
التي تحتاج إلى تحديث بناءً على الـentities التي تغيرت في الباك إند.

## ملخص العقد مع الباك إند

| العنصر | القيمة |
|---|---|
| API أثناء التطوير | `https://localhost:7067/api/v1` |
| رابط SignalR | `https://localhost:7067/hubs/updates` |
| اسم الحدث | `entityChanged` |
| الحماية | JWT Access Token مرتبط بشركة واحدة |
| اتجاه الاتصال | الباك إند يرسل للفرونت؛ الفرونت لا يستدعي Hub Methods |

مهم جدًا: رابط SignalR لا يحتوي على `/api/v1`.

الرابط الصحيح:

```text
https://localhost:7067/hubs/updates
```

الرابط الخطأ:

```text
https://localhost:7067/api/v1/hubs/updates
```

## الفكرة الأساسية: Event واحد وليس Event لكل صفحة

كل النظام يستمع إلى حدث واحد فقط:

```text
entityChanged
```

الباك إند لا يعرف أسماء صفحات الفرونت، ولا يرسل حدثًا اسمه مثلًا
`invoicePageChanged`. بدلًا من ذلك يرسل أسماء الـentities التي تغيرت داخل
`changes`.

الفرونت يحتفظ بخريطة مركزية تربط كل `resource` بالمناطق أو الصفحات التي تعتمد
عليه:

```text
entityChanged
      ↓
resource = InvoiceLine
      ↓
resourceToAreas
      ↓
invoices + inventory + partnerStatement
      ↓
إعادة تحميل البيانات المطلوبة
```

لا تنشئ اتصال SignalR داخل كل صفحة. أنشئ اتصالًا واحدًا في جذر التطبيق بعد
تسجيل الدخول.

## 1. تثبيت المكتبة

```bash
npm install @microsoft/signalr
```

## 2. استخدام الـToken الصحيح

اتصال SignalR يحتاج إلى `accessToken` النهائي المرتبط بشركة واحدة.

لا تستخدم:

- `selectionToken`
- `refreshToken`
- Access Token خاص بشركة تم تغييرها

إذا كان المستخدم مرتبطًا بأكثر من شركة، نفذ أولًا:

```http
POST /api/v1/Auth/select-company
Content-Type: application/json

{
  "selectionToken": "...",
  "companyId": 2
}
```

بعد ذلك استخدم `accessToken` الموجود في الاستجابة.

كل اتصال يتم وضعه تلقائيًا في مجموعة الشركة الموجودة داخل الـToken، لذلك
مستخدمو شركة أخرى لن تصلهم نفس الأحداث.

## 3. شكل الرسالة القادمة من الباك إند

```ts
export type RealtimeAction = "Added" | "Updated" | "Deleted";

export type RealtimeEntityChange = {
  resource: string;
  action: RealtimeAction;
  entityId: string | null;
  storeIds: number[];
};

export type RealtimeChangeNotification = {
  eventId: string;
  occurredAtUtc: string;
  changes: RealtimeEntityChange[];
};
```

معنى الحقول:

- `eventId`: رقم فريد للرسالة، ويستخدم لمنع تنفيذ نفس الرسالة مرتين.
- `occurredAtUtc`: وقت حدوث التغيير بتوقيت UTC.
- `changes`: قائمة بكل الـentities التي تغيرت في نفس عملية الحفظ.
- `resource`: اسم Entity في الباك إند مثل `Invoice` أو `InvoiceLine`.
- `action`: نوع العملية: `Added` أو `Updated` أو `Deleted`.
- `entityId`: رقم الـentity كنص، وقد يكون `null` أثناء إضافة سجل جديد.
- `storeIds`: أرقام المخازن المرتبطة مباشرة بالتغيير، وقد تكون فارغة.

مثال عند تعديل فاتورة رقم `148`:

```json
{
  "eventId": "01ea72d5-670e-4a63-9046-80c16c42ba2b",
  "occurredAtUtc": "2026-08-08T11:25:43.120Z",
  "changes": [
    {
      "resource": "Invoice",
      "action": "Updated",
      "entityId": "148",
      "storeIds": [3]
    },
    {
      "resource": "InvoiceLine",
      "action": "Updated",
      "entityId": "552",
      "storeIds": []
    },
    {
      "resource": "ItemMovement",
      "action": "Added",
      "entityId": null,
      "storeIds": [3]
    }
  ]
}
```

عملية واحدة يمكن أن تغير أكثر من Entity. لذلك يجب فحص كل العناصر داخل
`notification.changes`، وليس أول عنصر فقط.

مهم: `entityId` الخاص بـ`InvoiceLine` هو رقم سطر الفاتورة، وليس رقم الفاتورة.
عند تغير Entity فرعية أعد تحميل الفاتورة الحالية من الـAPI بدل محاولة اعتبار
رقم الـentity الفرعية هو رقم الفاتورة.

## 4. إنشاء ملف الاتصال `realtime.ts`

```ts
import {
  HubConnection,
  HubConnectionBuilder,
  LogLevel
} from "@microsoft/signalr";

export type RealtimeAction = "Added" | "Updated" | "Deleted";

export type RealtimeEntityChange = {
  resource: string;
  action: RealtimeAction;
  entityId: string | null;
  storeIds: number[];
};

export type RealtimeChangeNotification = {
  eventId: string;
  occurredAtUtc: string;
  changes: RealtimeEntityChange[];
};

export function getRealtimeHubUrl(apiBase: string): string {
  const url = new URL(
    apiBase.replace(/\/+$/, ""),
    window.location.origin
  );

  url.pathname = "/hubs/updates";
  url.search = "";
  url.hash = "";
  return url.toString();
}

export function createRealtimeConnection(
  apiBase: string,
  accessToken: string
): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(getRealtimeHubUrl(apiBase), {
      // بدون كلمة Bearer؛ مكتبة SignalR تضيف الـToken بالطريقة الصحيحة.
      accessTokenFactory: () => accessToken,
      withCredentials: false
    })
    .withAutomaticReconnect([0, 2_000, 5_000, 10_000, 30_000])
    .configureLogging(LogLevel.Warning)
    .build();
}
```

## 5. تحديد الصفحات المتأثرة

أنشئ أسماء وظيفية للمناطق الموجودة في الفرونت. هذه ليست أسماء Events في
الباك إند؛ هي فقط أسماء داخلية لتنظيم إعادة التحميل في الفرونت.

```ts
export type RealtimeArea =
  | "invoices"
  | "items"
  | "stores"
  | "inventory"
  | "storeAvailability"
  | "partnerStatement"
  | "cashboxes"
  | "all";

export const resourceToAreas: Record<string, RealtimeArea[]> = {
  Invoice: [
    "invoices",
    "inventory",
    "partnerStatement"
  ],
  InvoiceLine: [
    "invoices",
    "inventory",
    "partnerStatement"
  ],
  InvoiceContainerLine: [
    "invoices",
    "inventory"
  ],
  InvoicePayment: [
    "invoices",
    "partnerStatement",
    "cashboxes"
  ],
  ItemMovement: [
    "inventory",
    "storeAvailability"
  ],
  ContainerMovement: [
    "inventory"
  ],
  BusinessPartnerMovement: [
    "partnerStatement"
  ],
  ItemStoreBalance: [
    "inventory",
    "storeAvailability"
  ],
  Item: [
    "items",
    "inventory",
    "storeAvailability"
  ],
  ItemUnit: [
    "items"
  ],
  ItemsCategory: [
    "items"
  ],
  Store: [
    "stores",
    "inventory",
    "storeAvailability"
  ],
  BusinessPartner: [
    "invoices",
    "partnerStatement"
  ],
  CashVoucher: [
    "partnerStatement",
    "cashboxes"
  ]
};
```

هذه الخريطة مثال ابتدائي ويجب تحديثها عند إضافة شاشة أو Entity جديدة.

إذا وصل `resource` غير موجود في الخريطة، نفذ Refresh شاملًا. هذا أكثر أمانًا
من تجاهل الحدث وعرض بيانات قديمة.

## 6. مثال React كامل: `RealtimeProvider.tsx`

هذا الـProvider ينفذ الآتي:

- ينشئ اتصالًا واحدًا فقط.
- يستمع إلى `entityChanged`.
- يمنع تكرار `eventId`.
- يجمع عدة رسائل خلال 300ms لتقليل عدد طلبات GET.
- يحول الـresources إلى مناطق متأثرة.
- يزيد رقم Version لكل منطقة.
- يعيد المحاولة إذا فشل الاتصال الأول.
- ينفذ Refresh شاملًا بعد إعادة الاتصال.

```tsx
import {
  createContext,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode
} from "react";
import { HubConnectionState } from "@microsoft/signalr";
import {
  createRealtimeConnection,
  type RealtimeChangeNotification
} from "./realtime";
import {
  resourceToAreas,
  type RealtimeArea
} from "./realtimeAreas";

type ConnectionStatus =
  | "disconnected"
  | "connecting"
  | "connected"
  | "reconnecting";

type RealtimeVersions = Record<RealtimeArea, number>;

type RealtimeContextValue = {
  versions: RealtimeVersions;
  status: ConnectionStatus;
};

const initialVersions: RealtimeVersions = {
  invoices: 0,
  items: 0,
  stores: 0,
  inventory: 0,
  storeAvailability: 0,
  partnerStatement: 0,
  cashboxes: 0,
  all: 0
};

const RealtimeContext = createContext<RealtimeContextValue | null>(null);

type RealtimeProviderProps = {
  apiBase: string;
  accessToken: string;
  children: ReactNode;
};

export function RealtimeProvider({
  apiBase,
  accessToken,
  children
}: RealtimeProviderProps) {
  const [versions, setVersions] = useState(initialVersions);
  const [status, setStatus] =
    useState<ConnectionStatus>("disconnected");

  useEffect(() => {
    let disposed = false;
    let flushTimer: number | undefined;
    let startRetryTimer: number | undefined;

    const seenEventIds = new Set<string>();
    const pendingAreas = new Set<RealtimeArea>();
    const connection = createRealtimeConnection(apiBase, accessToken);

    const flushAreas = () => {
      const areasToRefresh = new Set(pendingAreas);
      pendingAreas.clear();

      if (disposed || areasToRefresh.size === 0) return;

      setVersions(current => {
        const next = { ...current };

        if (areasToRefresh.has("all")) {
          next.all++;
          return next;
        }

        for (const area of areasToRefresh) {
          next[area]++;
        }

        return next;
      });
    };

    const scheduleFlush = () => {
      if (flushTimer !== undefined) {
        window.clearTimeout(flushTimer);
      }

      flushTimer = window.setTimeout(flushAreas, 300);
    };

    const requestFullRefresh = () => {
      pendingAreas.add("all");
      scheduleFlush();
    };

    const handleEntityChanged = (
      notification: RealtimeChangeNotification
    ) => {
      if (seenEventIds.has(notification.eventId)) return;

      seenEventIds.add(notification.eventId);
      if (seenEventIds.size > 500) {
        const oldestId = seenEventIds.values().next().value;
        if (oldestId) seenEventIds.delete(oldestId);
      }

      for (const change of notification.changes) {
        console.log(
          `Entity ${change.resource} was ${change.action}`,
          change.entityId
        );

        const affectedAreas = resourceToAreas[change.resource];

        if (!affectedAreas) {
          // Entity جديدة غير معروفة؛ Refresh شامل كحل آمن.
          pendingAreas.add("all");
          continue;
        }

        for (const area of affectedAreas) {
          pendingAreas.add(area);
        }
      }

      scheduleFlush();
    };

    let startConnection: () => Promise<void>;

    const scheduleStartRetry = () => {
      if (startRetryTimer !== undefined) {
        window.clearTimeout(startRetryTimer);
      }

      startRetryTimer = window.setTimeout(
        () => void startConnection(),
        5_000
      );
    };

    startConnection = async () => {
      if (
        disposed ||
        connection.state !== HubConnectionState.Disconnected
      ) {
        return;
      }

      setStatus("connecting");

      try {
        await connection.start();
        if (!disposed) setStatus("connected");
      } catch (error) {
        if (disposed) return;
        console.warn("فشل اتصال SignalR الأول.", error);
        setStatus("disconnected");
        scheduleStartRetry();
      }
    };

    // سجل الـhandler قبل start.
    connection.on("entityChanged", handleEntityChanged);

    connection.onreconnecting(() => {
      if (!disposed) setStatus("reconnecting");
    });

    connection.onreconnected(() => {
      if (disposed) return;
      setStatus("connected");

      // الأحداث التي أرسلت وقت انقطاع المتصفح لا يعاد إرسالها له.
      requestFullRefresh();
    });

    connection.onclose(error => {
      if (disposed) return;
      if (error) console.warn("تم إغلاق اتصال SignalR.", error);
      setStatus("disconnected");
      scheduleStartRetry();
    });

    void startConnection();

    return () => {
      disposed = true;

      if (flushTimer !== undefined) {
        window.clearTimeout(flushTimer);
      }
      if (startRetryTimer !== undefined) {
        window.clearTimeout(startRetryTimer);
      }

      connection.off("entityChanged", handleEntityChanged);
      void connection.stop();
    };
  }, [apiBase, accessToken]);

  const value = useMemo(
    () => ({ versions, status }),
    [versions, status]
  );

  return (
    <RealtimeContext.Provider value={value}>
      {children}
    </RealtimeContext.Provider>
  );
}

export function useRealtimeVersion(area: RealtimeArea): number {
  const context = useContext(RealtimeContext);

  if (!context) {
    throw new Error(
      "useRealtimeVersion must be used inside RealtimeProvider."
    );
  }

  if (area === "all") return context.versions.all;

  // زيادة all تعني أن كل الصفحات يجب أن تعيد التحميل.
  return context.versions[area] + context.versions.all;
}

export function useRealtimeStatus(): ConnectionStatus {
  const context = useContext(RealtimeContext);

  if (!context) {
    throw new Error(
      "useRealtimeStatus must be used inside RealtimeProvider."
    );
  }

  return context.status;
}
```

ضع `RealtimeArea` و`resourceToAreas` الموجودين في الخطوة السابقة داخل ملف
`realtimeAreas.ts`.

## 7. وضع الـProvider في جذر التطبيق

يجب تركيبه بعد نجاح تسجيل الدخول واختيار الشركة:

```tsx
function AuthenticatedApp({ apiBase, session }: Props) {
  return (
    <RealtimeProvider
      key={session.company.id}
      apiBase={apiBase}
      accessToken={session.accessToken}
    >
      <AppRoutes />
    </RealtimeProvider>
  );
}
```

استخدام `company.id` كـ`key` يضمن إزالة الاتصال القديم وإنشاء Provider جديد
عند تغيير الشركة.

عند تغيير الشركة يجب أيضًا مسح أي Cache تابع للشركة القديمة.

## 8. مثال صفحة الفواتير

صفحة الفواتير لا تنشئ اتصال SignalR ولا تستمع مباشرة إلى الحدث. هي فقط تحصل
على رقم Version الخاص بمنطقة `invoices`.

```tsx
import { useCallback, useEffect, useState } from "react";
import { useRealtimeVersion } from "../realtime/RealtimeProvider";

type Invoice = {
  id: number;
  invoiceNumber: string;
};

type InvoicePageProps = {
  apiBase: string;
  accessToken: string;
};

export function InvoicePage({
  apiBase,
  accessToken
}: InvoicePageProps) {
  const [invoices, setInvoices] = useState<Invoice[]>([]);
  const [loading, setLoading] = useState(false);
  const realtimeVersion = useRealtimeVersion("invoices");

  const loadInvoices = useCallback(async () => {
    setLoading(true);

    try {
      const response = await fetch(`${apiBase}/Invoices`, {
        headers: {
          Authorization: `Bearer ${accessToken}`
        }
      });

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
      }

      const result = await response.json();
      setInvoices(result.items ?? result);
    } finally {
      setLoading(false);
    }
  }, [apiBase, accessToken]);

  useEffect(() => {
    void loadInvoices();
  }, [loadInvoices, realtimeVersion]);

  return (
    <section>
      {loading && <p>جاري التحميل...</p>}

      {invoices.map(invoice => (
        <div key={invoice.id}>
          {invoice.invoiceNumber}
        </div>
      ))}
    </section>
  );
}
```

عندما تصل رسالة تحتوي على `Invoice` أو `InvoiceLine` أو أي resource مربوط
بمنطقة `invoices`، يزيد `realtimeVersion` فتعمل `loadInvoices` مرة أخرى.

تأكد أن `loadInvoices` ثابتة باستخدام `useCallback` حتى لا تحدث حلقة تحميل لا
نهائية.

## 9. الإضافة والتعديل والحذف تظل REST

لا تستدعِ SignalR يدويًا بعد الحفظ.

```ts
await fetch(`${apiBase}/Invoices`, {
  method: "POST",
  headers: {
    Authorization: `Bearer ${accessToken}`,
    "Content-Type": "application/json"
  },
  body: JSON.stringify(invoice)
});
```

بعد نجاح الحفظ في قاعدة البيانات، الباك إند يرسل `entityChanged` تلقائيًا لكل
المستخدمين المتصلين في نفس الشركة، بما فيهم المستخدم الذي نفذ الحفظ.

التصرف الأفضل في الشاشة التي نفذت الحفظ:

1. أظهر نجاح العملية فور نجاح REST request.
2. حدث الشاشة الحالية فورًا لتحسين تجربة المستخدم.
3. اقبل رسالة SignalR التالية لتحديث باقي التبويبات والمستخدمين.
4. استخدم Debounce حتى لا ترسل عدة GET requests متطابقة.

لا تستخدم:

```ts
connection.invoke("somethingChanged");
```

لا توجد Hub Method مطلوبة من الفرونت لهذا السيناريو.

## 10. استخدام `entityId` و`action`

إذا كانت الصفحة تعرض سجلًا محددًا، يمكن استخدام `entityId` لتحديد هل السجل
المفتوح هو الذي تغير:

```ts
const currentItemChanged = notification.changes.some(change =>
  change.resource === "Item" &&
  change.action === "Updated" &&
  change.entityId === String(openItemId)
);

if (currentItemChanged) {
  void loadItem(openItemId);
}
```

لكن في الشاشات والقوائم والتقارير المركبة، يفضل تحديث المنطقة كاملة لأن
التغيير قد يأتي من Entity فرعية.

`entityId` قد يكون `null` عند الإضافة، لذلك لا تعتمد على وجوده دائمًا.

## 11. فلترة المخزن

إذا كانت الصفحة تعرض بيانات مخزن واحد، يمكن الاستفادة من `storeIds`:

```ts
const affectsSelectedStore =
  change.storeIds.length === 0 ||
  change.storeIds.includes(selectedStoreId);
```

تعامل مع القائمة الفارغة على أنها قد تؤثر على الصفحة. بعض الـentities لا
تحتوي على `StoreId` مباشر، رغم أن تأثيرها قد يظهر في تقرير مخزن.

لا تستخدم `storeIds` للحماية أو الصلاحيات. الباك إند هو المسؤول عن صلاحيات
الوصول وعزل الشركات.

## 12. إعادة الاتصال والأحداث الفائتة

الـOutbox في الباك إند يعيد محاولة الإرسال عند فشل السيرفر في النشر، لكنه لا
يعيد تشغيل الأحداث للمتصفح الذي كان غير متصل وقت إرسالها.

لذلك بعد `onreconnected` يجب عمل Refresh شامل للبيانات الحالية.

أيضًا، `withAutomaticReconnect` يعالج انقطاع اتصال بدأ بنجاح، لكنه لا يكرر
أول `connection.start()` إلى ما لا نهاية. المثال السابق يحتوي على Retry لأول
اتصال كل خمس ثوانٍ.

احتمال وصول نفس `eventId` أكثر من مرة موجود، لذلك يجب منع التكرار مع الاحتفاظ
بعدد محدود من IDs حتى لا تزداد الذاكرة بلا حدود.

## 13. تغيير الـToken أو الشركة والخروج

عند تحديث الـAccess Token أو تغيير الشركة:

1. أوقف الاتصال القديم.
2. امسح Cache الشركة القديمة.
3. أنشئ اتصالًا جديدًا بالـToken الجديد.
4. أعد تحميل بيانات الشركة الجديدة.

عند Logout:

```ts
await connection.stop();
```

إزالة `RealtimeProvider` من React tree تنفذ `connection.stop()` تلقائيًا من
خلال Cleanup في المثال السابق.

## 14. اختبار التسليم

نفذ الخطوات التالية قبل اعتبار الربط مكتملًا:

1. شغل الـAPI والفرونت.
2. افتح نافذتين Browser أو Profile مختلفين.
3. سجل الدخول في النافذتين على نفس الشركة.
4. أضف سجلًا من النافذة A وتأكد أن النافذة B تحدثت.
5. عدل السجل من A وتأكد أن B تعرض القيم الجديدة.
6. احذف السجل من A وتأكد أنه اختفى من B.
7. كرر الاختبار على فاتورة وتأكد من تحديث القوائم والتقارير المرتبطة.
8. افصل Network عن B، نفذ تغييرًا في A، ثم أعد الاتصال وتأكد من Refresh شامل.
9. افتح نافذة ثالثة على شركة أخرى وتأكد أنها لا تستقبل بيانات الشركة الأولى.
10. حدث الـJWT وتأكد أن الاتصال يستخدم الـAccess Token الجديد.
11. نفذ Logout وتأكد أن الاتصال توقف.

## 15. مشاكل شائعة

| المشكلة | السبب المتوقع | الحل |
|---|---|---|
| `404` عند الاتصال | استخدام `/api/v1/hubs/updates` | استخدم `/hubs/updates` |
| `401` عند الاتصال | Token منتهي أو Selection/Refresh Token | استخدم Access Token النهائي للشركة |
| الاتصال يغلق فورًا | لا يوجد `company_id` صحيح في الـToken | أكمل اختيار الشركة ثم أعد الاتصال |
| أول اتصال لا يعاد | الاعتماد على `withAutomaticReconnect` فقط | أضف Retry حول أول `start()` |
| عاد الاتصال والبيانات قديمة | توقع إعادة إرسال الأحداث الفائتة | نفذ Refresh شامل في `onreconnected` |
| عدد GET requests كبير | كل Entity تسبب في Reload منفصل | استخدم Set وDebounce لمدة 300ms |
| وصلت الرسالة ولم تتغير الشاشة | الصفحة لا تعتمد على Version أو Query Key | اربط الصفحة بالمنطقة الصحيحة |
| ظهور بيانات الشركة القديمة | لم يتم مسح Cache عند تغيير الشركة | أوقف الاتصال وامسح Cache الشركة القديمة |

## Checklist نهائية

- [ ] تم تثبيت `@microsoft/signalr`.
- [ ] يوجد اتصال واحد فقط في جذر التطبيق.
- [ ] رابط الـHub هو `/hubs/updates`.
- [ ] الاتصال يستخدم Access Token نهائي مرتبط بالشركة.
- [ ] تم تسجيل `entityChanged` قبل `start()`.
- [ ] توجد خريطة مركزية `resourceToAreas`.
- [ ] أي resource غير معروف يؤدي إلى Refresh شامل.
- [ ] يتم منع تكرار `eventId`.
- [ ] يتم تجميع التحديثات باستخدام Debounce.
- [ ] الصفحات تستخدم Version أو Query Cache invalidation.
- [ ] أول اتصال يعاد عند الفشل.
- [ ] إعادة الاتصال تنفذ Refresh شامل.
- [ ] تغيير الشركة يوقف الاتصال القديم ويمسح الـCache.
- [ ] Logout يوقف الاتصال.
- [ ] اختبارات Add وUpdate وDelete وعزل الشركات ناجحة.

للمرجع الإنجليزي الكامل:
[`FRONTEND_SIGNALR_INTEGRATION_GUIDE.md`](FRONTEND_SIGNALR_INTEGRATION_GUIDE.md).

