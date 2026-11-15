# MiniERP — Combined frontend handoff: November 13–14, 2026

This document consolidates the backend changes and frontend behavior covered by the November 13 and 14 handoffs. Repeated containers-only invoice notes are combined into one section. Manual invoice numbering was reviewed and confirmed as existing behavior.

All endpoint paths below are relative to `/api/v1`. Use the selected company's Bearer token. The reference React client already implements the corresponding screens; use these contracts when integrating another frontend.

## Overview

| Date | Arabic title | Section |
| --- | --- | --- |
| November 13 | إظهار الخزينة والسند في كشف حساب العميل | 1 |
| November 13 | إضافة أرصدة افتتاحية للسائقين | 2 |
| November 13 | إنشاء رحلات سائقين بدون فواتير | 3 |
| November 13; verified again November 14 | إنشاء فواتير عبوات فقط بدون أصناف | 4 |
| November 14 | تعديل سند الخزينة قبل التوصيف وقَفله بعد الترحيل | 5 |
| Existing behavior reviewed November 14 | الترقيم اليدوي للفواتير | 6 |
| November 14 | تعطيل الخزينة عند رصيد صفر | 7 |
| November 14 | حذف الخزينة بعد حذف حركاتها | 8 |

## Common frontend rules

- Send an explicit `fiscalYearId` for lists, statements and historical detail requests where supported. Opening a historical document must not switch the year in the top bar.
- Send the latest Base64 `rowVersion` for updates that require it. URL-encode it when an opening balance or driver trip DELETE takes it in the query string.
- On `409`, show the API message and reload the current record before retrying. Do not manufacture or reuse a stale revision.
- Use returned `isReadOnly`, `isCarriedForward`, `isStandalone` and `isDraft` according to each contract. Synthetic opening rows cannot use normal transaction actions.
- Use `amountText` and `costText` for precise JavaScript editing. Opening balances and standalone trip costs accept decimal strings.
- Clear selected details and cancel or ignore pending responses when company or fiscal-year context changes. Re-check permissions when opening a linked document.
- Cashbox permissions distinguish View and Execute. Preserve delegated Execute access for ordinary authenticated users; cash-voucher writes do not introduce an Admin-only requirement.

## 1. إظهار الخزينة والسند في كشف حساب العميل

### Backend

`GET /Statements/partner?businessPartnerId={id}&fiscalYearId={yearId}&pageNumber=1&pageSize=20`

Statement items add five nullable link fields: `cashVoucherId`, `cashboxId`, `cashboxName`, `cashDirection` and `fiscalYearId`.

```json
{
  "documentNumber": "PAY-0042",
  "sourceType": "CashVoucher",
  "cashVoucherId": 37,
  "cashboxId": 5,
  "cashboxName": "Main Cashbox",
  "cashDirection": "Payment",
  "fiscalYearId": 9
}
```

These five fields are populated for a posted standalone voucher only when the user can view its cashbox. They are null for inaccessible cashboxes and non-voucher rows, including invoices, manual journals and opening balances. Hidden link fields do not remove the statement row or change its amount, balance, totals or pagination. A cash-paid invoice row is not itself a voucher link.

`GET /CashVouchers/{cashVoucherId}?fiscalYearId={rowFiscalYearId}`

The detail endpoint checks the cashbox permission again.

### Frontend

- For `Payment`, display `من خزنة {cashboxName}` next to the document number.
- For `Receipt`, display `إلى خزنة {cashboxName}`.
- Make the number clickable only when the link fields are complete and View access exists. Otherwise keep the plain document number and financial amounts visible without the cashbox label or link.
- Open the exact voucher by ID and its own fiscal year, not by document number, journal-line ID or the current top-bar year. Duplicate document numbers must not open another record.
- Show readonly voucher details and return to the same statement page and filters. A posted independent voucher can also display its exact journal as described in section 5.
- On `403`, clear stale details and show access denied. On `404`, show that the voucher is unavailable. On `401`, use the existing session flow.

## 2. إضافة أرصدة افتتاحية للسائقين

### Endpoints

`GET /FiscalYears/select`

`GET /Drivers/select`

`GET /DriverOpeningBalances?fiscalYearId={yearId}&driverId={driverId}&pageNumber=1&pageSize=20`

`GET /DriverOpeningBalances/{id}?fiscalYearId={yearId}`

`POST /DriverOpeningBalances`

`PUT /DriverOpeningBalances/{id}`

`DELETE /DriverOpeningBalances/{id}?rowVersion={urlEncodedBase64}`

