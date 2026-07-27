# Cash Management Frontend Contract

This contract covers Cashboxes, Cash Movement Types, unified Cash Vouchers,
DriverTrip cost entry, and Cashbox/Partner/Driver Statements.

`CompanyId` always comes from the authenticated access token. The client must
never send or allow editing it.

## Shared behavior

- API base route: `/api/v1`.
- Enum values are JSON strings.
- Dates use `yyyy-MM-dd`.
- Money uses two decimal places and is sent as a JSON number.
- `RowVersion` is an eight-byte value serialized as base64.
- Reads require authentication.
- Creates, updates, deletes, and bulk cost updates require the `Admin` role.
- Lists filter and search on the server before pagination.
- The client must use the exact `RowVersion` returned by the API on update.
- On a concurrency `409`, discard unsaved assumptions, reload, let the user
  review the current values, and retry manually.
- Soft-deleted records do not appear in active lists, balances, or statements.

Standard paginated response:

```json
{
  "items": [],
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 0,
  "totalPages": 0
}
```

Common pagination:

| Query | Required | Default/use |
|---|---:|---|
| `pageNumber` | Yes | Positive integer |
| `pageSize` | Yes | `1..100` |
| `search` | No | Common resource-wide text search |

## Enums

```text
CashDirection:
  Receipt
  Payment

CashPartyType:
  None
  Partner
  Driver
  Other

PartnerAccountEffect:
  None
  Debit
  Credit

CurrencyCode:
  EGP
  USD
  EUR
  GBP
  SAR
  AED
  KWD

PartnerStatementSourceType:
  OpeningBalance
  Invoice
  CashVoucher

DriverStatementSourceType:
  CashVoucher
  DriverTrip
```

## Cashboxes

Routes:

```text
GET    /Cashboxes
GET    /Cashboxes/select
GET    /Cashboxes/{id}
POST   /Cashboxes
PUT    /Cashboxes/{id}
DELETE /Cashboxes/{id}
```

`GET /Cashboxes` filters:

| Filter | Type | Effect |
|---|---|---|
| `search` | string | Code, name, or notes |
| `code` | string | Contains |
| `name` | string | Contains |
| `currency` | `CurrencyCode` | Exact |
| `isActive` | boolean | Exact |

Create:

```json
{
  "code": "CASH-MAIN",
  "name": "Main Cashbox",
  "currency": "EGP",
  "openingBalance": 100000.00,
  "isActive": true,
  "notes": null
}
```

Update sends the same fields plus the original token:

```json
{
  "code": "CASH-MAIN",
  "name": "Main Cashbox",
  "currency": "EGP",
  "openingBalance": 100000.00,
  "isActive": true,
  "notes": null,
  "rowVersion": "AAAAAAAAB9E="
}
```

Response:

```json
{
  "id": 1,
  "companyId": 1,
  "code": "CASH-MAIN",
  "name": "Main Cashbox",
  "currency": "EGP",
  "openingBalance": 100000.00,
  "currentBalance": 101250.00,
  "isActive": true,
  "notes": null,
  "rowVersion": "AAAAAAAAB9E="
}
```

`currentBalance` is server-calculated and must not be sent in create/update
requests.

Select response:

```json
[
  {
    "id": 1,
    "name": "Main Cashbox",
    "currency": "EGP",
    "currentBalance": 101250.00
  }
]
```

An inactive cashbox remains visible in historical voucher responses but is not
returned by the select endpoint. Opening balance and currency cannot change
after any current or historical voucher exists.

## Cash Movement Types

Routes:

```text
GET    /CashMovementTypes
GET    /CashMovementTypes/select
GET    /CashMovementTypes/{id}
POST   /CashMovementTypes
PUT    /CashMovementTypes/{id}
DELETE /CashMovementTypes/{id}
```

List filters:

| Filter | Type | Effect |
|---|---|---|
| `search` | string | Name or notes |
| `name` | string | Contains |
| `direction` | `CashDirection` | Exact |
| `partnerEffect` | `PartnerAccountEffect` | Exact |
| `isActive` | boolean | Exact |

Select filters:

| Filter | Type | Effect |
|---|---|---|
| `direction` | `CashDirection` | Receipt or Payment |
| `forPartner` | boolean | `true` returns Debit/Credit; `false` returns None |

Create:

```json
{
  "name": "Customer Collection",
  "direction": "Receipt",
  "partnerEffect": "Credit",
  "isActive": true,
  "notes": null
}
```

Update adds `rowVersion`. Response adds `id`, `companyId`, and `rowVersion`.

The voucher form must reload movement types whenever direction or party type
changes:

- Partner party: `forPartner=true`.
- None, Driver, or Other party: `forPartner=false`.
- Always send the selected voucher direction.

Direction or partner effect cannot change after the type has been used.

## Unified Cash Vouchers

Routes:

