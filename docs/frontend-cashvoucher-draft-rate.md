# Edit a cash voucher rate before classification

```http
PATCH /api/v1/CashVouchers/{id}/draft-rate
Authorization: Bearer <token>
Content-Type: application/json
```

```json
{
  "exchangeRate": 52.25,
  "rowVersion": "<Base64 value from the latest voucher response>"
}
```

- Available only for an existing standalone **draft**, in the current company's open, selected fiscal year, with Execute permission on its cashbox.
- `exchangeRate` is company-base currency units per one cashbox currency unit. It must be positive, fit decimal(28,12), and produce a base amount fitting decimal(28,8). Base-currency vouchers accept only `1`.
- Only the voucher's rate and calculated `baseAmount` change, alongside modification time and `rowVersion`. Date, native amount, cashbox, currency, and posting details remain unchanged.
- The voucher stays `isDraft: true`: no cash balance effect, journal entry, partner/employee movement, or daily exchange-rate table update.
- Returns `200` with the full `CashVoucherResponse`. Replace the locally cached voucher, including `rowVersion`, with this response. Completion through `PUT /CashVouchers/{id}` can then use the updated revision and retain the captured rate when currency/date are unchanged and no replacement rate is sent.
- Invalid rate/revision or date outside the selected fiscal year: `400`; no Execute access: `403`; missing/other-company voucher: `404`; stale revision, closed/missing fiscal period, posted voucher, or invoice/transfer-generated voucher: `409`. Reload on conflict; never retry with a guessed revision.
- A CashVouchers Updated realtime notification is emitted after success. This is a voucher-local rate, not an ExchangeRates update.

Frontend action: add a separate **Save draft rate** button for draft rows, calling this PATCH with only the two fields above. Keep completion/classification on the existing PUT action. The new endpoint is implemented in the backend; wiring this separate action into the frontend is not part of this endpoint change.

No migration or database update is required.
