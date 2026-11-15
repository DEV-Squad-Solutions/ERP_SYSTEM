# MiniERP — Frontend integration handoff

November 13, 2026 · Today's change only: cashbox labels and voucher links in the partner statement. All paths below are relative to `/api/v1`. Use the current company's Bearer token.

## Backend: partner statement response

Affected endpoint:

```http
GET /Statements/partner?businessPartnerId=42&fiscalYearId=9&pageNumber=1&pageSize=20
```

The backend adds five nullable fields to each statement item. Example excerpt from an authorized voucher row:

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

The fields are populated only for a posted standalone voucher when the user can view its cashbox. All five are null for inaccessible cashboxes and non-voucher rows, including invoices, manual journal entries and opening balances. The backend does not filter out these rows or change their amounts, balances, totals or pagination. An invoice row is not a voucher link, even if the invoice was paid in cash.

## Frontend: display and navigation

Already wired in the reference React client. Apply the same behavior in the target frontend:

1. On an eligible row, show `من خزنة {cashboxName}` for `Payment`, or `إلى خزنة {cashboxName}` for `Receipt`, next to the document number. Make the number clickable only when the link fields are present and the cashbox is viewable. For null or missing fields, show the ordinary number and amounts without the cashbox label or link.
2. Open the exact voucher in a read-only cash voucher detail view. Use the row's `cashVoucherId` and `fiscalYearId`, not its document number, journal line ID, or the current year in the top bar.

   ```http
   GET /CashVouchers/37?fiscalYearId=9
   ```

3. Show the voucher number, date, fiscal year, cashbox, direction and amount in the voucher currency. Return to the same statement filters and page. Do not switch the application's fiscal year just to open a historical voucher.
4. The detail endpoint checks access again. On `403`, show access denied and clear stale details; on `404`, show that the voucher is unavailable; on `401`, follow the existing session flow. Clear the selection on company switch and ignore late responses from the previous context.

Acceptance: check receipt and payment labels, a historical year, two rows with the same document number, an account with View but not Execute, an account without View, and invoice/opening rows. Check that changing access does not change the partner statement balance.

## Rollout

Deploy the updated backend and frontend together. No migration is required for this change. If the API does not yet return the new fields, keep displaying the plain document number.
