# Invoice frontend contract

**API version:** v1  
**Status:** Backend ready / frontend integrated  
**Tenant:** `CompanyId` comes from the access-token `company_id` claim. The
frontend never sends `companyId`.

## Routes and authorization

| Method | Route | Access | Success |
|---|---|---|---:|
| GET | `/api/v1/Invoices?pageNumber=1&pageSize=20` | Authenticated | 200 |
| GET | `/api/v1/Invoices/{id}` | Authenticated | 200 |
| POST | `/api/v1/Invoices` | Admin | 201 |
| PUT | `/api/v1/Invoices/{id}` | Admin | 200 |
| DELETE | `/api/v1/Invoices/{id}` | Admin | 204 |

List pagination defaults to page 1 and size 20. `pageSize` must be 1–100.
Ordering is invoice date descending, then ID descending. Every list item
contains complete ordered product and container lines.

## Enums

Enums are serialized as JSON names; numeric enum values are rejected.

```text
PaymentTerm:
  Cash = 1
  Credit = 2

InvoiceType:
  Sales = 1
  Purchase = 2
  SalesReturn = 3
  PurchaseReturn = 4
```

The create and edit form must show a required Payment Term select:

```text
Cash   -> نقدي
Credit -> آجل
```

The default is `Cash`.

The form sends `discountAmount` and `paidAmount` explicitly. Both default to
zero for a new form. The server calculates the payment summary:

- `subtotal = sum(lines[].total)`.
- `total = subtotal - discountAmount`.
- `remainingAmount = total - paidAmount`.
- `Cash`: `paidAmount` must equal `total`, so `remainingAmount` is zero and
  no partner movement is created.
- `Credit`: `paidAmount` may be zero through `total`. A positive
  `remainingAmount` creates the applicable partner movement; a zero
  `remainingAmount` creates none. A partially paid Credit invoice remains
  `paymentStatus = "Unpaid"`; a zero remaining amount is `"Paid"`.

The backend validates that `discountAmount` is non-negative and does not
exceed the subtotal, and that `paidAmount` is non-negative and does not exceed
the net total. The frontend should keep a Cash invoice's paid amount
synchronized with its calculated total when lines or discount change.

The migration defaults both new columns to zero for existing invoice rows.
Historical Cash rows should be reviewed before they are edited under the
fully-paid Cash rule.

Invoice create, update, and delete also synchronize the current operational
side effects in the same transaction:

- Product lines create `ItemMovement` rows.
- Container lines create `ContainerMovement` rows.
- A supplied internal `driverId` creates one `DriverTrip` row.
- A Credit invoice creates one `BusinessPartnerMovement` row only when its
  `remainingAmount` is positive; the movement amount is the remaining amount.
- Cash invoices are immediately paid and do not create a partner movement.
- Updates replace the invoice's active side-effect rows; deletes soft-delete
  them with the invoice.

There is no status, posting, cancellation, reversal, voucher, or allocation
workflow in this current feature.

## Create request

The request contains the complete aggregate. Do not send `companyId`,
`invoiceNumber`, `currency`, `itemUnitId`, `quantity`, line `total`, invoice
`total`, audit fields, `lastModifiedAt`, or `rowVersion`.

```json
{
  "invoiceType": "Sales",
  "paymentTerm": "Cash",
  "invoiceDate": "2026-07-25",
  "dueDate": null,
  "businessPartnerId": 12,
  "storeId": 4,
  "containerStoreId": null,
  "countryId": 1,
  "driverId": 3,
  "usesExternalDriver": false,
  "externalDriverName": null,
  "vehicleNumber": "ABC-123",
  "exportInvoiceCode": null,
  "lines": [
    {
      "itemId": 8,
      "count": 10,
      "weight": 2.5,
      "price": 12.00,
      "notes": null
    }
  ],
  "containerLines": [],
  "discountAmount": 0.00,
  "paidAmount": 300.00,
  "notes": "Customer delivery"
}
```

Product lines require unique active items. The server derives the item unit,
calculates `quantity = count * weight`, rounds `total = quantity * price` to
two decimal places, and sums the subtotal. The net invoice total is
`subtotal - discountAmount`; the remaining amount is `total - paidAmount`.
At least one product line is required.

For an external driver, send `usesExternalDriver: true`, a required
`externalDriverName`, and `driverId: null`. For an internal driver,
`usesExternalDriver` is false and `driverId` may be null or an active driver.

