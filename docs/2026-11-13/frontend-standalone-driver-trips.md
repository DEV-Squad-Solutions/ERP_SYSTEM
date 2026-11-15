# Standalone driver trips and container-only invoices

All endpoints are relative to `/api/v1`. Use the selected company's Bearer token and send the selected `fiscalYearId`. Trip creation, editing, deletion and bulk cost updates require `Admin` and an open fiscal year.

## Standalone driver trips

The reference client adds an independent trip form to the existing driver trip costs screen. A trip can have a driver, date, optional business partner, actual driver name, cost and notes. Its number is generated as `TR-{driverTripId}`. Cost uses the company's base currency. The selected partner identifies the trip's customer or supplier, and the cost accrues to the driver.

```http
GET /FiscalYears/select
GET /Drivers/select
GET /BusinessPartners/select
GET /DriverTrips/cost-entry?fiscalYearId=9&isStandalone=true&pageNumber=1&pageSize=20
GET /DriverTrips/123?fiscalYearId=9
POST /DriverTrips
PUT /DriverTrips/123
DELETE /DriverTrips/123?rowVersion=<url-encoded-base64>
PUT /DriverTrips/bulk-costs
GET /Statements/driver?driverId=42&fiscalYearId=9&pageNumber=1&pageSize=20
```

Create a trip using the following body. Both null and zero cost are supported. Decimal strings preserve exact cents in JavaScript. The cost limit is 15 integer digits and two decimal places.

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

PUT accepts the same fields plus the latest Base64 `rowVersion`. Keep the fiscal year fixed on edit. Notes are limited to 1,000 characters and actual driver name to 200 characters. DELETE supplies the current version in the query string and returns `204` on success.

Cost-entry and detail responses retain the existing fields and add `isStandalone`, `isReadOnly`, `baseCurrency`, `actualDriverName`, and `costText`. Invoice and business-partner IDs and names can be null. Display the generated trip number and an empty invoice indicator for independent trips. Use `costText` for editing to preserve precision. `isStandalone` on the list query is optional.

Show standalone edit/delete actions when the user can manage the selected open year and the returned row is editable. Invoice-owned trips use the invoice and the existing bulk cost editor. A cash voucher reference, including one in a soft-deleted voucher, blocks deleting the trip or changing its driver, partner or date with `DriverTrips.LinkedVoucher`. Cost and notes retain the existing editing flow. Reload stale rows on a concurrency conflict.

## Accounting, statements and vouchers

A positive trip cost creates a balanced automatic journal using `DriverTripExpense` and `DriverControl`, tagged with the driver. A null or zero cost removes the trip's cost journal. The trip and journal changes commit together. The driver statement includes the posted trip cost and derives the driver's balance from the ledger. Paying the driver uses the existing cash voucher flow.

For a driver voucher, load `/DriverTrips/cost-entry` with the chosen driver and the voucher's `fiscalYearId`. Use the returned `driverTripId` as the reference. Refresh the trip list and statement on `DriverTrip`, ledger or fiscal-year updates. A closed year is read-only. Year closing carries the driver's remaining ledger balance into the following year.

## Container-only invoice

The invoice content option is labelled `عبوات فقط`. Its existing API contract uses `contentType: "Containers"`, an empty `lines` array, and one or more `containerLines` containing `containerId`, `outgoingUnits`, and `incomingUnits`. Choose the partner's container store. This flow records container quantities.

```http
POST /Invoices
PUT /Invoices/{id}
GET /Invoices?fiscalYearId=9
```

## Rollout

Apply `20261113135739_AllowStandaloneDriverTrips` before deploying the updated API. Verify a trip with no partner and no invoice, both cost states, cost amendment and deletion, closed-year protection, voucher linking in the correct fiscal year, the driver statement, and coexistence with invoice-generated trips. Verify that a container-only invoice can be saved without item lines.