`GET /Statements/driver?driverId={id}&fiscalYearId={yearId}&pageNumber=1&pageSize=20`

The opening list defaults to the company's current fiscal year when the year filter is omitted. Adding, editing or deleting a manual opening requires Admin and an open fiscal year.

### Request and returned fields

```json
{
  "driverId": 42,
  "fiscalYearId": 9,
  "amount": "1500.25",
  "balanceType": "Payable",
  "notes": "Balance at start of year"
}
```

- `Payable` means the company owes the driver (`له`); `Receivable` means the driver owes the company (`عليه`).
- Send a positive amount with up to 15 integer digits and two decimal places. JSON numbers are also accepted. Notes are optional, maximum 1,000 characters.
- The server assigns the first day of the selected fiscal year and the company's base currency.
- PUT adds the latest `rowVersion`. Keep the driver and fiscal year fixed on edit. DELETE returns `204` on success.
- Rows provide `id`, `driverId`, `driverName`, `fiscalYearId`, `fiscalYearName`, `documentNumber`, `documentDate`, `amount`, `amountText`, `currency`, `balanceType`, `notes`, `isCarriedForward`, `isReadOnly` and `rowVersion`.
- A carried opening has a synthetic ID, `isCarriedForward: true`, `isReadOnly: true` and null `rowVersion`. Hide edit and delete.
- An existing manual or carried opening prevents a second opening for the same driver/year. Closing also rejects a carry into a year that already has a manual opening for that driver.

### Screen, statement and accounting

Filter the opening screen by year and optionally driver. Show driver, year, document number/date, amount, currency, balance side and notes. Use active drivers for new documents; retain inactive drivers on historical rows and allow filtering by a driver already shown in a row.

Render the single opening row supplied by the driver statement. It has `isOpeningBalance: true` and `isReadOnly: true`; use the server's `balanceAmount` and `balanceDescription`. It affects the running balance, while its cash-paid, cash-received and trip-cost fields are zero. Do not add a second synthetic opening in the frontend.

The backend posts a balanced journal through `DriverControl` and `OpeningBalanceEquity` for the selected year. Manage a dedicated manual opening through its opening document. A year-closing journal containing driver carry remains readonly. Refresh the list and statement after changes, year closing, and relevant opening/ledger/fiscal-year realtime events.

## 3. إنشاء رحلات سائقين بدون فواتير

### Endpoints

`GET /FiscalYears/select`

`GET /Drivers/select`

`GET /BusinessPartners/select`

`GET /DriverTrips/cost-entry?fiscalYearId={yearId}&isStandalone=true&pageNumber=1&pageSize=20`

`GET /DriverTrips/{id}?fiscalYearId={yearId}`

`POST /DriverTrips`

`PUT /DriverTrips/{id}`

`DELETE /DriverTrips/{id}?rowVersion={urlEncodedBase64}`

`PUT /DriverTrips/bulk-costs`

`GET /Statements/driver?driverId={id}&fiscalYearId={yearId}&pageNumber=1&pageSize=20`

Trip creation, editing, deletion and bulk cost editing require Admin and an open fiscal year.

### Create and edit

```json
{
  "driverId": 42,
  "fiscalYearId": 9,
  "tripDate": "2026-11-13",
  "businessPartnerId": null,
  "actualDriverName": null,
  "cost": "1500.25",
  "notes": "Independent delivery"
}
```

A standalone trip has no invoice. Business partner, actual driver name, cost and notes are optional. The selected partner identifies the customer or supplier; the cost accrues to the driver. The server generates `TR-{driverTripId}` and uses the company's base currency for cost.

Both null and zero cost are supported. A cost has at most 15 integer digits and two decimal places. Notes have a 1,000-character limit and actual driver name a 200-character limit. PUT uses the same fields plus the latest Base64 `rowVersion`; the trip stays in its original fiscal year. DELETE returns `204`.

Responses retain the existing cost-entry fields and provide `isStandalone`, `isReadOnly`, `baseCurrency`, `actualDriverName` and `costText`. Invoice and partner IDs/names can be null. Use the trip number and an empty invoice indicator for standalone rows; edit using `costText`. The list's `isStandalone` filter is optional.

### Screen, accounting and restrictions