Sales and sales-return invoices may contain container lines. When they are
present, `containerStoreId` is required and every container must be active and
assigned to that partner's active container store. Outgoing and incoming units
are non-negative and cannot both be zero.

Sales returns and purchase returns are independent invoice documents. They use
the selected partner, product store, items, quantities, prices, and payment
term without referencing an earlier invoice.

## Update request

`PUT /api/v1/Invoices/{id}` uses the same complete editable fields as create
and adds the original base64 row-version token returned by the API:

```json
{
  "invoiceType": "Sales",
  "paymentTerm": "Credit",
  "invoiceDate": "2026-07-25",
  "dueDate": "2026-08-24",
  "businessPartnerId": 12,
  "storeId": 4,
  "containerStoreId": null,
  "countryId": null,
  "driverId": null,
  "usesExternalDriver": false,
  "externalDriverName": null,
  "vehicleNumber": null,
  "exportInvoiceCode": null,
  "lines": [
    {
      "itemId": 8,
      "count": 10,
      "weight": 2.5,
      "price": 12.00,
      "notes": null
    }
  ],
  "containerLines": [],
  "discountAmount": 25.00,
  "paidAmount": 275.00,
  "notes": null,
  "rowVersion": "AAAAAAAAB9E="
}
```

The client must send the original token unchanged. Header-only, product-line-
only, and container-line-only changes all advance the header token. A
`409 Invoices.Concurrency` response requires reloading the aggregate before
retrying.

## Response shape

```json
{
  "id": 41,
  "companyId": 2,
  "invoiceNumber": "INV-2-20260725170000000-AB12CD34",
  "exportInvoiceCode": null,
  "invoiceType": "Sales",
  "paymentTerm": "Cash",
  "invoiceDate": "2026-07-25",
  "dueDate": null,
  "businessPartnerId": 12,
  "businessPartnerName": "Ahmed Mohamed Trading",
  "storeId": 4,
  "storeName": "Main Store",
  "containerStoreId": null,
  "containerStoreName": null,
  "countryId": 1,
  "countryName": "Egypt",
  "currency": "EGP",
  "driverId": 3,
  "driverName": "Ahmed Ali",
  "usesExternalDriver": false,
  "externalDriverName": null,
  "vehicleNumber": "ABC-123",
  "subtotal": 300.00,
  "discountAmount": 0.00,
  "total": 300.00,
  "paymentStatus": "Paid",
  "paidAmount": 300.00,
  "remainingAmount": 0.00,
  "notes": "Customer delivery",
  "rowVersion": "AAAAAAAAB9E=",
  "lines": [
    {
      "id": 71,
      "companyId": 2,
      "invoiceId": 41,
      "itemId": 8,
      "itemCode": "ITEM-0008",
      "itemName": "Item 8",
      "itemUnitId": 3,
      "itemUnitName": "Piece",
      "count": 10,
      "weight": 2.5,
      "quantity": 25.0,
      "price": 12.00,
      "total": 300.00,
      "notes": null
    }
  ],
  "containerLines": []
}
```

## Errors

All errors use the nine-field `ApiErrorResponse` and
`application/problem+json`.

- `400 Validation.Failed`: required field, invalid enum, invalid amount,
  invalid discount (`Invoices.InvalidDiscountAmount`), invalid paid amount
  (`Invoices.InvalidPaidAmount`), a Cash invoice that is not fully paid
  (`Invoices.CashInvoiceMustBeFullyPaid`), duplicate child IDs, or invalid
  pagination. Attach `errors.PaymentTerm`, `errors.DiscountAmount`,
  `errors.PaidAmount`, `errors.Lines`, or the matching field to the form.
- `404 Invoices.NotFound` or a relationship-specific not-found code:
  missing/deleted/cross-company data. Reload selectors or remove stale rows.
- `409 Invoices.Concurrency`: stale row version. Reload the invoice.
- `409 Inventory.InsufficientStock`: the invoice cannot be applied with the
  available balance on its invoice date.
- `409 Inventory.HistoricalStockConflict`: the aggregate change would make a
  later historical balance negative.
- `409` relationship conflicts: inactive partner/store/item/driver or
  container assignment mismatch. Preserve form values and show `errors.General`
  or the field entry.

Empty list pages return HTTP 200 with `items: []`. After a successful create,
update, or delete, reload the current page.
