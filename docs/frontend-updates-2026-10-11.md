# Frontend Update Summary — October 11, 2026

Covers the six commits below. All endpoint paths use the `/api/v1` prefix.

## 1. Allow negative cashbox balances — `1468306`

Remove insufficient-balance checks that prevent payments or draft completion. Display negative balances as returned. Voucher amounts remain positive; use `Payment` for outgoing cash. Permissions, fiscal-year locks, and other validation still apply.

Affected endpoints:

- `POST /CashVouchers`
- `PUT /CashVouchers/{id}`
- `DELETE /CashVouchers/{id}`
- `POST /CashVouchers/bulk`
- `POST /CashboxTransfers`
- `PUT /CashboxTransfers/{id}`
- `DELETE /CashboxTransfers/{id}`
- `POST /Invoices` — when cash payments are involved.
- `PUT /Invoices/{id}` — when cash payments are involved.
- `DELETE /Invoices/{id}` — when cash payments are involved.
- `POST /CashboxRevaluations` — foreign-currency revaluation also supports negative balances.

## 2. Opening-balance rows in statements — `3530382`

Display the opening row returned by the server. Disable edit/delete when `isOpeningBalance` or `isReadOnly` is true. It appears on the first page and is included in `totalCount`; do not add another row from `summary` or count it as a period movement.

Affected endpoints:

- `GET /Statements/cashbox?cashboxId=...`
- `GET /Statements/partner?businessPartnerId=...`
- `GET /Statements/driver?driverId=...`
- `GET /Statements/employee?employeeId=...`
- `GET /dashboard/payroll/employees/statement?employeeId=...` — employee-statement alias.

These statements accept `fiscalYearId`, `fromDate`, `toDate`, `pageNumber`, and `pageSize`.

## 3. Capture the cashbox exchange rate before draft completion — `c951e13`

Add optional `exchangeRate` to draft creation and display `baseCurrency` and `baseAmount`. Drafts do not affect actual balances. Completing a draft without a rate preserves its captured rate if the date and currency are unchanged. Editing an already posted voucher without a rate resolves the registered rate again.

Affected endpoints:

- `POST /CashVouchers` — capture the draft rate.
- `PUT /CashVouchers/{id}` — complete or edit the voucher.
- `GET /CashVouchers` — display the rate and base amount.
- `GET /CashVouchers/{id}` — display the rate and base amount.
- `GET /CashVouchers/handover-report` — display draft rates/base amounts and distinguish actual from expected balances.

A foreign-currency draft created without a rate may temporarily return `exchangeRate = 1` and `baseAmount = 0`. Do not treat these as final conversion values.

## 4. User cashbox permissions — `f84db55`

Permission flow:

1. Admin assigns the user's cashbox permissions for the selected company through `PUT /Users/{userId}/cashbox-permissions`. They are stored in the database.
2. The backend identifies the user from the access token and the selected company, then reads their current database permissions.
3. `GET /Cashboxes` automatically returns only viewable cashboxes. The frontend does not send a list of allowed IDs.
4. The frontend calls `GET /CashboxPermissions/current` and uses `executeCashboxIds` or `hasAllCashboxes` to enable payment/edit controls. The backend checks permissions again when executing an operation.

Permissions are per user and company, not fiscal year. A cashbox appearing in a list does not necessarily grant permission to execute movements on it.

New endpoints:

- `GET /CashboxPermissions/current` — returns `hasAllCashboxes`, `viewCashboxIds`, and `executeCashboxIds`.
- `GET /Users/{userId}/cashbox-permissions` — read a user's permissions; Admin only.
- `PUT /Users/{userId}/cashbox-permissions` — replace the entire permission set using `allCashboxes`, `permissions`, and the latest `revision`; Admin only.

Existing affected endpoints:

