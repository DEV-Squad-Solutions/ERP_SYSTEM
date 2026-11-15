# Zero-balance cashbox deactivation and containers-only invoices

## Item 7 — deactivate a cashbox

Use the existing **Cashboxes** form and clear **Cashbox active**. Deactivation requires a zero current balance in the company's current fiscal year, measured in the cashbox's own currency. A positive or negative balance must first be settled. Re-activation remains available from the management list.

The server checks the latest balance inside the update transaction. Opening balances, carried native balances, posted cash vouchers and posted manual native cashbox journal lines contribute; drafts and base-only foreign-exchange valuation differences do not represent cash units. A missing current fiscal year does not count as a verified zero balance.

Changing only activity, name or notes does not repost an unchanged opening journal. Existing transactions and balances are retained.

`PUT /api/v1/Cashboxes/{id}`

Send the existing complete cashbox update payload, with `isActive: false` and the latest Base64 `rowVersion`. Keep the original currency/opening fields when changing activity alone. This endpoint requires Execute access to the cashbox. Nonzero balance returns `Cashboxes.NonZeroBalance` (`409`); show the server message and refresh the balance. Existing concurrency conflicts still require reloading the record.

`GET /api/v1/Cashboxes/select`

Only active, accessible cashboxes are returned. A deactivated cashbox disappears from new transaction choices. Refresh options when receiving the existing Cashboxes realtime update. This selector does not provide an archive picker for historical reports.

`GET /api/v1/Cashboxes`

The management list retains inactive cashboxes. Use `isActive=false` to find them and re-activate through the existing PUT. Existing View/Execute permissions still apply.

`GET /api/v1/Cashboxes/{id}`

An authorized user can still read an inactive cashbox. Activity does not delete its transaction history or prevent existing report APIs from reporting its ID. Existing records retain their original cashbox reference.

`DELETE /api/v1/Cashboxes/{id}`

The user can delete the cashbox itself after all its movements have been deleted. Historical soft-deleted vouchers no longer block this action. Any remaining non-deleted voucher (including a draft, in any fiscal year), active transfer or live cashbox journal entry still prevents deletion. These return `Cashboxes.HasVouchers` or `Cashboxes.HasMovements` (`409`). The existing UI delete action uses this endpoint; display the server error when a dependency remains.

Deletion is soft: the cashbox disappears from normal lists and selectors, while historical deleted records remain stored. Its own opening journal is removed through the existing atomic posting workflow; closed-year accounting restrictions are preserved. This changes deletion of the **cashbox master**, not the separate rule prohibiting deletion of independent cash vouchers after the previous edit-lock change.

## Item 8 — containers-only invoices

This workflow is already implemented in both applications. In the invoice form choose **Invoice content → Containers only** (`محتوى الفاتورة ← عبوات فقط`), select the container store and add the outgoing/incoming container quantities. Item lines are not required.

`POST /api/v1/Invoices`

Use the normal invoice endpoint with:

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

This is a field excerpt: supply the other required invoice fields as usual. `storeId` and `containerStoreId` use the selected container store. Include at least one valid container movement. Do not send item lines with `Containers`. The existing workflow records container quantities, with zero invoice value; it does not introduce pricing for containers.

`PUT /api/v1/Invoices/{id}`

Existing editing uses the same content and line fields plus the latest `rowVersion`. Keep the immutable invoice number out of the update payload. The UI clears item lines, discount, payment and cashbox when switching to containers-only content.

`GET /api/v1/Invoices`

`GET /api/v1/Invoices/{id}`

The response retains `contentType: "Containers"`, empty item `lines` and the saved `containerLines`. Container stock movements are created without item stock movements. The existing workflow covers sales, purchases and their returns, within the normal company/fiscal-year rules.

No new endpoint or schema migration is needed for either item.

Executed checks are recorded in [verification results](verification-cashbox-inactive-and-deletion.md).
