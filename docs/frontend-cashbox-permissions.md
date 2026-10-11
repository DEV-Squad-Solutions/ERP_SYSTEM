# Cashbox permissions — Frontend integration

## Scope and rollout

Permissions are per **user + company + cashbox**, independent of fiscal year. Switching company must reload the current scope and cashbox selections; switching fiscal year keeps the same permissions but reloads financial data.

- `Admin` sees and executes all cashboxes only in companies they belong to.
- `allCashboxes: true` grants all current and future company cashboxes.
- Otherwise only explicit grants apply. No grants means no cashboxes, not unrestricted access.
- `canExecute` implies `canView`. View-only permits reading, not creating, completing, editing or deleting cash movements.
- Existing role restrictions still apply; cashbox permissions do not replace them.
- Apply the new `AddUserCashboxPermissions` migration using the normal deployment process. No live database migration is applied by this change. Assign non-admin permissions before rollout, otherwise existing non-admin users will see no cashboxes.

## Admin management

`GET /api/v1/Users/{userId}/cashbox-permissions`

Requires Admin; targets the selected company from authentication/company context. Target user must belong to that company.

```json
{
  "userId": "11111111-1111-1111-1111-111111111111",
  "companyId": 1,
  "allCashboxes": false,
  "revision": "00000000-0000-0000-0000-000000000000",
  "permissions": [
    { "cashboxId": 5, "canView": true, "canExecute": false },
    { "cashboxId": 8, "canView": true, "canExecute": true }
  ]
}
```

`PUT /api/v1/Users/{userId}/cashbox-permissions`

Replace the complete permission set, not an incremental patch. Send the last GET/PUT `revision` to prevent overwriting another administrator's changes.

```json
{
  "allCashboxes": false,
  "revision": "00000000-0000-0000-0000-000000000000",
  "permissions": [
    { "cashboxId": 5, "canView": true, "canExecute": false },
    { "cashboxId": 8, "canView": false, "canExecute": true }
  ]
}
```

Success returns the same schema as GET with a new revision; the second grant is normalized to `canView: true`. Duplicate cashbox IDs are merged. Removing a grant means omitting it from the complete list. Existing inactive cashbox grants may be retained for historical viewing; new grants require an active cashbox. A cashbox from another company is invalid.

Errors: `400` invalid grants, `403` insufficient authority/company access, `404` target user unavailable, `409` stale revision. On `409`, reload and let the administrator reconcile changes; do not silently overwrite.

## Current user's scope

`GET /api/v1/CashboxPermissions/current`

Available to authenticated users; resolves live database permissions, not cached cashbox claims in the token.

```json
{
  "hasAllCashboxes": false,
  "viewCashboxIds": [5, 8],
  "executeCashboxIds": [8]
}
```

When `hasAllCashboxes` is true, both arrays can be empty; do not interpret that as no access.

Recommended UI checks:

```ts
const canView = (id: number) => scope.hasAllCashboxes || scope.viewCashboxIds.includes(id);
const canExecute = (id: number) => scope.hasAllCashboxes || scope.executeCashboxIds.includes(id);
```

Reload this scope on login, company switch, and permission-management success. On backend `403`, refresh scope and relevant lists and display an access-denied message; hiding UI controls is not a security boundary. A permission revocation takes effect on the next backend request without logout.

## Screen behavior

- Cashbox lists, selections, voucher lists, handover data and dashboard cash balance cards include only viewable cashboxes. Totals/counts are calculated over the permitted rows before pagination. An explicit denied cashbox filter/detail returns `403`.
- Transfers require View on **both** source and destination to display the complete transfer; Execute on both to create/change/delete it. Voucher details belonging to transfers follow the same visibility boundary.
- Executing movements, completing drafts, bulk operations, changing cashboxes or deleting existing movements require Execute on all affected old and new cashboxes. View-only must not expose these buttons as enabled.
- Creating a cashbox requires full-company cashbox access. Existing cashbox edits require Execute, plus existing role rules.
- Cash-paid invoices with any inaccessible linked direct or allocated voucher are hidden as a whole; unpaid/credit invoices remain visible under the existing invoice rules. Do not expect a partially redacted invoice.
- Cashbox statements require View. Manual journal entries are visible only when every identifiable cashbox line is viewable, and cashbox-related writes require Execute. Historical ambiguous cashbox-account lines require full-company access; journal rows are never partially removed from a displayed entry.
- Full-company trial balance, operational trial balance, income statement, financial position and cash flow require `hasAllCashboxes`. Show a permission notice rather than presenting partial/imbalanced accounts.
- Partner, driver and employee statements keep their true balances and payment effects. For restricted users, journal navigation IDs are null; hidden driver cashbox names are null. Existing company-business references remain available, but opening a cash voucher or journal still enforces its own permissions. Disable journal navigation when `journalEntryId` is null.
- Dashboard cash balances/counts cover permitted cashboxes only; other company business indicators retain their existing scope. Label the cash section accordingly (for example “Accessible cashboxes”). No accessible cashboxes yields an empty cash section, not a company-wide zero balance.
- Journal account selection and cashbox account mappings exclude hidden cashbox parties/mappings.

## Realtime

Canonical `ReceiveEntityChanged` and legacy `entityChanged` event shapes are unchanged. Cashbox, voucher, transfer, invoice and journal invalidations are dispatched to current permitted company-user recipients, using live grants for each event. No client group-management API is required. Reconnect on company switch as before to join the new company's connection scope. A stale open screen should reload on actions/focus after a permission change; do not rely solely on invalidations to refresh the current permission set.

## Acceptance checks

1. User A views cashbox 5 and executes cashbox 8; user B sees only cashbox 9.
2. A sees 5 and 8 in dropdowns/dashboard, cannot submit a payment from 5, and cannot open 9 using a manually entered ID.
3. A cannot view or execute a transfer between 8 and 9.
4. Removing access to 8 blocks A's next request and prevents subsequent cash-sensitive invalidations reaching A.
5. An explicit all-cashboxes user sees newly created cashboxes; a user with an empty grant set sees none.
6. Company switch reloads permissions and does not reuse another company's cached IDs.
7. Fiscal year switch changes the displayed transactions without changing cashbox grants.
8. View-only users cannot edit/delete old movements or route around restrictions through invoice payments/manual journals.