```text
GET    /CashVouchers
GET    /CashVouchers/{id}
POST   /CashVouchers
PUT    /CashVouchers/{id}
DELETE /CashVouchers/{id}
```

List filters:

| Filter | Type | Effect |
|---|---|---|
| `search` | string | Voucher, cashbox, movement type, party, trip invoice, external name, reference, or description |
| `voucherNumber` | string | Contains |
| `direction` | `CashDirection` | Exact |
| `cashboxId` | integer | Exact |
| `cashMovementTypeId` | integer | Exact |
| `partyType` | `CashPartyType` | Exact |
| `businessPartnerId` | integer | Exact |
| `driverId` | integer | Exact |
| `driverTripId` | integer | Exact |
| `fromDate` | date | Inclusive |
| `toDate` | date | Inclusive |

Create:

```json
{
  "voucherNumber": "CV-2026-0001",
  "voucherDate": "2026-07-27",
  "direction": "Payment",
  "cashboxId": 1,
  "cashMovementTypeId": 4,
  "partyType": "Driver",
  "businessPartnerId": null,
  "driverId": 12,
  "driverTripId": null,
  "externalPartyName": null,
  "amount": 2500.00,
  "referenceNumber": "ADV-12",
  "description": "General driver advance",
  "notes": null
}
```

`voucherNumber` is required for each voucher, but it is not unique. The user may
save more than one voucher with the same number; use the voucher `id` as the
record identifier.

Update sends the complete same shape plus `rowVersion`.

Response:

```json
{
  "id": 81,
  "companyId": 1,
  "voucherNumber": "CV-2026-0001",
  "voucherDate": "2026-07-27",
  "direction": "Payment",
  "cashboxId": 1,
  "cashboxName": "Main Cashbox",
  "cashMovementTypeId": 4,
  "cashMovementTypeName": "Driver Advance",
  "partnerEffect": "None",
  "partyType": "Driver",
  "businessPartnerId": null,
  "businessPartnerName": null,
  "driverId": 12,
  "driverName": "Ahmed Ali",
  "driverTripId": null,
  "driverTripInvoiceNumber": null,
  "externalPartyName": null,
  "amount": 2500.00,
  "currency": "EGP",
  "referenceNumber": "ADV-12",
  "description": "General driver advance",
  "notes": null,
  "rowVersion": "AAAAAAAAB9E="
}
```

`currency`, names, partner effect, company ID, audit data, and all balance
effects are server-owned.

Party shapes:

| Party type | Required | Must be null |
|---|---|---|
| None | None | Partner, driver, trip, external name |
| Partner | `businessPartnerId` | Driver, trip, external name |
| Driver | `driverId` | Partner, external name; trip remains optional |
| Other | `externalPartyName` | Partner, driver, trip |

Changing party type in the form must immediately clear fields from the prior
shape. Selecting a driver exposes an optional trip selector populated through:

```text
GET /DriverTrips/cost-entry?pageNumber=1&pageSize=100&driverId={driverId}
```

A voucher never creates a DriverTrip. A successful voucher affects the
cashbox immediately. A partner voucher creates exactly one movement in the
existing partner account source. Update replaces the net effect; it does not
double it. Delete soft-deletes the voucher and reverses/removes its active
effects.

## DriverTrip cost entry

Routes:

```text
GET /DriverTrips/cost-entry
PUT /DriverTrips/bulk-costs
```

Query filters:

| Filter | Type | Effect |
|---|---|---|
| `search` | string | Invoice, export code, driver, or `TR-{id}` |
| `fromDate` / `toDate` | date | Inclusive |
| `driverId` | integer | Exact |
| `invoiceNumber` | string | Contains |
| `tripNumber` | string | `TR-101` or `101` |
| `hasCost` | boolean | Positive cost versus null/zero |

Row:

```json
{
  "driverTripId": 101,
  "tripNumber": "TR-101",
  "tripDate": "2026-07-27",
  "invoiceId": 5001,
  "invoiceNumber": "INV-5001",
  "driverId": 25,
  "driverName": "Driver Name",
  "cost": 1200.00,
  "costNotes": "Fuel and road expenses",
  "rowVersion": "AAAAAAAAB9E="
}
```

Bulk update:

```json
{
  "items": [
    {
      "driverTripId": 101,
      "cost": 1200.00,
      "notes": "Fuel and road expenses",
      "rowVersion": "AAAAAAAAB9E="
    },
    {
      "driverTripId": 102,
      "cost": null,
      "notes": null,
      "rowVersion": "AAAAAAAAB9I="
    }
  ]
}
```

Send only changed rows. IDs must be unique. If any row is invalid, missing,
cross-company, or stale, no row is saved. A cost update never creates a Cash
Voucher or changes cashbox, partner, or invoice amounts.

## Statements

All statements are authenticated read-only endpoints and return `items`,
pagination metadata, and a `summary`.

### Cashbox Statement