- `GET /Cashboxes` — viewable cashboxes only.
- `GET /Cashboxes/select` — viewable selections; check Execute before using a selection for an operation.
- `GET /Cashboxes/{id}`
- `POST /Cashboxes` — requires full-company cashbox access.
- `PUT /Cashboxes/{id}`
- `DELETE /Cashboxes/{id}`
- `GET /CashVouchers`
- `GET /CashVouchers/{id}`
- `POST /CashVouchers`
- `PUT /CashVouchers/{id}`
- `DELETE /CashVouchers/{id}`
- `POST /CashVouchers/bulk`
- `GET /CashVouchers/handover-report`
- `GET /CashboxTransfers`
- `GET /CashboxTransfers/{id}`
- `POST /CashboxTransfers`
- `PUT /CashboxTransfers/{id}`
- `DELETE /CashboxTransfers/{id}`
- `GET /Invoices`
- `GET /Invoices/{id}`
- `GET /Invoices/return-sources`
- `POST /Invoices`
- `PUT /Invoices/{id}`
- `DELETE /Invoices/{id}`
- `GET /JournalEntries`
- `GET /JournalEntries/{id}`
- `POST /JournalEntries`
- `PUT /JournalEntries/{id}`
- `DELETE /JournalEntries/{id}`
- `GET /Statements/cashbox` — requires View on the cashbox.
- `GET /Statements/partner` — balances remain complete; journal navigation IDs may be `null`.
- `GET /Statements/driver` — balances remain complete; journal navigation IDs may be `null`.
- `GET /Statements/employee` — balances remain complete; journal navigation IDs may be `null`.
- `GET /dashboard/payroll/employees/statement` — employee-statement alias.
- `GET /Statements/trial-balance` — requires access to all cashboxes.
- `GET /Statements/operational-trial-balance` — requires access to all cashboxes.
- `GET /Statements/expenses` — requires access to all cashboxes.
- `GET /Statements/income-statement` — requires access to all cashboxes.
- `GET /Statements/financial-position` — requires access to all cashboxes.
- `GET /Statements/cash-flow` — requires access to all cashboxes.
- `GET /Dashboard` — the cash section covers viewable cashboxes only.
- `GET /CashboxRevaluations`
- `POST /CashboxRevaluations`
- `GET /MonetaryAccountRevaluations/options`
- `GET /MonetaryAccountRevaluations`
- `POST /MonetaryAccountRevaluations`

Do not display partial financial reports on `403`. Transfers require permissions on both cashboxes; changes require Execute on all affected old and new cashboxes. Linked invoices and journal entries must not be displayed as partially redacted records.

Reload permissions on login, company switch, and permission changes. `hasAllCashboxes = true` grants all company cashboxes even when the ID arrays are empty. No grants means no viewable cashboxes. Existing role restrictions still apply.

## 5. Disable automatic seeding — `b86c3be`

Seeding is disabled on startup and database-connection recovery without deleting existing data. This commit does not change the migration-startup setting.

Affected endpoints: none. No frontend screen or API request change is required.

## 6. Cross-currency partner settlements — `5aefdf1`

`amount` and `exchangeRate` always describe the cashbox side. Add optional `partnerExchangeRate` during completion and display `partnerCurrency`, `partnerAmount`, and `partnerBaseAmount` from the response. The partner account moves in its own currency.

Both rates mean company-base currency units per one unit of the relevant currency. If the partner rate is omitted, the backend resolves an applicable rate within the company and fiscal year covering the voucher date. An existing manual partner rate is retained when the partner, currency, and date are unchanged.

Affected endpoints:

- `PUT /CashVouchers/{id}` — accept the partner rate for partner vouchers.
- `POST /CashVouchers/bulk` — Add/Update accept the partner rate.
- `GET /CashVouchers` — new partner fields; they may be `null` on historical or non-partner vouchers.
- `GET /CashVouchers/{id}` — new partner fields.
- `GET /Statements/partner` — settlement amounts in the partner's currency.
- `PUT /ExchangeRates/{id}` with `updateLinkedTransactions = true` — correct base values while preserving original settled amounts.
- `DELETE /ExchangeRates/{id}` — prevent deleting a rate referenced by either side of a voucher.

Existing helper endpoints:

- `GET /ExchangeRates/resolve?currency=USD&date=...&fiscalYearId=...` — resolve a rate.
- `GET /BusinessPartners/{id}` — read the partner's currency.
- `GET /CashMovementTypes/select` — select the movement descriptor.

Do not send `partnerExchangeRate` in the initial `POST /CashVouchers`. For payments to customers, select a descriptor marked `isDefaultForSalesReturn`; for receipts from suppliers, use `isDefaultForPurchaseReturn`. Do not hard-code descriptor IDs. The existing invoice-payment allocation workflow is unchanged.

## Deployment and shared frontend rules

- Apply `AddUserCashboxPermissions` and `AddCrossCurrencyPartnerVoucherSettlements` if not already applied. Assign non-Admin cashbox grants before rollout.
- Send `fiscalYearId` in queries that accept it. Reload permissions and data on company switch; reload financial data on fiscal-year switch.
- Use the server's `rowVersion` for voucher updates and `revision` for permission updates. On `409`, reload before retrying. On `403`, show access denied, not a zero balance.
- Additional integration details: [Cashbox permissions](frontend-cashbox-permissions.md) and [Cross-currency settlements](frontend-cross-currency-partner-settlements.md).
