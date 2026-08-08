# MiniErp SignalR: Practical Frontend Steps

This is the short implementation checklist for the production frontend
developer. For the complete contract and production React example, see
[`FRONTEND_SIGNALR_INTEGRATION_GUIDE.md`](FRONTEND_SIGNALR_INTEGRATION_GUIDE.md).

## 1. Install SignalR

```bash
npm install @microsoft/signalr
```

## 2. Obtain the correct token

Complete login and company selection first. Connect using only the final
company-scoped `accessToken`.

Do not use:

- `selectionToken`
- `refreshToken`
- A token from a previously selected company

## 3. Create one application-level connection

Use one connection for the authenticated application. Do not create a separate
connection for every page or component.

```ts
import {
  HubConnectionBuilder,
  LogLevel
} from "@microsoft/signalr";

const connection = new HubConnectionBuilder()
  .withUrl("https://localhost:7067/hubs/updates", {
    accessTokenFactory: () => accessToken,
    withCredentials: false
  })
  .withAutomaticReconnect([0, 2_000, 5_000, 10_000, 30_000])
  .configureLogging(LogLevel.Warning)
  .build();
```

The hub path is:

```text
/hubs/updates
```

It is not `/api/v1/hubs/updates`.

## 4. Register the event and start the connection

Register `entityChanged` before calling `start()`:

```ts
connection.on("entityChanged", notification => {
  requestDataRefresh(notification);
});

await connection.start();
```

The payload is:

```ts
type RealtimeNotification = {
  eventId: string;
  occurredAtUtc: string;
  changes: Array<{
    resource: string;
    action: "Added" | "Updated" | "Deleted";
    entityId: string | null;
    storeIds: number[];
  }>;
};
```

### How to know which entity changed

Inspect every item in `notification.changes`:

- `resource` is the entity class name, for example `Invoice`, `InvoiceLine`,
  `Item`, or `ItemMovement`.
- `action` is `Added`, `Updated`, or `Deleted`.
- `entityId` is the changed entity's key when available.
- `storeIds` identifies directly related stores when available.

Example event produced while updating invoice `148`:

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

Basic routing example:

```ts
const invoiceResources = new Set([
  "Invoice",
  "InvoiceLine",
  "InvoiceContainerLine",
  "InvoicePayment",
  "ItemMovement",
  "ContainerMovement",
  "BusinessPartnerMovement"
]);

connection.on(
  "entityChanged",
  (notification: RealtimeNotification) => {
    for (const change of notification.changes) {
      console.log(
        `Entity ${change.resource} was ${change.action}`,
        change.entityId
      );

      if (
        change.resource === "Item" &&
        change.action === "Updated" &&
        change.entityId === String(openItemId)
      ) {
        void loadItem(openItemId);
      }
    }

    const invoiceDataChanged = notification.changes.some(change =>
      invoiceResources.has(change.resource)
    );

    if (invoiceDataChanged) {
      void loadInvoices();

      if (openInvoiceId !== null) {
        void loadInvoice(openInvoiceId);
      }
    }
  }
);
```

Important: `InvoiceLine.entityId` is the line ID, not the invoice ID. Use the
`Invoice` change to identify the invoice when present. Otherwise reload the
current invoice or invoice list from the API. SignalR is an invalidation signal,
not the complete updated entity.

## 5. Refresh frontend data

When an event arrives:

1. Ignore it if its `eventId` was already handled.
2. Debounce events for about 300 milliseconds.
3. Increment a global revision or invalidate query-cache keys.
4. Reload authoritative data using the REST API.

Simple React page pattern:

```tsx
type PageProps = {
  realtimeRevision: number;
};

useEffect(() => {
  void loadRows();
}, [loadRows, realtimeRevision]);
```

Start with refreshing all currently mounted data. Add resource/store filtering
only if measurements show too many requests.

## 6. Keep mutations as REST calls

Continue creating, updating, and deleting through the normal REST API.

```ts
await apiRequest(apiBase, "/Invoices", {
  method: "POST",
  token: accessToken,
  body: invoice
});
```

Do not call `connection.invoke()` after saving or deleting. The backend
automatically broadcasts `entityChanged` after a successful database save.

Do not wait for SignalR before showing mutation success. Update the current
screen immediately, then also accept the SignalR refresh for other users and
tabs.

## 7. Handle reconnect correctly

`withAutomaticReconnect` handles a connection that drops after it has started.
Also add a retry around the first failed `connection.start()` call.

After `onreconnected`, perform a full data refresh:

```ts
connection.onreconnected(() => {
  refreshAllCurrentData();
});
```

Events sent while the browser was disconnected are not replayed.

## 8. Handle token, company, and logout changes

When the access token or selected company changes:

1. Stop the old connection.
2. Clear all company-specific frontend cache.
3. Create a new connection using the new access token.
4. Reload the selected company's data.

On logout:

```ts
await connection.stop();
```

## 9. Test before delivery

1. Open the frontend in two browsers or profiles.
2. Log both into the same company.
3. Add a record in browser A; browser B must refresh.
4. Update it in browser A; browser B must show the new values.
5. Delete it in browser A; browser B must remove it.
6. Disconnect browser B, make a change, reconnect it, and confirm a full
   refresh occurs.
7. Log another browser into a different company and confirm it does not receive
   or display the first company's data.
8. Refresh the JWT and confirm SignalR reconnects with the new access token.
9. Log out and confirm the connection stops.

## Completion checklist

- [ ] `@microsoft/signalr` is installed.
- [ ] Only one app-level connection is created.
- [ ] Hub URL is `/hubs/updates`.
- [ ] The final company-scoped access token is used.
- [ ] `entityChanged` is registered before `start()`.
- [ ] Duplicate `eventId` values are ignored.
- [ ] Refresh requests are debounced or batched.
- [ ] REST data reloads when realtime events arrive.
- [ ] Initial connection failures retry.
- [ ] Reconnect performs a full refresh.
- [ ] Company switching stops the old connection and clears its cache.
- [ ] Logout stops the connection.
- [ ] Same-company and different-company tests pass.