- Add the standalone form to the existing driver-trip-cost screen. Show edit/delete only for editable standalone rows in an open year with management access.
- Invoice-owned trips retain their invoice source and existing bulk-cost flow.
- A cash-voucher reference, including a soft-deleted voucher reference, blocks deleting a trip or changing its driver, partner or date with `DriverTrips.LinkedVoucher`. Cost and notes retain their existing editing flow.
- A positive cost creates a balanced automatic journal through `DriverTripExpense` and `DriverControl`. Null or zero removes the trip's cost journal. Source and accounting changes commit together.
- The driver statement includes the posted cost and derives the balance from the ledger. Payment uses the existing cash-voucher flow.
- For voucher trip choices, load `/DriverTrips/cost-entry` with the selected driver and the voucher's fiscal year. Submit `driverTripId` from the response.
- Refresh trips and the driver statement on `DriverTrip`, ledger or fiscal-year updates. Closed years are readonly. Closing carries only the remaining driver ledger balance.

## 4. إنشاء فواتير عبوات فقط بدون أصناف

This workflow is implemented in both applications. Choose **محتوى الفاتورة → عبوات فقط**, select the partner's container store, and enter outgoing/incoming container quantities. Do not require item lines.

### Endpoints

`POST /Invoices`

`PUT /Invoices/{id}`

`GET /Invoices?fiscalYearId={yearId}`

`GET /Invoices/{id}`

### Request excerpt

```json
{
  "invoiceNumber": "CONT-001",
  "contentType": "Containers",
  "storeId": 41,
  "containerStoreId": 41,
  "lines": [],
  "containerLines": [
    { "containerId": 5, "outgoingUnits": 8, "incomingUnits": 3 }
  ],
  "discountAmount": 0,
  "paidAmount": 0,
  "cashboxId": null
}
```

Supply the other normal required invoice fields. `storeId` and `containerStoreId` refer to the selected container store. At least one valid container movement is required. A `Containers` invoice must not contain item lines.

This workflow records container quantities with zero invoice value; it does not introduce container pricing. It creates container-stock movements without item-stock movements, and supports sales, purchases and both return types within the normal company/year rules. Driver data can use the normal invoice flow.

When switching from Items to Containers, the UI clears item lines, discount, payment and cashbox. Responses retain `contentType: "Containers"`, empty `lines` and the saved `containerLines`. Editing uses the existing PUT and latest `rowVersion`; do not send a new invoice number.

## 5. تعديل سند الخزينة قبل التوصيف وقَفله بعد الترحيل

This section applies to independent cash receipts/payments. Use `isDraft` to identify posting state. The optional `classification` field can be blank on an already-posted voucher, so it cannot determine editability.

| State | Frontend actions |
| --- | --- |
| Independent draft; own year open; Execute permission | Edit draft; separate classify-and-post action |
| Posted independent voucher | Readonly details and journal |
| Closed year or View-only access | Readonly details |
| Invoice/transfer-generated voucher | Readonly details and source guidance |
| Opening balance | Existing opening-balance/journal workflow |

Independent vouchers cannot be deleted in either draft or posted state. This is different from deleting the cashbox master in section 8.

### Endpoints and payloads

`GET /CashVouchers?fiscalYearId={yearId}`

Load the selected year's list.

`GET /CashVouchers/{id}?fiscalYearId={voucherFiscalYearId}`

Load the latest record before opening an editor or recovering from a conflict. Retain its current revision, posting state, source IDs and fiscal year.

`POST /CashVouchers`

Create an operational draft with no effect on cash balance or accounting.

`PUT /CashVouchers/{id}/draft`

Replace the draft's operational fields without posting:

```json
{
  "voucherDate": "2026-11-14",
  "direction": "Payment",
  "cashboxId": 5,
  "amount": 100,
  "exchangeRate": 52.25,
  "referenceNumber": "REF-1",
  "description": "Cash payment",
  "notes": null,
  "rowVersion": "<latest Base64 revision>"
}
```

- Send the complete operational payload. Optional text omitted or null is cleared; text is trimmed and blank text becomes null. Description and notes allow 1,000 characters each; reference allows 100. Amount is positive and fits decimal(18,2).
- Date stays inside the draft's original open fiscal year. Company, year, ID and voucher number cannot change; the number also stays fixed if direction changes.
- Currency comes from the selected cashbox. Moving requires Execute access to both original and destination cashboxes; a newly selected cashbox must be active. Retaining an original inactive reference follows existing source rules.
- Do not send party/account/movement-type or `isPosted` fields to `/draft`.
- `exchangeRate` is base-currency units per one cashbox-currency unit. It must be positive, fit decimal(28,12), and produce a base amount fitting decimal(28,8). Base currency accepts only `1`.
- With no replacement rate and unchanged date/currency, retain the captured snapshot and recalculate base amount for the new amount. With changed date/currency and no foreign rate, leave the draft unpriced. An unresolved foreign draft has rate `1` and base amount `0`; this is a placeholder, not a quote.
- A supplied draft rate is voucher-local and does not change the daily rate table. Draft saving creates no cash, partner, employee or journal effect.
- On success replace the cached full response, including `rowVersion`. The voucher remains `isDraft: true`.

