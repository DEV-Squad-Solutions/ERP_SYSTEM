# MiniErp SignalR Frontend Integration Guide

This document is the frontend contract for MiniErp realtime updates. It is
intended for the production frontend developer. The React application under
`F:\client\client` is only a test client and is not required by this guide.

## 1. Contract summary

| Setting | Value |
|---|---|
| Development API base | `https://localhost:7067/api/v1` |
| SignalR hub URL | `https://localhost:7067/hubs/updates` |
| Client event name | `entityChanged` |
| Authentication | Company-scoped JWT access token |
| Server-to-client only | The frontend does not invoke hub methods |
| Tenant isolation | A connection receives events only for its token's company |

The hub URL is not under `/api/v1`. Given an API base URL, replace its path
with `/hubs/updates` instead of appending the hub path to it.

SignalR notifications are invalidation hints. After receiving one, fetch the
authoritative data from the REST API. Do not use the notification as a complete
replacement for the updated record.

## 2. What the backend publishes

The backend watches tracked business-entity changes saved through
`ApplicationDbContext`. An `Added`, `Updated`, or `Deleted` change creates a
company-scoped outbox message in the same database transaction. A background
dispatcher publishes the message to connected clients after the transaction
commits.

Soft deletion is published as `Deleted` when `IsDeleted` changes from `false`
to `true`.

The outbox gives durable server-side retry when dispatch fails. It does not
provide replay to a browser that was disconnected when an event was published.
For that reason, the frontend must perform a full refresh after reconnecting.

The mutating browser receives the same event as every other connected browser
in that company. The frontend must tolerate duplicate delivery and repeated
refresh requests.

## 3. Authentication requirement

The hub has the same JWT security requirements as protected REST endpoints.
Only a final access token containing one valid `company_id` can connect.

Do not use either of these values as the hub token:

- A company `selectionToken`.
- A `refreshToken`.

### Login with one company

Call:

```http
POST /api/v1/Auth/login
Content-Type: application/json

{
  "userName": "user",
  "password": "password"
}
```

When `requiresCompanySelection` is `false`, use the returned `accessToken` for
both REST requests and the SignalR connection.

### Login with multiple companies

When login returns `requiresCompanySelection: true`, display the returned
`companies` and send the selected company with the short-lived selection token:

```http
POST /api/v1/Auth/select-company
Content-Type: application/json

{
  "selectionToken": "...",
  "companyId": 2
}
```

Use the `accessToken` from this response to connect to SignalR.

### Refreshing or changing the token

Refresh the session through:

```http
POST /api/v1/Auth/refresh
Content-Type: application/json

{
  "refreshToken": "..."
}
```

The access-token provider used by SignalR must return the latest token. When
the selected company changes, stop the existing connection and create a new
one. Company group membership is assigned when the connection is established.

Stop the connection on logout and clear all company-specific cached data.

## 4. Event payload

The `entityChanged` handler receives this camel-case JSON shape:

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

Example:

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
      "action": "Added",
      "entityId": null,
      "storeIds": []
    }
  ]
}
```

Payload rules:

- `eventId` identifies one outbox event. Keep a bounded set of recently seen
  IDs and ignore duplicates.
- `occurredAtUtc` is an ISO-8601 UTC timestamp.
- `changes` can contain several related entities from one save operation.
- `resource` is the backend C# entity class name, such as `Invoice`,
  `InvoiceLine`, `Item`, or `Store`—not the REST endpoint name.
- `entityId` is a string because keys can have different types or be composite.
  It can be `null`, especially when an added entity still has a temporary
  database-generated key.
- `storeIds` contains positive values from direct entity properties whose names
  end in `StoreId`.
- An empty `storeIds` array means the event is not directly store-tagged. It
  does not prove that no store-based screen is affected.

## 5. Install the JavaScript client

For React, Vue, Angular, or plain browser TypeScript:

```bash
npm install @microsoft/signalr
```

Use one connection for the logged-in application shell. Do not create one
connection for every page, table, or component.

## 6. Framework-independent connection module

Create `realtime.ts` in the frontend:

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

export function buildRealtimeHubUrl(apiBase: string): string {
  const url = new URL(apiBase.replace(/\/+$/, ""), window.location.origin);
  url.pathname = "/hubs/updates";
  url.search = "";
  url.hash = "";
  return url.toString();
}

export function createRealtimeConnection(
  apiBase: string,
  getAccessToken: () => string | Promise<string>
): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(buildRealtimeHubUrl(apiBase), {
      accessTokenFactory: getAccessToken,
      withCredentials: false
    })
    .withAutomaticReconnect([0, 2_000, 5_000, 10_000, 30_000])
    .configureLogging(LogLevel.Warning)
    .build();
}
```

`accessTokenFactory` must return the raw token without a `Bearer ` prefix. The
SignalR client adds the token using the transport-appropriate mechanism.

Register event handlers before calling `connection.start()`.

`withAutomaticReconnect` handles a connection that drops after it started. It
does not continuously retry the first failed `start()` call, so the application
must retry initial startup separately.

## 7. Production-safe React integration

