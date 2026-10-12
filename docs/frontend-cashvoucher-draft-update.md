# Edit a cash voucher draft before classification

```http
PUT /api/v1/CashVouchers/{id}/draft
Authorization: Bearer <token>
Content-Type: application/json
```

```json
{
  "voucherDate": "2026-08-01",
  "direction": "Receipt",
  "cashboxId": 5,
  "amount": 100,
  "description": "Cash handover",
  "notes": null,
  "referenceNumber": "REF-1",
  "exchangeRate": 52.25,
  "rowVersion": "<Base64 value from the latest voucher response>"
}
```

- Replaces the previous `PATCH /CashVouchers/{id}/draft-rate`; that route is removed. Available only for an existing standalone **draft**, in the current company's open, selected fiscal year, with Execute permission on both the original and requested cashboxes.
- This is full operational replacement: always send `voucherDate`, `direction` (`Receipt` / `Payment`), `cashboxId`, `amount`, and the latest `rowVersion`. You may change date, direction, cashbox, positive native amount, exchange rate, description, notes, and reference number. Omitted/null optional text clears that field. Text is trimmed; blank text becomes null. Amount must fit decimal(18,2); description/notes max 1,000 characters; reference max 100.
- Date must remain inside the draft's original, selected open fiscal year. Company, fiscal year, id, and voucher number cannot be changed. The voucher number stays stable even when direction changes. Currency is derived from the selected cashbox, not supplied by the caller. A newly selected cashbox must be active; the existing cashbox may remain selected if it was deactivated after creation.
- Do not send posting fields such as party, account, movement type, or `isPosted`: this endpoint does not classify the voucher. The request DTO exposes only operational draft fields.
- `exchangeRate` is company-base currency units per one cashbox currency unit. It must be positive, fit decimal(28,12), and produce a base amount fitting decimal(28,8). Base-currency vouchers accept only `1`.
- `exchangeRate` is optional. If omitted/null and currency/date are unchanged, a captured rate is retained and `baseAmount` recalculated for the new amount. An unresolved foreign draft remains rate `1` / base amount `0` (not a real quoted rate). If currency or date changes without an explicit rate, the old foreign rate is cleared and the draft is unpriced; no daily rate is required. Base-currency drafts use `1` and base amount equals native amount. Provide an explicit rate to capture a new voucher-local snapshot, without changing the daily rate table.
- The voucher stays `isDraft: true`: no cash balance effect, journal entry, partner/employee movement, or daily exchange-rate table update.
- Returns `200` with the full `CashVoucherResponse`. Replace the locally cached voucher, including `rowVersion`, with this response. Completion through `PUT /CashVouchers/{id}` can then use the updated revision and retain the captured rate when currency/date are unchanged and no replacement rate is sent.
- Invalid fields/rate/revision or date outside the selected fiscal year: `400`; no Execute access: `403`; missing/other-company voucher/cashbox: `404`; stale revision, closed/missing fiscal period, inactive new cashbox, posted voucher, or invoice/transfer-generated voucher: `409`. Reload on conflict; never retry with a guessed revision.
- A CashVouchers Updated realtime notification is emitted after success. If the cashbox changes, a scoped Cashboxes Updated notification also invalidates the old cashbox's viewers without exposing the moved voucher. This is a voucher-local rate, not an ExchangeRates update.

Frontend action: add a separate **Save draft** button calling this PUT with the complete operational fields above. Keep completion/classification on `PUT /CashVouchers/{id}`, which requires a posting target and posts the voucher. The new endpoint is implemented in the backend; wiring the separate draft action into the frontend is not part of this change.

No migration or database update is required.
