# Stock Opening Balance frontend contract

Base route: `/api/v1/StockOpeningBalances`

The selected company is taken from the authenticated token `company_id`. The
frontend must not send `companyId`. This feature is draft CRUD only: it has no
document status, post, cancel, or movement endpoint.

## Authorization

- `GET /` and `GET /{id}` require an authenticated bearer token.
- `POST /`, `PUT /{id}`, and `DELETE /{id}` require the `Admin` role.

## Requests

Create:

```json
{
  "storeId": 1,
  "documentNumber": "OPEN-001",
  "documentDate": "2026-01-01",
  "notes": "Opening stock",
  "lines": [
    {
      "itemId": 10,
      "count": 20,
      "weight": 5.000000,
      "price": 12.50,
      "notes": "Initial count"
    }
  ]
}
```

Update uses the same fields plus the `rowVersion` returned by the API:

```json
{
  "storeId": 1,
  "documentNumber": "OPEN-001",
  "documentDate": "2026-01-01",
  "notes": "Updated count",
  "rowVersion": "AAAAAAAAB9E=",
  "lines": [
    {
      "itemId": 10,
      "count": 25,
      "weight": 5.000000,
      "price": 12.50,
      "notes": null
    }
  ]
}
```

Do not send `companyId`, `itemUnitId`, `quantity`, `total`, `DocumentStatus`,
or posting/cancellation fields. `itemUnitId` and `itemUnitName` are nullable
response fields; when available, the API derives them from the item. The API
calculates:

```text
quantity = count × weight
total = quantity × price, rounded to 2 decimal places
```

## Responses

`GET /` returns the shared paged shape. Every item contains the header and its
complete ordered `lines` collection:

```json
{
  "id": 1,
  "companyId": 1,
  "storeId": 1,
  "storeName": "Main Store",
  "documentNumber": "OPEN-001",
  "documentDate": "2026-01-01",
  "notes": "Opening stock",
  "lineCount": 1,
  "rowVersion": "AAAAAAAAB9E=",
  "lines": [
    {
      "id": 1,
      "companyId": 1,
      "stockOpeningBalanceId": 1,
      "itemId": 10,
      "itemCode": "ITEM-0010",
      "itemName": "Example item",
      "itemUnitId": 2,
      "itemUnitName": "Piece",
      "count": 20,
      "weight": 5.0,
      "quantity": 100.0,
      "price": 12.5,
      "total": 1250.0,
      "notes": "Initial count"
    }
  ]
}
```

`GET /{id}`, create, and update return the header plus `lines`:

```json
{
  "id": 1,
  "companyId": 1,
  "storeId": 1,
  "storeName": "Main Store",
  "documentNumber": "OPEN-001",
  "documentDate": "2026-01-01",
  "notes": "Opening stock",
  "rowVersion": "AAAAAAAAB9E=",
  "lines": [
    {
      "id": 1,
      "companyId": 1,
      "stockOpeningBalanceId": 1,
      "itemId": 10,
      "itemCode": "ITEM-0010",
      "itemName": "Example item",
      "itemUnitId": 2,
      "itemUnitName": "Piece",
      "count": 20,
      "weight": 5.0,
      "quantity": 100.0,
      "price": 12.5,
      "total": 1250.0,
      "notes": "Initial count"
    }
  ]
}
```

Document numbers are trimmed and unique per company among non-deleted records.
The store must be an active product store in the selected company. Lines must
contain 1–100 unique active items. `count` and `weight` must be greater than
zero. `price` may be zero but cannot be negative. Weight and calculated
quantity use `decimal(18,6)`; price and calculated total use `decimal(18,2)`.
Header and line notes are limited to 1,000 characters. Missing or cross-company
records return `404`; validation returns `400`; duplicate, inactive, or
invalid-state conflicts return `409`. A stale `rowVersion` on update returns
`409` with `StockOpeningBalances.Concurrency`.

Every successful update returns a new `rowVersion`, including updates that
change only line quantities or notes.

Delete is a soft delete and returns `204 No Content`.