`PUT /CashVouchers/{id}`

Classify and post an existing draft exactly once. Keep the existing full completion payload: operational fields, posting target and latest `rowVersion`. Show an explicit confirmation that posting affects balances and locks the voucher. Success returns `isDraft: false`; voucher, movements and journal commit atomically.

A valid target follows the existing rules: one party or account, or an eligible mapped movement type where supported. Cashbox/partner exchange rates retain their existing settlement rules. Valid cash payments and completion remain allowed when the cashbox balance is zero or negative.

`POST /CashVouchers/bulk`

The request is atomic. `Add` without a posting target creates a draft; `Add` with a valid target creates and posts it. `Update` completes an existing draft with the same target/revision rules. A posted `Update` or independent `Delete` is rejected, rolling back all earlier items in the batch. A selected movement type can be a valid posting route when its account mapping is valid.

`DELETE /CashVouchers/{id}`

Independent deletion returns `CashVouchers.DeleteNotAllowed` (`409`), including drafts. Remove its UI delete action. Generated vouchers keep source-specific restrictions.

`GET /Cashboxes`

Use existing View/Execute permissions; do not assume the Admin role is needed for delegated voucher execution.

`GET /FiscalYears/select`

Use the voucher's own year metadata to suppress writes for closed years. The server revalidates year and period.

### Readonly journal view

`GET /JournalEntries?fiscalYearId={voucherFiscalYearId}&sourceType=CashVoucher&sourceId={voucherId}&entryType=Automatic&status=Posted`

The existing list endpoint accepts optional `sourceType` and `sourceId` together. A partial pair is rejected. Company, fiscal-year and cashbox journal visibility still apply. Validate returned source identity and render the journal number, date, reference, lines and totals without editing or deletion. Preserve up to four decimal places for journal amounts, including genuine `0.0001` FX differences. Do not guess the corresponding journal by searching its description.

### Lock, conflicts and refresh

- After posting, amount, target, text, applied rate and base amounts are locked. Daily exchange-rate updates do not revalue or repost an independent posted voucher. Accounting readiness can rebuild from its saved snapshot.
- On `409`, show the message and reread the voucher in its own year. If another session posted it, close the editor and show readonly details. Never retry using a guessed version.
- `CashVouchers.PostedReadOnly` (`409`) rejects changes to posted independent vouchers. `CashVouchers.DeleteNotAllowed` (`409`) rejects their deletion. Existing concurrency/source errors remain applicable.
- Keep **Edit draft** and **Classify and post** separate. Saving a draft must never call the posting route.
- Update the cached record and revision after success. Refresh list and balances after posting. If a draft moves cashbox, refresh old-cashbox choices/views on the scoped cashbox notification.
- Cancel or ignore old detail/save responses on company/year/voucher changes.
- Existing posted vouchers remain readonly even when classification is blank. Historical posted documents missing a target require accounting review rather than silently reopening them.

## 6. الترقيم اليدوي للفواتير

This behavior was reviewed on November 14 and is already supported.

`POST /Invoices`

Supply `invoiceNumber` manually. It is required, trimmed, and limited to 100 characters. The current contract permits duplicate entered numbers.

`PUT /Invoices/{id}`

The invoice number is immutable after creation. Do not send `invoiceNumber` in the update payload; the UI shows it readonly. No separate numbering endpoint is required.

## 7. تعطيل الخزينة عند رصيد صفر

### Endpoints

`PUT /Cashboxes/{id}`

Use the existing full update payload with `isActive: false` and latest Base64 `rowVersion`. Keep original currency/opening fields when changing activity alone. Execute access is required.

The server checks the latest native-currency balance in the company's current fiscal year inside the transaction. Both positive and negative nonzero balances return `Cashboxes.NonZeroBalance` (`409`). Opening balance, carried native balance, posted vouchers and source-less posted manual native cashbox journal lines contribute. Drafts, excluded/deleted/reversed rows and base-only valuation differences do not count as cash units. A missing current year is not a verified zero balance.

