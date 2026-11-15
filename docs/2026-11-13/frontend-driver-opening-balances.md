# Driver opening balances

This handoff covers client item 2. All paths are relative to `/api/v1`. Send the selected company's Bearer token. The list and statement use the existing company access roles. Adding, editing, and deleting a manual opening requires `Admin` and an open fiscal year.

## Screen and endpoints

Show the driver, fiscal year, document number and date, amount, currency, side, and notes. Filter by fiscal year and optionally by driver. Initialize the year from the top bar and send the chosen `fiscalYearId` on each list and statement request. The API defaults the list to the company's current fiscal year when the filter is omitted.

```http
GET /FiscalYears/select
GET /Drivers/select
GET /DriverOpeningBalances?fiscalYearId=9&driverId=42&pageNumber=1&pageSize=20
GET /DriverOpeningBalances/123?fiscalYearId=9
POST /DriverOpeningBalances
PUT /DriverOpeningBalances/123
DELETE /DriverOpeningBalances/123?rowVersion=<url-encoded-base64>
GET /Statements/driver?driverId=42&fiscalYearId=9&pageNumber=1&pageSize=20
```

POST accepts the following body. The server assigns the first day of the fiscal year and the company's base currency.

```json
{
  "driverId": 42,
  "fiscalYearId": 9,
  "amount": "1500.25",
  "balanceType": "Payable",
  "notes": "Balance at start of year"
}
```

- `Payable` means the company owes the driver (`له`). `Receivable` means the driver owes the company (`عليه`). Send a positive decimal amount with up to 15 integer digits and two decimal places. JSON numbers are also accepted. Notes are optional and limited to 1,000 characters.
- PUT also takes the latest `rowVersion` from the row. Keep the driver and fiscal year fixed on edit. DELETE takes the URL-encoded version and returns `204` on success. If the API returns a concurrency conflict, reload the row before retrying.
- List items provide `id`, `driverId`, `driverName`, `fiscalYearId`, `fiscalYearName`, `documentNumber`, `documentDate`, `amount`, `amountText`, `currency`, `balanceType`, `notes`, `isCarriedForward`, `isReadOnly`, and `rowVersion`. Use `amountText` to preserve decimal precision when displaying or editing a balance in JavaScript. Hide edit and delete when `isReadOnly` is true. A carried row has `isCarriedForward: true`, a synthetic ID, and a null `rowVersion`.
- An existing manual or carried opening prevents a second opening for the same driver and year. If closing would carry a driver balance into a year that already has a manual opening for that driver, the close returns a conflict. Show the API's message and reload the selected year after successful changes.
- Use the active-driver selector for new balances. Historical rows remain visible for inactive drivers. The reference client also lets users filter by clicking a driver's name in an existing row.

## Driver statement and rollout

Render the single opening row returned by `/Statements/driver` with `isOpeningBalance: true` and `isReadOnly: true`. Use the server's `balanceAmount` and `balanceDescription`. The opening affects the running balance. The cash paid, cash received, and trip cost fields on that row are zero. Refresh the list and statement after a manual change or fiscal-year closing.

The server posts a balanced journal using the `DriverControl` and `OpeningBalanceEquity` mappings for the selected year. Manage a dedicated manual opening through its opening-balance document. A fiscal-year closing journal containing driver carry is read-only in the journal screen.

Apply migration `20261113121824_AddDriverOpeningBalances` before deploying the updated API. Verify each balance side, duplicate rejection, editing and deleting an open-year manual balance, the statement totals, and a read-only carry after closing.
