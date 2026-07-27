# Cash Voucher Lifecycle — Frontend User Story

## API references

- Base URL: `https://minierpapi.runasp.net/api/v1`
- [Cashboxes Swagger](https://minierpapi.runasp.net/swagger/index.html#/Cashboxes)
- [Cash movement types Swagger](https://minierpapi.runasp.net/swagger/index.html#/CashMovementTypes)
- [Cash vouchers Swagger](https://minierpapi.runasp.net/swagger/index.html#/CashVouchers)

All requests require:

```http
Authorization: Bearer <token>
Content-Type: application/json
```

The backend gets `CompanyId` from the authenticated token. The frontend must
never send or allow the user to select a company ID.

Enum values must be sent as their JSON names, not numeric values:

```text
CashDirection: Receipt | Payment
CashPartyType: None | Partner | Driver | Other
CurrencyCode: EGP | USD | EUR | GBP | SAR | AED | KWD
```

Dates must use ISO format:

```text
YYYY-MM-DD
```

## Main user story

As an authenticated MiniErp user, I want to view cash vouchers and their effect
on cashboxes, partners, and drivers so that I can follow all cash received and
cash paid by the company.

As an Admin, I want to configure cashboxes and cash movement types and create,
edit, or delete cash vouchers so that cash balances and related party balances
remain correct.

## Roles

### Authenticated user

Can:

- View cashboxes.
- View cash movement types.
- View cash vouchers.
- Use select endpoints.
- View cashbox, partner, and driver statements.

### Admin

Can also:

- Create, edit, and delete cashboxes.
- Create, edit, and delete cash movement types.
- Create, edit, and delete cash vouchers.

The frontend must hide mutation buttons when the user is not an Admin. The
backend remains the authority and can return `403`.

## Accounting rules shown in the UI

### Cash side

| Voucher direction | Cash effect | Accounting meaning |
|---|---:|---|
| `Receipt` | Increases cashbox balance | Debit Cash |
| `Payment` | Decreases cashbox balance | Credit Cash |

### Partner side

For a normal cash settlement with a business partner:

| Voucher direction | Cash side | Partner effect |
|---|---|---|
| `Receipt` | Debit Cash | `Credit` Partner |
| `Payment` | Credit Cash | `Debit` Partner |

Examples:

| Movement | Direction | Partner effect |
|---|---|---|
| Customer collection | `Receipt` | `Credit` |
| Supplier refund received | `Receipt` | `Credit` |
| Supplier payment | `Payment` | `Debit` |
| Customer refund paid | `Payment` | `Debit` |
| Driver advance/payment | `Payment` | `None` |
| Cash returned by driver | `Receipt` | `None` |
| Other receipt/payment | Matching direction | `None` |

Important:

- The frontend never asks the user to select Debit or Credit.
- A movement type exposes only `forPartner`.
- The server derives Credit Partner for Receipt and Debit Partner for Payment.
- Driver and other non-partner transactions create no partner movement.

## Page 1 — Cashboxes

### User experience

Show a paginated table with:

- Code.
- Name.
- Currency.
- Opening balance.
- Current balance.
- Active status.
- Notes.
- Admin actions.

Use `currentBalance` returned by the server. Never calculate or persist a
mutable cashbox balance in the frontend.

### Endpoints

| Method | Endpoint | Purpose |
|---|---|---|
| `GET` | `/Cashboxes` | Paginated table and filters |
| `GET` | `/Cashboxes/select` | Active cashbox dropdown with currency and current balance |
| `GET` | `/Cashboxes/{id}` | Load one cashbox before editing |
| `POST` | `/Cashboxes` | Create; Admin only |
| `PUT` | `/Cashboxes/{id}` | Update with `rowVersion`; Admin only |
| `DELETE` | `/Cashboxes/{id}` | Delete unused cashbox; Admin only |

### List filters

```ts
type CashboxFilters = {
  pageNumber?: number; // default 1
  pageSize?: number;   // default 20, maximum 100
  search?: string;
  code?: string;
  name?: string;
  currency?: CurrencyCode;
  isActive?: boolean;
};
```

All supplied filters use AND semantics.

### Create request

```ts
type CashboxRequest = {
  code: string;              // required, max 50
  name: string;              // required, max 200
  currency: CurrencyCode;    // required
  openingBalance: number;    // decimal(18,2)
  isActive: boolean;
  notes: string | null;      // max 1000
};
```

### Update request

Send all editable fields plus the latest base64 `rowVersion` received from the
API:

```ts
type CashboxUpdateRequest = CashboxRequest & {
  rowVersion: string;
};
```

### Cashbox rules

- Code is unique per company, case-insensitive.
- Name is unique per company, case-insensitive.
- After the first historical or active voucher uses the cashbox, currency and
  opening balance cannot be changed.
- A used cashbox cannot be deleted; deactivate it instead.
- Inactive cashboxes are excluded from `/Cashboxes/select`.
- Keep an inactive currently selected cashbox visible while editing an old
  voucher, but do not allow it for a new voucher.

## Page 2 — Cash Movement Types

### User experience

Show a paginated master-data table with:

- Name.
- Direction.
- Usage: business partner or general movement.
- Active status.
- Notes.
- Admin actions.

Use clear labels:

```text
Receipt = قبض / نقد داخل
Payment = صرف / نقد خارج
ForPartner = true: حركة عميل أو مورد
ForPartner = false: حركة عامة
```

### Endpoints

| Method | Endpoint | Purpose |
|---|---|---|
| `GET` | `/CashMovementTypes` | Paginated table and filters |
| `GET` | `/CashMovementTypes/select` | Active voucher-form options |
| `GET` | `/CashMovementTypes/{id}` | Load one type before editing |
| `POST` | `/CashMovementTypes` | Create; Admin only |
| `PUT` | `/CashMovementTypes/{id}` | Update with `rowVersion`; Admin only |
| `DELETE` | `/CashMovementTypes/{id}` | Delete unused type; Admin only |

### List filters

```ts
type CashMovementTypeFilters = {
  pageNumber?: number;
  pageSize?: number;
  search?: string;
  name?: string;
  direction?: CashDirection;
  forPartner?: boolean;
  isActive?: boolean;
};
```

### Dropdown endpoint

```http
GET /CashMovementTypes/select?direction=Receipt&forPartner=true
GET /CashMovementTypes/select?direction=Payment&forPartner=false
```

- `forPartner=true`: returns active customer/supplier movement types.
- `forPartner=false`: returns active general, driver, and other movement types.
- Omit `forPartner` only on a general master-data screen.

### Create request

```ts
type CashMovementTypeRequest = {
  name: string;                // required, max 200
  direction: CashDirection;    // required
  forPartner: boolean;         // true for customer/supplier types
  isActive: boolean;
  notes: string | null;        // max 1000
};
```

### Update request

```ts
type CashMovementTypeUpdateRequest = CashMovementTypeRequest & {
  rowVersion: string;
};
```

### Movement-type rules

- Name is unique inside the same company and direction.
- Once used by any active or historical voucher, direction and `forPartner`
  cannot change.
- A used type cannot be deleted; deactivate it instead.
- Inactive types are excluded from `/CashMovementTypes/select`.
- The end user never selects Debit or Credit.
- The backend stores the derived accounting effect internally:
  - `Receipt + forPartner=true` becomes Credit Partner.
  - `Payment + forPartner=true` becomes Debit Partner.
  - `forPartner=false` creates no partner effect.

## Page 3 — Cash Vouchers

### List view

Show:

- Voucher date.
- Voucher number.
- Direction badge.
- Cashbox.
- Movement type.
- Party type and party name.
- Driver trip when present.
- Amount and currency.
- Reference number.
- View action.
- Admin edit/delete actions.

Direction styling:

- `Receipt`: positive/green.
- `Payment`: negative/red or orange.

### Endpoints

| Method | Endpoint | Purpose |
|---|---|---|
| `GET` | `/CashVouchers` | Paginated voucher table and filters |
| `GET` | `/CashVouchers/{id}` | Full voucher details and current `rowVersion` |
| `POST` | `/CashVouchers` | Create voucher and side effects atomically; Admin only |
| `PUT` | `/CashVouchers/{id}` | Replace editable data and side effects atomically; Admin only |
| `DELETE` | `/CashVouchers/{id}` | Delete voucher and reverse its side effects atomically; Admin only |

### List filters

```ts
type CashVoucherFilters = {
  pageNumber?: number;
  pageSize?: number;
  search?: string;
  voucherNumber?: string;
  direction?: CashDirection;
  cashboxId?: number;
  cashMovementTypeId?: number;
  partyType?: CashPartyType;
  businessPartnerId?: number;
  driverId?: number;
  driverTripId?: number;
  fromDate?: string; // YYYY-MM-DD
  toDate?: string;   // YYYY-MM-DD
};
```

`toDate` cannot be earlier than `fromDate`.

Search covers voucher number, cashbox, movement type, partner, driver, trip
invoice number, external party, reference number, and description.

### Form fields

Always show:

- Direction — required.
- Voucher number — required, max 100.
- Voucher date — required.
- Cashbox — required.
- Movement type — required.
- Amount — required, greater than zero, maximum 2 decimal places.
- Party type — required.
- Reference number — optional, max 100.
- Description — optional, max 1000.
- Notes — optional, max 1000.

### Conditional party fields

| `partyType` | Show | Send |
|---|---|---|
| `None` | No party field | All party IDs and external name `null` |
| `Partner` | Required partner dropdown | `businessPartnerId`; other party fields `null` |
| `Driver` | Required driver and optional trip | `driverId`, optional `driverTripId`; other party fields `null` |
| `Other` | Required external-party name | `externalPartyName`; all IDs `null` |

Whenever `partyType` changes, clear all previously selected party fields and
clear `cashMovementTypeId`.

Whenever `direction` changes, clear `cashMovementTypeId`.

Whenever `driverId` changes, clear `driverTripId`.

### Supporting dropdown endpoints

Load when the form opens:

```http
GET /Cashboxes/select
GET /BusinessPartners/select
GET /Drivers/select
```

Load movement types whenever direction or party type changes:

```http
GET /CashMovementTypes/select
    ?direction=<Receipt|Payment>
    &forPartner=<true when partyType is Partner, otherwise false>
```

Load optional trips after selecting a driver:

```http
GET /DriverTrips/cost-entry
    ?pageNumber=1
    &pageSize=100
    &driverId=<selected driver id>
```

The trip is optional. A general driver voucher without a trip is valid. A cash
voucher never creates a driver trip and never automatically allocates a general
voucher to a future trip.

### Create request

```ts
type CashVoucherRequest = {
  voucherNumber: string;
  voucherDate: string;
  direction: CashDirection;
  cashboxId: number;
  cashMovementTypeId: number;
  partyType: CashPartyType;
  businessPartnerId: number | null;
  driverId: number | null;
  driverTripId: number | null;
  externalPartyName: string | null;
  amount: number;
  referenceNumber: string | null;
  description: string | null;
  notes: string | null;
};
```

Example — customer collection:

```json
{
  "voucherNumber": "REC-1001",
  "voucherDate": "2026-07-27",
  "direction": "Receipt",
  "cashboxId": 1,
  "cashMovementTypeId": 1,
  "partyType": "Partner",
  "businessPartnerId": 25,
  "driverId": null,
  "driverTripId": null,
  "externalPartyName": null,
  "amount": 1500.00,
  "referenceNumber": "INV-2001",
  "description": "Customer invoice collection",
  "notes": null
}
```

The selected movement type must be `Receipt + forPartner=true`. The backend
derives the partner Credit.

Example — supplier payment:

```json
{
  "voucherNumber": "PAY-1001",
  "voucherDate": "2026-07-27",
  "direction": "Payment",
  "cashboxId": 1,
  "cashMovementTypeId": 4,
  "partyType": "Partner",
  "businessPartnerId": 30,
  "driverId": null,
  "driverTripId": null,
  "externalPartyName": null,
  "amount": 750.00,
  "referenceNumber": null,
  "description": "Supplier payment",
  "notes": null
}
```

The selected movement type must be `Payment + forPartner=true`. The backend
derives the partner Debit.

Example — driver advance:

```json
{
  "voucherNumber": "DRV-1001",
  "voucherDate": "2026-07-27",
  "direction": "Payment",
  "cashboxId": 1,
  "cashMovementTypeId": 6,
  "partyType": "Driver",
  "businessPartnerId": null,
  "driverId": 7,
  "driverTripId": null,
  "externalPartyName": null,
  "amount": 500.00,
  "referenceNumber": null,
  "description": "General driver advance",
  "notes": null
}
```

The selected movement type must use `forPartner=false`.

### Save lifecycle

When the user submits a new voucher:

1. Disable the Save button to prevent duplicate submission.
2. Validate required and conditional fields locally.
3. Send enum names and ISO date values.
4. The backend accepts the voucher number even when another voucher already
   uses the same number.
5. The backend loads the active company cashbox.
6. The backend loads the active movement type.
7. The backend verifies movement-type direction equals voucher direction.
8. The backend validates the selected party:
   - Partner must exist, be active, and match cashbox currency.
   - Driver must exist and be active.
   - Optional driver trip must belong to the selected driver.
9. The backend validates party type against movement-type usage:
   - Partner requires `forPartner=true`.
   - Non-partner requires `forPartner=false`.
10. The backend checks the final cashbox balance.
11. The backend saves the voucher.
12. For `PartyType.Partner`, the backend creates exactly one
    `BusinessPartnerMovement`.
13. For driver vouchers, no partner movement is created.
14. All database changes commit atomically.
15. On success, close the form, show a success notice, reset to an appropriate
    page, and refresh the list and cashbox options.

### Cashbox balance rule

```text
CurrentBalance = OpeningBalance + Receipts - Payments
```

- Receipt increases balance.
- Payment decreases balance.
- The backend rejects an operation that makes an affected cashbox balance
  negative.
- The frontend should show the selected cashbox's current balance and warn
  before submitting a Payment greater than it.
- The backend remains authoritative because balances can change concurrently.

### Edit lifecycle

1. Call `GET /CashVouchers/{id}` before opening the edit form.
2. Populate the form from that response.
3. Keep the original returned `rowVersion`.
4. Load dropdowns using the voucher direction and party type.
5. If an existing cashbox or movement type is inactive, append it as a disabled
   or clearly marked “inactive/current value” option so the old voucher remains
   readable.
6. Send all editable fields plus the original `rowVersion` to
   `PUT /CashVouchers/{id}`.
7. Replace local state with the successful response, including its new
   `rowVersion`.
8. If `CashVouchers.Concurrency` is returned, tell the user that another user
   changed the voucher and offer Reload.

The backend removes the old cashbox/partner movement and applies the replacement
exactly once inside one transaction. This supports changing direction, cashbox,
movement type, party type, partner, driver, trip, date, and amount.

### Delete lifecycle

1. Show a confirmation containing voucher number, direction, amount, currency,
   and party name.
2. Call `DELETE /CashVouchers/{id}`.
3. On `204`, remove or reload the row and refresh cashbox options.
4. The backend deletes the related partner movement atomically.
5. Deleting a Receipt can be rejected if removing it would leave the cashbox
   balance negative.

## Response model used by voucher UI

```ts
type CashVoucherResponse = {
  id: number;
  companyId: number;
  voucherNumber: string;
  voucherDate: string;
  direction: CashDirection;
  cashboxId: number;
  cashboxName: string;
  cashMovementTypeId: number;
  cashMovementTypeName: string;
  partyType: CashPartyType;
  businessPartnerId: number | null;
  businessPartnerName: string | null;
  driverId: number | null;
  driverName: string | null;
  driverTripId: number | null;
  driverTripInvoiceNumber: string | null;
  externalPartyName: string | null;
  amount: number;
  currency: CurrencyCode;
  referenceNumber: string | null;
  description: string | null;
  notes: string | null;
  rowVersion: string;
};
```

Currency is derived from the selected cashbox by the backend. Do not send a
voucher currency.

## Pagination contract

Paginated endpoints return:

```ts
type PagedResponse<T> = {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
};
```

Default page size is 20 and maximum page size is 100.

## Error handling

The API error response includes:

```ts
type ApiErrorResponse = {
  type: string;
  title: string;
  status: number;
  detail: string;
  instance: string;
  errorCode: string;
  errorType: string;
  errors: Record<string, string[]>;
  traceId: string;
};
```

Frontend behavior:

| Status | UI behavior |
|---|---|
| `400` | Show field validation from `errors`; keep form open |
| `401` | Clear authentication and redirect to login |
| `403` | Show permission message |
| `404` | Show stale/missing reference message and reload options |
| `409` | Show `detail`; handle known `errorCode` cases below |

Important conflict codes:

```text
Cashboxes.CodeExists
Cashboxes.NameExists
Cashboxes.Concurrency
Cashboxes.HasVouchers
Cashboxes.OpeningOrCurrencyChangeNotAllowed

CashMovementTypes.NameExists
CashMovementTypes.Concurrency
CashMovementTypes.HasVouchers
CashMovementTypes.UsedSemanticsChangeNotAllowed

CashVouchers.Concurrency
CashVouchers.CashboxInactive
CashVouchers.MovementTypeInactive
CashVouchers.MovementTypeDirectionMismatch
CashVouchers.MovementTypeNotForPartner
CashVouchers.MovementTypeForPartnerOnly
CashVouchers.PartnerCurrencyMismatch
CashVouchers.InsufficientCashboxBalance
```

Do not branch on translated error text. Branch on `errorCode` and display
`detail` to the user.

## Statement links after save

The voucher detail page may provide these optional navigation actions:

```http
GET /Statements/cashbox?cashboxId=<id>&pageNumber=1&pageSize=20
GET /Statements/partner?businessPartnerId=<id>&pageNumber=1&pageSize=20
GET /Statements/driver?driverId=<id>&pageNumber=1&pageSize=20
```

The backend keeps this calculation internally:

```text
DriverBalance = CashPaidToDriver - CashReceivedFromDriver - DriverTripCost
```

The frontend must not show a negative number or accounting wording to the
user. It displays the non-negative `balanceAmount` with the Arabic
`balanceDescription` returned by the API:

- `مبلغ مطلوب من السائق`
- `مبلغ مطلوب دفعه للسائق`
- `لا يوجد مبلغ مستحق`

## Acceptance scenarios

### Scenario 1 — Customer collection

Given an active EGP cashbox and an active customer using EGP  
And an active movement type with `Receipt + forPartner=true`  
When the Admin creates a partner Receipt  
Then the cashbox balance increases  
And one partner Credit movement is created  
And the voucher appears in the list and statements.

### Scenario 2 — Supplier payment

Given an active cashbox with enough balance  
And an active movement type with `Payment + forPartner=true`  
When the Admin creates a partner Payment  
Then the cashbox balance decreases  
And one partner Debit movement is created.

### Scenario 3 — Insufficient cash

Given a cashbox whose current balance is 100  
When the Admin tries to create a Payment for 150  
Then the backend returns `409 CashVouchers.InsufficientCashboxBalance`  
And no voucher or partner movement is saved.

### Scenario 4 — Driver advance without trip

Given an active driver and a `Payment + None` movement type  
When the Admin creates a Driver payment without `driverTripId`  
Then the voucher is saved  
And no partner movement is created  
And the driver statement shows Cash Paid to Driver.

### Scenario 5 — Driver voucher linked to trip

Given a trip belonging to the selected driver  
When the Admin optionally selects that trip  
Then the voucher is linked to it  
But no trip is created or automatically allocated.

### Scenario 6 — External party

Given a movement type with `forPartner=false`  
When the Admin chooses `Other`  
Then external-party name is required  
And partner/driver/trip IDs are sent as `null`.

### Scenario 7 — Edit with concurrency

Given two users opened the same voucher  
When the first user saves successfully  
And the second submits the old `rowVersion`  
Then the second receives `409 CashVouchers.Concurrency`  
And the UI offers Reload without silently overwriting data.

### Scenario 8 — Delete receipt that supports later payments

Given deleting a Receipt would make the final cashbox balance negative  
When the Admin confirms delete  
Then the API rejects it with
`CashVouchers.InsufficientCashboxBalance`  
And the voucher remains unchanged.

### Scenario 9 — Inactive master data during edit

Given an old voucher references an inactive cashbox or movement type  
When the user edits that voucher  
Then the current inactive selection remains visible  
But it cannot be selected for a new voucher.

## Definition of done

- All three list pages support loading, empty, error, and pagination states.
- Admin mutation controls are permission-aware.
- Create/edit forms follow the conditional-field matrix.
- Direction or party changes reload compatible movement types and clear stale
  selection.
- Driver changes reload trips and clear stale trip selection.
- Requests use enum names and ISO dates.
- Update requests send the latest base64 `rowVersion`.
- The UI displays server-derived currency and balances.
- The UI handles all standard HTTP statuses and known conflict codes.
- Successful voucher mutations refresh both the voucher list and cashbox
  balances.
- No frontend logic creates partner movements, driver trips, or calculated
  balances; the backend owns those operations.