Only name, notes or activity changes preserve the unchanged opening journal; they do not repost historical accounting. Reactivation remains available.

`GET /Cashboxes/select`

Returns active accessible cashboxes only, so a deactivated cashbox disappears from new transaction choices. This selector is not a historical archive picker.

`GET /Cashboxes`

Management retains inactive cashboxes. Use `isActive=false` to find them and the existing PUT to reactivate.

`GET /Cashboxes/{id}`

Authorized users can still read an inactive cashbox. Its history is retained and existing report APIs can still report by its ID.

### Frontend

Use the existing **الصندوق نشط** checkbox and show the zero-balance condition. Surface backend errors; a stale displayed balance must not override server validation. Refresh new transaction choices on Cashbox realtime updates.

`GET /Accounts/journal-select?fiscalYearId={yearId}`

Cashbox changes also refresh selectable accounts/parties in an already-open journal workspace. Preserve the draft inputs and ignore responses from cancelled option requests.

## 8. حذف الخزينة بعد حذف حركاتها

`DELETE /Cashboxes/{id}`

This deletes the cashbox master after all remaining movement dependencies have been cleared. Soft-deleted voucher history no longer prevents deletion.

The server still rejects deletion when any of the following remains:

- A non-deleted voucher, including a draft, in any fiscal year: `Cashboxes.HasVouchers` (`409`).
- An active transfer or live cashbox journal entry: `Cashboxes.HasMovements` (`409`).

The cashbox's own opening journal is removed atomically through its existing posting workflow. Closed-year accounting restrictions are preserved, and a failure rolls back the cashbox change.

Use the existing management-list delete action and display the API message if a dependency remains. Deletion is soft: normal lists/selectors hide the cashbox, while deleted historical records and audit information remain stored. This permission to delete the master does not enable deletion of independent cash vouchers described in section 5.

## Verification recorded for November 13–14

These are recorded development checks, not a new live execution performed when compiling this document.

| Verification stage | Backend | Frontend |
| --- | --- | --- |
| Voucher edit lock | 1,408 passed; 4 existing failures; 1,412 total. Related subset: 218 passed. | 66 passed; TypeScript and production build passed. |
| Cashbox deactivation/deletion and final invoice checks | 1,433 passed; 4 existing failures; 1,437 total. New cashbox tests: 25 passed. | 71 passed; TypeScript and production build passed. |

The four full-suite failures were already established outside these changes:

- `BusinessPartnerContainerStoreServiceTests.GetAll_FiltersBySpecialFlag`: SQLite fixture lacks `JournalEntryLines`.
- `BusinessPartnerContainerStoreServiceTests.GetAll_ReturnsOnlyActiveContainersAssignedToPartnerStore`: the same fixture issue.
- `EmployeeMovementServiceTests.GetAllAsync_WithTypeAndCurrencyFilter_ShouldWork`: fiscal-year name expectation differs from the fixture.
- `StockTransferServiceTests.Create_InOpenNonCurrentFiscalYear_CostsTheTransferInItsOwnYear`: fixture source-cost date falls outside its current year.

Checks covered permissions and hidden statement links, historical-year navigation, manual and carried driver openings, standalone trips, containers-only invoices, stale revisions, atomic batch rollback, posted rate preservation, zero-balance checks and master deletion dependencies. Journal rounding display and cancelled selector/detail responses were also checked.

The SSR test-harness cleanup race was fixed by disabling an unused client dependency scan in its owning test setup; product dependency optimization was not changed. The production build retained its existing bundle-size advisory.

Runtime acceptance with simultaneous SQL Server sessions, historical posted-voucher accounting review and deployed UI acceptance remains for the target environment. Automated database checks used isolated SQLite fixtures; no live database was modified during those checks.

## Consolidated source documents

The contracts and recorded verification above were consolidated from:

- November 13: `frontend-handoff-2026-11-13.md`.
- November 13: `frontend-driver-opening-balances.md`.
- November 13: `frontend-standalone-driver-trips.md`.
- November 14: `frontend-cash-voucher-edit-lock.md`.
- November 14: `frontend-cashbox-inactive-and-container-invoices.md`.
- November 14: `verification.md`.
- November 14: `verification-cashbox-inactive-and-deletion.md`.

The detailed draft-field rules were included from the existing `frontend-cashvoucher-draft-update.md` reference, and the confirmed manual invoice-number behavior from the November 14 review. This file is self-contained and does not require sending the individual handoff files.
