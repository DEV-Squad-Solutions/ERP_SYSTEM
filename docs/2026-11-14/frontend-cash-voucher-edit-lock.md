# Cash voucher editing and posting — 2026-11-14

Scope: standalone cash receipts/payments. Use `isDraft` to determine posting state; `classification` is optional and does not determine whether editing is allowed.

| Voucher | Available actions |
| --- | --- |
| Draft, own fiscal year open, Execute access | Edit draft; classify and post |
| Posted | View details only |
| Closed year or View-only access | View details only |
| Invoice/transfer-generated voucher | View; make permitted changes through its original source |
| Opening balance | Existing opening-balance/journal workflow |

Standalone vouchers cannot be deleted, either before or after posting. A posted voucher's amount, applied exchange rate, base amount, target, and text fields are locked. Changing a daily exchange rate does not revalue or repost an independent posted voucher.

## Endpoints

All routes below use the existing company authentication context and `/api/v1` prefix.

`GET /CashVouchers?fiscalYearId={yearId}`

Load the list for the selected fiscal year.

`GET /CashVouchers/{id}?fiscalYearId={voucherFiscalYearId}`

Read the latest voucher before opening an editor or after a conflict. Keep its `rowVersion` (Base64), `isDraft`, source IDs, and own `fiscalYearId`. A statement link must use the voucher's year.

`POST /CashVouchers`

Create a draft. Drafts do not affect the cash balance or accounting entries.

`PUT /CashVouchers/{id}/draft`

Save operational fields without posting. Send the complete replacement payload:

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

The date must remain in the voucher's own open fiscal year. Company, year and voucher number remain unchanged. Currency comes from the cashbox. Moving a draft requires Execute access to both cashboxes; a newly selected cashbox must be active. Positive payments are allowed even when the cashbox balance is zero or negative.

Optional text omitted or set to `null` is cleared. `exchangeRate` is base-currency units per one cashbox currency unit. Base currency uses `1`. If no rate is sent and date/currency are unchanged, retain the captured rate. Changing date/currency without supplying a foreign rate leaves the draft unpriced until completion. Saving a draft does not update daily exchange rates. Full field limits and rate semantics: [draft API details](../frontend-cashvoucher-draft-update.md).

`PUT /CashVouchers/{id}`

Classify and post an existing draft exactly once. Keep the existing full completion payload, including operational fields, a valid posting target and the latest `rowVersion`. Show an explicit confirmation that posting locks the voucher. A successful response has `isDraft: false`. Cash balance, linked movements and journal posting are committed together. Negative cashbox balance does not block valid completion.

`POST /CashVouchers/bulk`

The request is atomic. `Add` without a posting target creates a draft; `Add` with a valid target creates and posts it. `Update` completes an existing draft using the same posting rules and current revision. Posted `Update` and every independent `Delete` are rejected; any rejected item rolls back the whole batch. A selected movement type is an existing valid posting route when its account mapping is valid.

`DELETE /CashVouchers/{id}`

Independent voucher deletion returns `409`, including drafts. Remove the delete action from the UI. Source-generated vouchers retain their source-specific restrictions.

`GET /Cashboxes`

Use the existing per-cashbox View/Execute permissions. Draft writes require Execute on the original and requested cashbox.

`GET /FiscalYears/select`

Use the voucher's own year metadata to hide write actions for closed years. The server validates the year and fiscal period again on each write.

`GET /JournalEntries?fiscalYearId={voucherFiscalYearId}&sourceType=CashVoucher&sourceId={id}&entryType=Automatic&status=Posted`

View the posted independent voucher's exact journal, including its readonly lines and totals. `sourceType` and `sourceId` are new optional filters on the existing list endpoint; send both together (a partial pair is rejected). Existing company, fiscal-year and journal/cashbox visibility checks still apply. No journal edit/delete action is exposed here. Do not search by description to guess the corresponding journal.

## Conflicts and frontend behavior

- Replace the cached voucher and its revision with each successful response. Refresh balances after posting.
- On `409`, show the server message and reload the voucher in its own year. If another session posted it, close the editor and show readonly details. Do not retry with a guessed revision.
- `CashVouchers.PostedReadOnly` (`409`) rejects attempts to change a posted independent voucher.
- `CashVouchers.DeleteNotAllowed` (`409`) rejects independent deletion. Generated vouchers keep their existing error codes.
- Keep separate **Edit draft** and **Classify and post** actions; a draft save must never call the posting endpoint.
- Cancel or ignore pending editor responses after changing company, fiscal-year context, or selected voucher.

## Rollout

No new schema migration is needed for this change. Old independent vouchers already marked posted stay readonly, even when classification is blank. Before rollout, review the read-only [legacy inventory SQL](cash-voucher-legacy-inventory.sql) against the intended company/year. It does not alter data. Any historical no-target voucher needing correction requires an agreed accounting correction; this feature does not reopen posted history.

Runtime acceptance on the deployed environment should include two concurrent SQL Server sessions saving/posting the same draft. Automated stale-session tests and transaction/revision checks cover competing revisions; they do not replace a simultaneous SQL Server transaction check. The inventory query is supplied for review and is not automatically executed against a live database.

Executed checks and the existing full-suite failures are recorded in [verification results](verification.md).