```text
GET /Statements/cashbox
```

Required: `cashboxId`.

Optional: `search`, `fromDate`, `toDate`, `direction`,
`cashMovementTypeId`, `partyType`, `businessPartnerId`, `driverId`,
`driverTripId`, `voucherNumber`.

Summary:

```json
{
  "openingBalance": 100000.00,
  "totalReceipts": 5000.00,
  "totalPayments": 2500.00,
  "closingBalance": 102500.00
}
```

The response also returns `cashboxId`, `cashboxName`, and `currency`. Each item
uses the direct display fields `movementName`, `partyName`, `receiptAmount`,
`paymentAmount`, and `balance`. The frontend does not need to translate cash
direction or party-type enums to render the table. Use `cashVoucherId` to
navigate to the voucher.

### Partner Statement

```text
GET /Statements/partner
```

Required: `businessPartnerId`.

Optional: `search`, `fromDate`, `toDate`, `sourceType`, `movementType`,
`cashMovementTypeId`.

The response returns `businessPartnerId`, `businessPartnerName`, and
`currency`. It intentionally avoids Debit/Credit wording in the user-facing
contract.

```json
{
  "openingBalanceAmount": 0,
  "openingBalanceDescription": "مسدد",
  "closingBalanceAmount": 250,
  "closingBalanceDescription": "عليه"
}
```

Each row returns `debitAmount` for the Arabic `عليه` column and `creditAmount`
for the Arabic `له` column. Display zero as a dash. Its Arabic `movementName`
explains whether it is an opening balance, invoice, return, receipt, or
payment. Always display the non-negative `balanceAmount` together with
`balanceDescription`, which is `عليه`, `له`, or `مسدد`.

### Driver Statement

```text
GET /Statements/driver
```

Required: `driverId`.

Optional: `search`, `fromDate`, `toDate`, `direction`,
`cashMovementTypeId`, `driverTripId`, `invoiceNumber`,
`transactionsWithoutTrip`, `hasCost`.

The response returns `driverId` and `driverName`.

```json
{
  "openingBalanceAmount": 0,
  "openingBalanceDescription": "لا يوجد مبلغ مستحق",
  "totalPaidToDriver": 100,
  "totalReceivedFromDriver": 20,
  "totalTripCost": 60,
  "closingBalanceAmount": 20,
  "closingBalanceDescription": "مبلغ مطلوب من السائق"
}
```

Rows return Arabic `sourceName` and `movementName`, and clearly separate
`amountPaidToDriver`, `amountReceivedFromDriver`, and `tripCost`. Display the
non-negative `balanceAmount` with its Arabic `balanceDescription`. A
DriverTrip cost row is operational and has no cashbox effect. General driver
vouchers with no trip affect only the overall driver balance.

## ProblemDetails and UI behavior

Validation example:

```json
{
  "status": 400,
  "title": "Validation error",
  "errorCode": "Validation.Failed",
  "errors": {
    "Amount": ["يجب أن يكون المبلغ أكبر من صفر."]
  }
}
```

Not found/cross-company example:

```json
{
  "status": 404,
  "errorCode": "CashVouchers.DriverTripNotFound",
  "detail": "لم يتم العثور على رحلة تخص السائق المحدد."
}
```

Conflict examples the UI should recognize:

```text
Cashboxes.CodeExists
Cashboxes.NameExists
Cashboxes.HasVouchers
Cashboxes.OpeningOrCurrencyChangeNotAllowed
Cashboxes.Concurrency

CashMovementTypes.NameExists
CashMovementTypes.HasVouchers
CashMovementTypes.UsedSemanticsChangeNotAllowed
CashMovementTypes.Concurrency

CashVouchers.CashboxInactive
CashVouchers.MovementTypeInactive
CashVouchers.MovementTypeDirectionMismatch
CashVouchers.MovementTypeNotForPartner
CashVouchers.MovementTypeForPartnerOnly
CashVouchers.PartnerCurrencyMismatch
CashVouchers.InsufficientCashboxBalance
CashVouchers.Concurrency

DriverTrips.Concurrency
```

Concurrency conflict behavior:

```json
{
  "status": 409,
  "errorCode": "CashVouchers.Concurrency",
  "detail": "تم تعديل سند النقدية بواسطة مستخدم آخر. أعد تحميل السند ثم حاول مرة أخرى."
}
```

Empty lists and statements are normal successful responses. Show an empty
state; do not treat `items: []` as an error.

## Implemented client screens

The React client provides:

- Cashbox list/form.
- Cash Movement Type list/form.
- Unified Cash Voucher list/create/edit/delete.
- Bulk DriverTrip cost entry.
- Cashbox Statement.
- Partner Statement.
- Driver Statement.

The screens reuse existing BusinessPartner and Driver select endpoints,
Cashbox/MovementType selects, the shared API error model, the current
Admin/read authorization split, and the existing sidebar/layout components.
