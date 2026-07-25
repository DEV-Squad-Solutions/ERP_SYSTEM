# Partner Opening Balance frontend contract

The authenticated company is selected by the access token's single
`company_id` claim. The client must not send `companyId`.

## Routes

```text
GET    /api/v1/PartnerOpeningBalances?pageNumber=1&pageSize=10
GET    /api/v1/PartnerOpeningBalances/{id}
POST   /api/v1/PartnerOpeningBalances
PUT    /api/v1/PartnerOpeningBalances/{id}
DELETE /api/v1/PartnerOpeningBalances/{id}
```

List and detail responses use the same complete item shape. Paginated items
are not reduced header-only rows:

```json
{
  "id": 1,
  "companyId": 1,
  "businessPartnerId": 12,
  "businessPartnerName": "Ahmed Mohamed Trading",
  "documentNumber": "PARTNER-OPEN-001",
  "documentDate": "2026-01-01",
  "currency": "EGP",
  "balanceType": "Receivable",
  "amount": 2500.00,
  "notes": "Opening balance",
  "rowVersion": "AAAAAAAAB9E="
}
```

Create and update send:

```json
{
  "businessPartnerId": 12,
  "documentNumber": "PARTNER-OPEN-001",
  "documentDate": "2026-01-01",
  "currency": "EGP",
  "balanceType": "Receivable",
  "amount": 2500.00,
  "notes": "Opening balance"
}
```

Update must also send the `rowVersion` returned by the loaded item. The
original client token is used for optimistic concurrency; a stale token returns
HTTP 409 with `PartnerOpeningBalances.Concurrency`, and the client should
reload the item before retrying.

`currency` accepts `EGP`, `USD`, `EUR`, `GBP`, `SAR`, `AED`, or `KWD`.
`balanceType` accepts `Receivable` or `Payable`. The partner must be active in
the selected company and the supplied currency must match that partner's
currency. Amounts are positive with at most two decimal places, document
numbers are trimmed and limited to 50 characters, and notes are optional up to
1,000 characters.

This feature has no status, posting, cancellation, reversal, or partner
movement operations.