Call the following hook once in the authenticated application shell. Child
pages receive `revision`; they do not know about connection management.

```tsx
import { useEffect, useRef, useState } from "react";
import { HubConnectionState } from "@microsoft/signalr";
import {
  RealtimeChangeNotification,
  createRealtimeConnection
} from "./realtime";

type RealtimeStatus =
  | "disconnected"
  | "connecting"
  | "connected"
  | "reconnecting";

export function useMiniErpRealtime(
  apiBase: string,
  accessToken: string | null
) {
  const [revision, setRevision] = useState(0);
  const [status, setStatus] = useState<RealtimeStatus>("disconnected");
  const seenEventIds = useRef(new Set<string>());

  useEffect(() => {
    if (!accessToken) {
      setStatus("disconnected");
      return;
    }

    let disposed = false;
    let refreshTimer: number | undefined;
    let startRetryTimer: number | undefined;
    const connection = createRealtimeConnection(
      apiBase,
      () => accessToken
    );

    const requestRefresh = (
      notification?: RealtimeChangeNotification
    ) => {
      if (notification?.eventId) {
        if (seenEventIds.current.has(notification.eventId)) return;

        seenEventIds.current.add(notification.eventId);
        if (seenEventIds.current.size > 500) {
          const oldest = seenEventIds.current.values().next().value;
          if (oldest) seenEventIds.current.delete(oldest);
        }
      }

      if (refreshTimer !== undefined) {
        window.clearTimeout(refreshTimer);
      }

      refreshTimer = window.setTimeout(() => {
        if (!disposed) setRevision(current => current + 1);
      }, 300);
    };

    const start = async () => {
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
        console.warn("SignalR initial connection failed.", error);
        setStatus("disconnected");
        startRetryTimer = window.setTimeout(() => void start(), 5_000);
      }
    };

    connection.on("entityChanged", requestRefresh);
    connection.onreconnecting(() => {
      if (!disposed) setStatus("reconnecting");
    });
    connection.onreconnected(() => {
      if (disposed) return;
      setStatus("connected");

      // Events sent while this browser was offline are not replayed.
      requestRefresh();
    });
    connection.onclose(error => {
      if (disposed) return;
      if (error) console.warn("SignalR connection closed.", error);
      setStatus("disconnected");
      startRetryTimer = window.setTimeout(() => void start(), 5_000);
    });

    void start();

    return () => {
      disposed = true;
      if (refreshTimer !== undefined) window.clearTimeout(refreshTimer);
      if (startRetryTimer !== undefined) {
        window.clearTimeout(startRetryTimer);
      }
      connection.off("entityChanged", requestRefresh);
      void connection.stop();
    };
  }, [apiBase, accessToken]);

  return { revision, status };
}
```

Use it at the authenticated root:

```tsx
function AuthenticatedApp({ apiBase, session }: Props) {
  const realtime = useMiniErpRealtime(apiBase, session.accessToken);

  return (
    <InvoicesPage
      apiBase={apiBase}
      token={session.accessToken}
      realtimeRevision={realtime.revision}
    />
  );
}
```

Reload a page's server data when the revision changes:

```tsx
type InvoicesPageProps = {
  apiBase: string;
  token: string;
  realtimeRevision: number;
};

function InvoicesPage({
  apiBase,
  token,
  realtimeRevision
}: InvoicesPageProps) {
  const loadInvoices = useCallback(async () => {
    const result = await apiRequest(apiBase, "/Invoices", { token });
    setInvoices(result);
  }, [apiBase, token]);

  useEffect(() => {
    void loadInvoices();
  }, [loadInvoices, realtimeRevision]);

  // ...
}
```

Make data-loading callbacks stable with `useCallback`, or place the actual
request inside the effect. An unstable function dependency can create an
infinite render/request loop.

For Vue or Angular, keep the same architecture: one app-level service owns the
connection and publishes an incrementing revision or invalidates application
query caches.

## 8. Add, update, and delete flow

Continue using normal REST requests for mutations:

```ts
await apiRequest(apiBase, "/Invoices", {
  method: "POST",
  token: accessToken,
  body: invoice
});
```

Do not call `connection.invoke()` after a create, update, or delete. The backend
automatically creates and broadcasts the event after a successful database
save. A failed or rolled-back mutation does not produce a committed event.

Recommended user experience:

1. Submit the REST mutation.
2. Show validation or business errors normally if it fails.
3. On success, update or reload the mutating screen immediately.
4. Also accept the later SignalR event so other tabs and users refresh.
5. Debounce refreshes to prevent a burst of related entity changes from causing
   many identical GET requests.

The event is eventual and normally arrives shortly after the REST request. Do
not make form success depend on waiting for SignalR.

## 9. Refresh-all versus selective invalidation

Start with the global revision approach. It is deliberately conservative: any
company change refreshes the currently mounted data screens. This prevents
stale data when a transaction changes related entities such as an invoice,
invoice lines, balances, and movements.

If performance measurements show excessive requests, route individual changes
to query-cache keys. For example:

```ts
const affectedQueriesByResource: Record<string, string[]> = {
  Invoice: ["invoices", "inventory", "partner-statement"],
  InvoiceLine: ["invoices", "inventory", "partner-statement"],
  ItemMovement: ["inventory", "inventory-cost-report"],
  Item: ["items", "item-selectors", "inventory"]
};

connection.on("entityChanged", notification => {
  const keys = new Set<string>();

  for (const change of notification.changes) {
    for (const key of affectedQueriesByResource[change.resource] ?? []) {
      keys.add(key);
    }
  }

  for (const key of keys) queryClient.invalidateQueries({ queryKey: [key] });
});
```

When applying a selected-store filter, refresh if `storeIds` contains that
store. Also refresh when `storeIds` is empty because global or indirectly
store-related resources can still change the result:

```ts
const affectsSelectedStore =
  change.storeIds.length === 0 ||
  change.storeIds.includes(selectedStoreId);
```

Do not use `storeIds` as an authorization control. API authorization and
company isolation remain server responsibilities.

Always invalidate all relevant data after `onreconnected`, even if normal
events use selective routing.

## 10. Delivery behavior the frontend must handle

- Notifications are scoped to the company in the access token.
- One notification may describe several entity changes.
- Duplicate delivery is possible. Deduplicate using `eventId`.
- Rapid events are possible. Debounce or batch data reloads.
- Events are not replayed to a browser that was disconnected. Fully invalidate
  current data after reconnection.
- Never assume `entityId` is present.
- Never assume `storeIds` is present or non-empty.
- Treat unknown `resource` values as valid future backend additions. Do not
  crash while parsing them.
- SignalR is not a replacement for optimistic-concurrency validation. Continue
  sending concurrency tokens required by REST endpoints and handle `409`
  responses.

## 11. Local configuration

Example Vite environment file:

```env
VITE_API_BASE_URL=https://localhost:7067/api/v1
```

Example URL selection:

```ts
export const apiBase =
  import.meta.env.VITE_API_BASE_URL ??
  "https://localhost:7067/api/v1";
```

Before local HTTPS testing, trust the ASP.NET development certificate:

```powershell
dotnet dev-certs https --trust
```

Run the API:

```powershell
dotnet run --project F:\MiniErp\src\MiniErp.Api
```

Swagger is available at `https://localhost:7067/swagger` when enabled.

## 12. Acceptance test

Use this test before considering frontend integration complete:

1. Start the API and frontend.
2. Open two browser windows or profiles.
3. Log both users into the same company.
4. Confirm `/hubs/updates/negotiate` and the subsequent SignalR transport are
   successful in browser developer tools.
5. Add a record in browser A. Browser B must refresh it.
6. Update the record in browser A. Browser B must display the new values.
7. Delete the record in browser A. Browser B must remove it.
8. Repeat with an invoice and verify reports or balances that depend on it are
   invalidated.
9. Disconnect browser B's network, make a change in A, reconnect B, and verify
   B performs a full refresh.
10. Log a third browser into another company and verify it does not receive or
    display the first company's changes.
11. Refresh the JWT and verify reconnect uses the new access token.
12. Log out and verify the SignalR connection stops.

## 13. Troubleshooting

| Symptom | Most likely cause | Fix |
|---|---|---|
| Hub returns `404` | Hub path was appended to `/api/v1` | Use the absolute path `/hubs/updates` |
| Hub returns `401` | Missing, expired, selection, or refresh token | Use the final company-scoped access token |
| Connection aborts | Token has no valid `company_id` | Complete company selection and reconnect |
| Browser reports certificate failure | Local ASP.NET certificate is untrusted | Run `dotnet dev-certs https --trust` |
| Initial connection never retries | Only automatic reconnect was configured | Add explicit retry around the initial `start()` |
| Reconnect succeeds but data is stale | Missed events were expected to replay | Fully invalidate current data in `onreconnected` |
| UI makes too many GET requests | Every related entity caused a reload | Deduplicate, debounce, and then add measured selective invalidation |
| Event is visible but UI does not change | Data effect/query cache was not invalidated | Increment a revision or invalidate the relevant query keys |
| Other company data appears | Frontend cache was retained across company change | Stop connection and clear all company-scoped cache on company switch/logout |

For temporary diagnostics, change the SignalR client log level from
`LogLevel.Warning` to `LogLevel.Information`. Do not log access tokens.

## 14. Frontend completion checklist

- [ ] Exactly one SignalR connection exists per authenticated browser app.
- [ ] The connection uses `/hubs/updates`, not `/api/v1/hubs/updates`.
- [ ] Only the final company-scoped access token is used.
- [ ] `entityChanged` is registered before `start()`.
- [ ] Initial startup and later disconnections both retry.
- [ ] Recent `eventId` values are deduplicated with bounded memory.
- [ ] Refresh calls are debounced or query-cache invalidations are batched.
- [ ] Every reconnect causes a full invalidation.
- [ ] Company switching stops the old connection and clears old cached data.
- [ ] Logout stops the connection.
- [ ] Add, update, delete, reconnect, and company-isolation tests pass.

