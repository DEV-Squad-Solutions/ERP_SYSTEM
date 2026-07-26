# MiniErp Invoice Sidebar Tasks

This file consolidates the current approved instructions for the Invoice tasks
shown in the Codex sidebar. It is the implementation checklist for Tasks 0–7
and supersedes the older lifecycle, posting, cancellation, reversal, and
movement instructions that may still appear in an earlier task preview.

Last reviewed: 2026-07-25

## Portable handoff for another PC

### Backend repository

- Remote: `https://github.com/DEV-Squad-Solutions/ERP_SYSTEM.git`
- Current branch at the time of this handoff: `Invoices`
- Base commit at the time of this handoff:
  `cab6669467c7598180ddb8e1a96a347fe38bcad7`
- Solution: `MiniErp.slnx`
- Backend repository folder on the current PC:
  `G:\test\miniErp\MiniErp`
- Target framework: .NET 10
- Database provider: SQL Server through EF Core 10
- Tests use xUnit and relational SQLite where appropriate.

Example setup on another Windows PC:

```powershell
git clone https://github.com/DEV-Squad-Solutions/ERP_SYSTEM.git
Set-Location ERP_SYSTEM
git switch Invoices
dotnet --info
dotnet restore MiniErp.slnx
dotnet build MiniErp.slnx --configuration Release --no-restore
dotnet test MiniErp.slnx --configuration Release --no-build --no-restore
```

The `Invoices` branch and this document must be committed and pushed before
another PC can obtain the latest state. At the time this document was created,
the backend checkout still contained uncommitted changes to:

- `src/MiniErp.Domain/Entities/Invoicing/Invoice.cs`
- `docs/INVOICE_SIDEBAR_TASKS.md`
- A user-specific publish-profile file unrelated to these tasks:
  `src/MiniErp.Api/Properties/PublishProfiles/site80382-WebDeploy.pubxml.user`

Do not commit the user-specific publish-profile file unless that change is
intentional.

### Frontend repository

- Remote: `https://github.com/shabanelmogy/client.git`
- Current branch at the time of this handoff: `main`
- Current folder: `G:\test\miniErp\client`
- Runtime: React 19, TypeScript, and Vite
- Build command:

```powershell
Set-Location path\to\client
npm ci
npm run build
```

The frontend is a separate Git repository. A backend commit does not include
frontend work. The current frontend checkout contains uncommitted/staged UI
changes for partner/container setup. Review its Git status before committing.
Avoid committing generated `node_modules`, Vite cache metadata,
`*.tsbuildinfo`, or `dist` output unless the repository deliberately tracks
those files.

### Configuration and secrets

Do not copy production secrets into Git. Supply environment-specific values
through configuration or environment variables, including:

```text
ConnectionStrings__DefaultConnection
Jwt__SigningKey
Database__ApplyMigrationsOnStartup
Seed__Enabled
Seed__Password
```

When startup migrations are enabled, the application applies every pending
migration before serving requests. Never move an unreviewed migration to
another PC or environment with that option enabled.

### Source instruction documents

On the original PC, the long-form source documents are located at:

```text
G:\test\miniErp\MyDocuemnts-main\miniErp backend\FEATURE_DEVELOPMENT_GUIDE.md
G:\test\miniErp\MyDocuemnts-main\miniErp backend\INVOICE_FEATURE_SPECIFICATION.md
G:\test\miniErp\MyDocuemnts-main\miniErp backend\INVOICE_IMPLEMENTATION_PLAN.md
```

Those paths are machine-specific. This file records all task-specific
decisions needed to resume work, but the three source documents should also be
copied or cloned on the other PC because they contain the complete project-wide
development and verification policy.

## Execution order

| Step | Sidebar task | Thread ID | Current state |
|---:|---|---|---|
| 0 | Invoice 0 – Reference Data | `019f97eb-c87d-7211-afbf-bc9bb1e69f96` | Implemented |
| 1 | Invoice 1 – Stock Opening Balances | `019f97eb-f38e-7610-a2cf-8537d71a1238` | Implemented and verified; Step 2 still requires explicit user confirmation |
| 2 | Invoice 2 – Partner Opening Balances | `019f97ec-202f-7563-9a7e-d0be18849b4a` | Waiting for Step 1 confirmation |
| 3 | Invoice 3 – Invoice Workflow | `019f97ec-5302-7c20-9d9e-55719e500014` | Waiting for Step 2 completion |
| 4 | Invoice 4 – Stock Adjustments | `019f97ec-7c06-7342-a0ce-2a8ed6273769` | Waiting for Step 3 completion |
| 5 | Invoice 5 – Receipt and Payment Vouchers | `019f97ec-ada6-72b3-843b-4350de7c7c0e` | Waiting for Step 4 completion |
| 6 | Invoice 6 – Balance Reports | `019f97ec-d7ae-79f0-a695-e3c5e1d1e962` | Deferred pending Step 5 and source-of-truth approval |
| 7 | Invoice 7 – Driver Trips | `019f97ed-0535-7e53-94e9-43d9bfd00a7d` | Deferred pending Step 6 and separate approval |

Only the user can confirm that a prerequisite task is complete and authorize
the next task to start.

### Resuming the sidebar tasks on another PC

These tasks share one checkout and must remain sequential. Do not run two
document implementation tasks against the same working tree at the same time.

When a step is fully implemented, verified, reviewed, and available on the
other PC, open the next sidebar task and send the matching confirmation:

```text
To Invoice 2:
“Invoice 1 – Stock Opening Balances” is complete. Start Step 2 using the latest
prepared instructions.

To Invoice 3:
“Invoice 2 – Partner Opening Balances” is complete. Start Step 3 using the
latest prepared instructions.

To Invoice 4:
“Invoice 3 – Invoice Workflow” is complete. Start Step 4 using the latest
prepared instructions.

To Invoice 5:
“Invoice 4 – Stock Adjustments” is complete. Start Step 5 using the latest
prepared instructions.
```

For Steps 6 and 7, prerequisite completion is not enough. The task must stop
and obtain the separate approval described in its section.

At the beginning of each task:

1. Read the three source documents completely.
2. Read this file completely.
3. Inspect `git status` and preserve unrelated user changes.
4. Inspect all entity, service, configuration, migration, seed, Swagger,
   frontend, and test references for the feature.
5. Present the Domain/EF design and affected files.
6. Wait for user approval before generating a migration.

## Project architecture and implementation placement

Use the existing Clean Architecture projects without adding CQRS, MediatR, or
repository classes:

```text
src/MiniErp.Domain
  Entities, enums, and calculation methods

src/MiniErp.Application
  Request/response records, validators, mapping registrations, service
  interfaces, Result types, and pagination contracts

src/MiniErp.Infrastructure
  ApplicationDbContext, EF configurations, direct service implementations,
  audit interceptor, migrations, and seed data

src/MiniErp.Api
  Versioned controllers, Result-to-ProblemDetails conversion, authorization,
  and separate feature Swagger operation documentation

tests/MiniErp.Tests
  Validator, service, transaction, tenant, persistence, and concurrency tests
```

Recommended files for every document feature:

```text
src/MiniErp.Application/Features/<Feature>/
  I<Feature>Service.cs
  <Feature>Requests.cs
  <Feature>Responses.cs
  <Feature>RequestValidator.cs
  <Feature>MappingRegister.cs

src/MiniErp.Infrastructure/Persistence/Configurations/
  <Header>Configuration.cs
  <Line>Configuration.cs

src/MiniErp.Infrastructure/Services/<Feature>/
  <Feature>Service.cs

src/MiniErp.Api/Controllers/
  <Feature>Controller.cs

src/MiniErp.Api/Swagger/
  <Feature>SwaggerDocumentation.cs

tests/MiniErp.Tests/<Feature>/
  <Feature>RequestValidatorTests.cs
  <Feature>ServiceTests.cs

docs/
  <FEATURE>_FRONTEND_CONTRACT.md
```

Services inject `ApplicationDbContext`, `IPaginationService` when the list is
paginated, `ICurrentCompanyContext`, and `TimeProvider` when `Touch` needs a
testable UTC timestamp. Services implement `IScopedService`; dependency
registration is discovered automatically.

## Standard API and authorization contract

Controllers inherit `ApiControllerBase`, which supplies:

```text
/api/v{version}/[controller]
```

Default authorization:

| Operation | Authorization |
|---|---|
| List, select, get by ID | Authenticated user |
| Create, update, soft delete | `Admin` role |

Expected status codes:

| Result | HTTP status |
|---|---:|
| Successful list/get/update | 200 |
| Successful create | 201 |
| Successful soft delete | 204 |
| Validation failure | 400 |
| Missing authentication | 401 |
| Wrong role | 403 |
| Missing/cross-company entity | 404 |
| Duplicate, inactive relationship, invalid state, or concurrency | 409 |

Every endpoint must declare matching `ProducesResponseType` attributes and a
feature-specific Swagger operation description. Expected failures use
`ProblemDetails` through the existing `Result` conversion.

Standard paginated shape:

```json
{
  "items": [],
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 0,
  "totalPages": 0
}
```

`pageNumber` must be greater than zero. `pageSize` must be between 1 and 100.
Sort by business date/number as appropriate and then by `Id` to guarantee
deterministic ordering.

## Rules shared by all document tasks

Before implementation, read these files completely:

- `FEATURE_DEVELOPMENT_GUIDE.md`
- `INVOICE_FEATURE_SPECIFICATION.md`
- `INVOICE_IMPLEMENTATION_PLAN.md`

Apply these approved simple-application rules:

1. Implement editable aggregate CRUD only.
2. Do not add `DocumentStatus`, draft/posted/cancelled states, post or cancel
   endpoints, reversal workflows, or posting/cancellation audit fields.
3. Do not generate item, partner, container, driver-trip, or reversal
   movements.
4. Save the complete aggregate in an explicit atomic transaction for create,
   update, and soft delete.
5. Let `AuditableEntityInterceptor` populate audit fields. Feature services
   must not duplicate audit handling.
6. Get `CompanyId` only from `ICurrentCompanyContext` and apply explicit tenant
   filters to reads, writes, duplicate checks, and foreign-key validation.
7. A document product `StoreId` must reference an active store in the selected
   company with `IsContainerStore = false`.
8. Paginated master-detail responses must include complete, deterministically
   ordered child collections. A count may remain, but never replaces details.
9. Review the entity and EF Core design with the user before creating a
   migration.
10. Provide Swagger documentation, Arabic validation and business messages,
    idempotent multi-company seed data where applicable, automated tests,
    verification, and an exact frontend contract.

## Tenant, foreign-key, and store rules

Every company-owned entity has its own `CompanyId`, but request DTOs never
accept a client-controlled company ID.

Required query pattern:

```csharp
var entity = await dbContext.Entities
    .FirstOrDefaultAsync(
        item => item.Id == id && item.CompanyId == companyId,
        cancellationToken);
```

Apply `companyId` to:

- Lists and get-by-ID queries.
- Update and soft-delete lookups.
- Duplicate checks.
- Every company-owned foreign-key lookup.
- Seed idempotency checks.
- Dependency checks, even when `IgnoreQueryFilters()` is used.

An ID from another company must behave as unavailable and return `404`; do not
reveal that it exists.

Document `StoreId` validation must confirm:

```text
Store.CompanyId == current company
Store.IsActive == true
Store.IsContainerStore == false
Store.IsDeleted == false through the normal query filter
```

Invoice `ContainerStoreId`, when required, must instead reference the selected
partner's active container store in the same company. Every selected
`ContainerId` must have an active `StoreContainer` assignment to that store.

Load bounded foreign-key sets in one query. Never execute one item/container
lookup per child row.

## Validation, mapping, audit, and calculated values

- FluentValidation validates request shape and boundaries.
- Add Arabic display names for every new request property to
  `ArabicValidationConfiguration`.
- Mapster mapping trims strings and ignores identity, calculated values,
  creation audit properties, `RowVersion`, and `LastModifiedAt` on update.
- Server code calculates quantities and totals. Never trust client-calculated
  values.
- `AuditableEntityInterceptor` owns `Created*`, `Updated*`, `Deleted*`, and
  `IsDeleted` behavior.
- Do not manually set audit fields in a feature service.
- Child records inherit `AuditableEntity` and are soft-deleted through the
  interceptor when removed.
- Configure decimal precision explicitly and validate that multiplication
  results fit the configured precision before saving.

Stable enums currently defined in the Domain:

```text
CurrencyCode:
  EGP = 1, USD = 2, EUR = 3, GBP = 4,
  SAR = 5, AED = 6, KWD = 7

PartnerBalanceType:
  Receivable = 1, Payable = 2

InvoiceType:
  Sales = 1, Purchase = 2,
  SalesReturn = 3, PurchaseReturn = 4

StockAdjustmentDirection:
  Increase = 1, Decrease = 2

VoucherType:
  Receipt = 1, Payment = 2

PaymentStatus:
  Unpaid = 1, Paid = 2

PaymentTerm:
  Cash = 1, Credit = 2
```

Enums are sent as JSON names, not numeric values.

## Shared RowVersion and concurrency rules

Use optimistic concurrency at the aggregate header:

1. Add `RowVersion` only to the document header and configure it with
   `.IsRowVersion()`.
2. Do not add `RowVersion` to detail, line, container-line, or allocation rows.
3. The update request sends the `RowVersion` originally returned when the user
   loaded the document.
4. Assign that client token as EF Core's tracked `OriginalValue`. Never replace
   it with a freshly loaded database token before saving.
5. For aggregates with children, add `LastModifiedAt` and a simple
   `Touch(DateTime utcNow)` method to the header.
6. Every aggregate update, including a child-only add, change, or removal,
   calls `Touch` and explicitly marks `LastModifiedAt` modified before the
   single `SaveChangesAsync`.
7. Catch `DbUpdateConcurrencyException`, roll back the transaction, and return
   a feature-specific conflict with a clear Arabic reload-and-retry message.
8. Every successful aggregate update must return a new `RowVersion`.
9. Test stale header-only and child-only updates with separate contexts.
10. Do not introduce a generic concurrency framework or independent child-row
    update endpoints.

### Required update request

The API serializes `byte[]` as a base64 string:

```json
{
  "rowVersion": "AAAAAAAAB9E="
}
```

Require a non-empty token. SQL Server `rowversion` is eight bytes, so validation
may require exactly eight decoded bytes.

### Required header design for master-detail documents

```csharp
public DateTime LastModifiedAt { get; private set; }

public byte[] RowVersion { get; private set; } = [];

public void Touch(DateTime utcNow)
{
    LastModifiedAt = utcNow;
}
```

EF configuration:

```csharp
builder.Property(document => document.LastModifiedAt)
    .HasColumnType("datetime2(7)")
    .IsRequired();

builder.Property(document => document.RowVersion)
    .IsRowVersion()
    .IsRequired();
```

Do not configure `RowVersion` on child entities.

### Required atomic update pattern

```csharp
await using var transaction =
    await dbContext.Database.BeginTransactionAsync(cancellationToken);

var document = await dbContext.Documents
    .Include(item => item.Lines)
    .FirstOrDefaultAsync(
        item => item.Id == id && item.CompanyId == companyId,
        cancellationToken);

if (document is null)
{
    return Result<DocumentResponse>.Failure(NotFound(id));
}

var entry = dbContext.Entry(document);

// Use the token originally loaded by the client.
entry.Property(item => item.RowVersion).OriginalValue = request.RowVersion;

ApplyHeaderChanges(document, request);
ReplaceLines(document, request.Lines);
document.Recalculate();
document.Touch(timeProvider.GetUtcNow().UtcDateTime);

// Guarantees a header UPDATE for line-only changes.
entry.Property(item => item.LastModifiedAt).IsModified = true;

try
{
    await dbContext.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
}
catch (DbUpdateConcurrencyException)
{
    await transaction.RollbackAsync(cancellationToken);
    dbContext.ChangeTracker.Clear();

    return Result<DocumentResponse>.Failure(
        Error.Conflict(
            "Documents.Concurrency",
            "تم تعديل المستند بواسطة مستخدم آخر. أعد تحميل المستند ثم حاول مرة أخرى."));
}
```

Do not fetch a new token and assign it to `OriginalValue`. Do not automatically
merge and retry. The user must reload the aggregate.

### Required concurrency tests

Use two separate contexts/services:

1. Both load the same aggregate and retain the same original token.
2. Context A changes only a child and saves successfully.
3. Verify the returned token differs from the original.
4. Context B attempts a header-only update with the stale token and receives
   the feature concurrency conflict.
5. Repeat with Context B attempting a child-only add/update/removal.
6. Verify failed stale writes did not partially change header or child rows.

## Explicit transaction and aggregate replacement rules

- Start the transaction before write-related validation whose consistency
  matters.
- Validate all foreign keys in bounded set queries.
- Track the header and complete child collections.
- Update matching children, add new children, and remove missing children in
  the same context.
- Recalculate all server-owned values.
- Call `SaveChangesAsync` once for the aggregate.
- Commit only after save and response projection succeed.
- Returning before commit must dispose/roll back the transaction.
- A forced database failure on a later child insert must leave no header or
  earlier child rows.
- Never expose child-only create/update/delete endpoints.

## EF Core and migration workflow

Before creating a migration:

1. Present the entity fields, nullability, indexes, composite tenant foreign
   keys, delete behavior, decimal precision, and `RowVersion` configuration to
   the user.
2. Wait for explicit user approval.
3. Check existing data for duplicates and invalid foreign keys.

Create a migration only after approval:

```powershell
dotnet ef migrations add <MigrationName> `
  --project src/MiniErp.Infrastructure `
  --startup-project src/MiniErp.Api
```

Then inspect `Up`, `Down`, the Designer file, and
`ApplicationDbContextModelSnapshot`. Reject unrelated changes.

Verification:

```powershell
dotnet ef migrations has-pending-model-changes `
  --project src/MiniErp.Infrastructure `
  --startup-project src/MiniErp.Api

dotnet build MiniErp.slnx --configuration Release --no-restore
dotnet test MiniErp.slnx --configuration Release --no-build --no-restore
dotnet format MiniErp.slnx --verify-no-changes --no-restore
git diff --check
```

Current backend migration baseline includes:

```text
20260719062827_InitialIdentity
20260719064120_IntialCreate
20260719074228_AddTablesItemAndItemUnit
20260719163928_AddRefreshTokens
20260720081528_addCompany
20260720094457_addCompanyTenant
20260720095927_addusermanagement
20260720100939_AddRefreshTokenCompanyContext
20260721095030_AddDrivers
20260721100306_MakeDriverLicenseNumberOptional
20260721101546_AddBusinessPartners
20260721103851_MakeDriverLicenseNumberRequired
20260722074035_MakeDriverAndBusinessPartnerNamesUnique
20260722111450_AddContainerStoreClassification
20260722193932_EnforceUniqueActiveContainerStore
20260722202332_AddReferenceAndContainerData
20260725064550_improveContainers
20260725084727_AddStockOpening
20260725100049_UpdateStockOpeningLineAmounts
```

Do not recreate, rename, reorder, or squash existing migrations while
implementing later tasks.

## Step 0 — Reference Data

### Scope

- Preserve the Store product/container classification:
  - Product store: `IsContainerStore = false`,
    `BusinessPartnerId = null`.
  - Partner container store: `IsContainerStore = true`,
    `BusinessPartnerId` is required.
- Maintain at most one active container store per business partner and company.
- Implement global Country reference data.
- Implement company-owned Container CRUD and selection.
- Implement StoreContainer assignments.
- Reuse existing Item, Store, BusinessPartner, and Driver selectors.
- Keep external driver names out of the `Driver` table.

Current reference entity fields:

```text
Country (global):
  Id
  Code
  Name
  ArabicName
  IsActive
  Audit fields

Container (company-owned):
  Id
  CompanyId / Company
  Code
  Name
  Description?
  IsActive
  StoreContainers
  Audit fields

StoreContainer (company-owned assignment):
  Id
  CompanyId / Company
  StoreId / Store
  ContainerId / Container
  IsActive
  Audit fields

Store classification fields:
  IsContainerStore
  BusinessPartnerId? / BusinessPartner?
```

Persistence rules:

- Country code is globally unique according to the approved reference design.
- Container code uniqueness is company-scoped for active/non-deleted records.
- Active StoreContainer uniqueness is
  `(CompanyId, StoreId, ContainerId)`.
- A StoreContainer assignment must use a same-company container store and
  container.
- A business partner may have at most one active dedicated container store in
  a company.

### Partner/container API contract

- `GET /api/v1/BusinessPartners/{id}` returns the business-partner detail. The
  current implementation also enriches it with its active container store and
  the active container workspace used by setup/edit screens.
- `GET /api/v1/BusinessPartners/{id}/container-store` is the focused endpoint
  for consumers that need only the partner's active container store and only
  the active containers actually assigned to that store. It does not return
  the full partner detail.
- Paginated BusinessPartner list items return only active containers actually
  assigned to the partner's active container store.
- Store-container setup screens may use the dedicated workspace endpoint to
  show assigned and unassigned active container choices.

Current reference-data routes:

```text
GET    /api/v1/Countries
GET    /api/v1/Countries/select
GET    /api/v1/Countries/{id}
POST   /api/v1/Countries
PUT    /api/v1/Countries/{id}
DELETE /api/v1/Countries/{id}

GET    /api/v1/Containers
GET    /api/v1/Containers/select
GET    /api/v1/Containers/{id}
POST   /api/v1/Containers
PUT    /api/v1/Containers/{id}
DELETE /api/v1/Containers/{id}

GET /api/v1/Stores
GET /api/v1/Stores/select
GET /api/v1/Stores/container-select
GET /api/v1/Stores/{id}

GET /api/v1/StoreContainers
GET /api/v1/StoreContainers/select
GET /api/v1/StoreContainers/workspace
GET /api/v1/StoreContainers/{id}
PUT /api/v1/StoreContainers/upsert

GET /api/v1/BusinessPartners
GET /api/v1/BusinessPartners/select
GET /api/v1/BusinessPartners/{id}
GET /api/v1/BusinessPartners/{id}/container-store
```

### Exclusions

- No invoice documents.
- No movements.
- No external-driver records.

## Step 1 — Stock Opening Balances

### Aggregate

- `StockOpeningBalance`
- `StockOpeningBalanceLine`

### Required behavior

- Simple aggregate list, get, create, update, and soft delete.
- Explicit atomic transactions.
- Active product stores only; reject container stores.
- Header-only `RowVersion` concurrency.
- Paginated list items include complete ordered line details.
- No status, posting, cancellation, reversal, or item movements.

Current routes:

```text
GET    /api/v1/StockOpeningBalances
GET    /api/v1/StockOpeningBalances/{id}
POST   /api/v1/StockOpeningBalances
PUT    /api/v1/StockOpeningBalances/{id}
DELETE /api/v1/StockOpeningBalances/{id}
```

Header fields:

```text
Id
CompanyId
StoreId / Store
DocumentNumber
DocumentDate
Notes
RowVersion
Lines
Audit fields
```

### Line contract

Each line contains:

- `ItemId`
- Nullable server-derived `ItemUnitId` and `ItemUnit`
- `Count`
- `Weight`
- Calculated `Quantity = Count * Weight`
- `Price`
- Calculated `Total = Quantity * Price`
- Optional `Notes`

The request sends `ItemId`, `Count`, `Weight`, `Price`, and `Notes`. It does not
send `ItemUnitId`, `Quantity`, or `Total`.

### Seed and response requirements

- Seed multiple lines with different `Count`, `Weight`, and `Price` values so
  calculated quantities and totals are clear.
- Both list and detail responses return all line fields.
- Existing migrations:
  - `20260725084727_AddStockOpening`
  - `20260725100049_UpdateStockOpeningLineAmounts`
- Existing frontend contract:
  `docs/STOCK_OPENING_BALANCE_FRONTEND_CONTRACT.md`

## Step 2 — Partner Opening Balances

### Prerequisite

Do not start until the user explicitly confirms Step 1 is complete.

### Aggregate

- `PartnerOpeningBalance`

Current entity scaffold fields:

```text
Id
CompanyId / Company
BusinessPartnerId / BusinessPartner
DocumentNumber
DocumentDate
Currency
BalanceType
Amount
Notes
RowVersion
Audit fields
```

### Required behavior

- Receivable and payable types.
- Simple list, get, create, update, and soft delete.
- Explicit atomic transactions.
- Tenant, partner, currency, and amount validation.
- Header-only `.IsRowVersion()` concurrency using the original client token.
- Catch stale-token conflicts and return a clear Arabic
  `PartnerOpeningBalances.Concurrency` reload-and-retry message.
- Because this document has no child collection, a normal header modification
  advances `RowVersion`; no `LastModifiedAt` is required solely for
  concurrency.
- No status, posting, cancellation, reversal, or partner movements.

Planned routes:

```text
GET    /api/v1/PartnerOpeningBalances
GET    /api/v1/PartnerOpeningBalances/{id}
POST   /api/v1/PartnerOpeningBalances
PUT    /api/v1/PartnerOpeningBalances/{id}
DELETE /api/v1/PartnerOpeningBalances/{id}
```

Create/update validation must include:

- Positive partner ID and active partner in the selected company.
- Required normalized document number with company-scoped uniqueness.
- Required document date.
- Defined `PartnerBalanceType`.
- Defined `CurrencyCode`; confirm whether it is supplied and checked against
  the partner or always derived from the partner during entity/EF review.
- Positive amount with explicit money precision.
- Optional bounded notes.
- Required original row version on update only.

### Response contract

Every paginated item returns the complete detail fields:

- Business-partner ID and name
- Document fields
- Receivable/payable type
- Currency
- Amount
- Notes
- Row version

Do not create a reduced header-only list response.

## Step 3 — Invoice Workflow

### Prerequisite

Do not start until Step 2 is complete.

### Aggregate

- `Invoice`
- `InvoiceLine`
- `InvoiceContainerLine`

Current Invoice header scaffold:

```text
Id
CompanyId / Company
InvoiceNumber
ExportInvoiceCode?
InvoiceType
PaymentTerm (Cash = 1, Credit = 2; defaults to Cash)
InvoiceDate
DueDate?
BusinessPartnerId / BusinessPartner
StoreId / product Store
ContainerStoreId? / ContainerStore?
CountryId? / Country?
Currency
DriverId? / Driver?
UsesExternalDriver
ExternalDriverName?
VehicleNumber?
Total (server-calculated)
Notes?
LastModifiedAt
RowVersion
Lines
ContainerLines
Audit fields
```

Current InvoiceLine scaffold:

```text
Id
CompanyId / Company
InvoiceId / Invoice
ItemId / Item
ItemUnitId / ItemUnit (server-derived; not sent by client)
Count
Weight
Quantity (server-calculated)
Price
Total (server-calculated)
Notes?
Audit fields
```

Current InvoiceContainerLine scaffold:

```text
Id
CompanyId / Company
InvoiceId / Invoice
ContainerId / Container
OutgoingUnits
IncomingUnits
Audit fields
```

### Required behavior

- Paginated list, details, create, update, and soft delete.
- Sales, purchase, sales-return, and purchase-return types.
- Required Cash/Credit payment term. Cash is immediately paid; Credit remains
  outstanding against the partner account in the current no-posting workflow.
- Server-generated invoice number.
- Server-derived currency and item units.
- Request line fields: `ItemId`, `Count`, `Weight`, `Price`, and `Notes`.
- Request header fields include `DiscountAmount` and `PaidAmount`.
- Calculate line quantity, subtotal, net invoice total, and remaining amount on
  the server:

  ```text
  Subtotal = SUM(Line Total)
  Total = Subtotal - DiscountAmount
  RemainingAmount = Total - PaidAmount
  ```

- `DiscountAmount` and `PaidAmount` are non-negative monetary values. Discount
  cannot exceed the subtotal and paid amount cannot exceed the net total.
  Cash invoices must be fully paid; Credit invoices may be unpaid, partially
  paid, or fully paid.
- Treat sales and purchase returns as independent invoice documents.
- Use an active product `StoreId`.
- Keep partner `ContainerStoreId` on the invoice header when container lines
  are used.
- Validate that every container line references an active container assigned
  to that partner's active container store.
- External driver name remains only on Invoice; `Driver` contains internal
  company drivers.
- Invoice CRUD synchronizes current `ItemMovement`,
  `ContainerMovement`, `BusinessPartnerMovement`, and internal `DriverTrip`
  rows in the same transaction. Updates replace active side-effect rows and
  deletes soft-delete them with the invoice.
- A `BusinessPartnerMovement` is created only when `RemainingAmount` is
  positive. Cash invoices are immediately paid and do not create an
  outstanding movement; a fully paid Credit invoice also creates none.
- There is no status, posting, cancellation, reversal, voucher, or allocation
  workflow in this current feature. Responses expose server-derived
  `Subtotal`, `DiscountAmount`, `Total`, `PaymentStatus`, `PaidAmount`, and
  `RemainingAmount`.

Planned routes:

```text
GET    /api/v1/Invoices
GET    /api/v1/Invoices/{id}
POST   /api/v1/Invoices
PUT    /api/v1/Invoices/{id}
DELETE /api/v1/Invoices/{id}
```

Create request does not send `CompanyId`, `InvoiceNumber`, `Currency`,
`PaymentStatus`, `Subtotal`, `Total`, or `RemainingAmount`,
`ItemUnitId`, `Quantity`, line totals, audit fields,
`LastModifiedAt`, or `RowVersion`.

Update request uses the same editable fields and additionally sends the
original `RowVersion`.

Line calculations:

```text
Quantity = Count * Weight
Line Total = Quantity * Price
Subtotal = SUM(Line Total)
Total = Subtotal - DiscountAmount
RemainingAmount = Total - PaidAmount
```

Container-line validation:

- Container lines are allowed only for the approved sales-related document
  types.
- `ContainerStoreId` is required when container lines exist.
- Each container must be active, same-company, and assigned to that store.
- `OutgoingUnits` and `IncomingUnits` are non-negative and cannot both be zero.
- Both may be positive in the same line.

Driver validation:

| Case | UsesExternalDriver | DriverId | ExternalDriverName |
|---|---:|---:|---|
| No driver | false | null | null |
| Internal driver | false | required | null |
| External driver | true | null | required |

Return validation:

- Returns do not reference or allocate against an earlier invoice.
- Validate the normal active partner, store, item, unit, container, driver,
  quantity, price, and payment-term rules.
- Purchase returns require sufficient stock.
- Backdated additions or updates must not make any later historical stock
  balance negative.
- Repeated item IDs are rejected.

### Concurrency

- Keep `RowVersion` only on `Invoice`.
- Preserve `Invoice.LastModifiedAt` and `Invoice.Touch(DateTime utcNow)`.
- Do not add tokens to `InvoiceLine` or `InvoiceContainerLine`.
- Header, product lines, and container lines are one atomic aggregate.
- Any header, product-line, or container-line update touches the header.
- Return `Invoices.Concurrency` for stale updates.
- Test stale header-only, product-line-only, and container-line-only changes.

### Response contract

Every paginated Invoice item includes complete ordered product-line and
container-line collections. Count fields may remain but do not replace them.

## Step 4 — Stock Adjustments

### Prerequisite

Do not start until Step 3 is complete.

### Aggregate

- `StockAdjustment`
- `StockAdjustmentLine`

Current StockAdjustment header scaffold:

```text
Id
CompanyId / Company
StoreId / Store
DocumentNumber
DocumentDate
Direction
Reason?
RowVersion
Lines
Audit fields
```

Current StockAdjustmentLine scaffold:

```text
Id
CompanyId / Company
StockAdjustmentId / StockAdjustment
ItemId / Item
ItemUnitId / ItemUnit
Quantity
Reason?
Audit fields
```

### Required behavior

- Simple increase/decrease aggregate CRUD.
- Active product-store validation and tenant isolation.
- Explicit atomic create, update, and soft-delete transactions.
- Add `LastModifiedAt`/`Touch` to the header.
- Keep `.IsRowVersion()` only on the StockAdjustment header.
- Any line addition, change, or removal touches the header.
- Return `StockAdjustments.Concurrency` for stale updates.
- Include complete ordered lines in every paginated item.
- No status, posting, cancellation, reversal, stock movements, or
  posting-time stock validation.

Planned routes:

```text
GET    /api/v1/StockAdjustments
GET    /api/v1/StockAdjustments/{id}
POST   /api/v1/StockAdjustments
PUT    /api/v1/StockAdjustments/{id}
DELETE /api/v1/StockAdjustments/{id}
```

Before migration approval, confirm whether adjustment lines keep their current
single `Quantity` design or adopt the Invoice-style
`Count`/`Weight`/calculated `Quantity`/`Price`/`Total` design. Do not invent
that change during implementation without user approval.

## Step 5 — Receipt and Payment Vouchers

### Prerequisite

Do not start until Step 4 is complete.

### Aggregate

- `BusinessPartnerVoucher`
- `BusinessPartnerVoucherAllocation`

Current voucher header scaffold:

```text
Id
CompanyId / Company
BusinessPartnerId / BusinessPartner
VoucherNumber
VoucherType
VoucherDate
Currency
Amount
Notes?
RowVersion
Allocations
Movements (future placeholder only; do not configure or write)
Audit fields
```

Current allocation scaffold:

```text
Id
CompanyId / Company
BusinessPartnerVoucherId / BusinessPartnerVoucher
InvoiceId / Invoice
Amount
Audit fields
```

### Required behavior

- Simple receipt/payment voucher aggregate CRUD.
- Query outstanding invoices where still applicable.
- Validate company, partner, currency, allocation limits, and allowed
  unallocated amount.
- Explicit atomic create, update, and soft-delete transactions.
- Add `LastModifiedAt`/`Touch` to the voucher header.
- Keep `.IsRowVersion()` only on the voucher header.
- Any allocation addition, change, or removal touches the header.
- Return `BusinessPartnerVouchers.Concurrency` for stale updates.
- No status, posting, cancellation, reversal, or partner movements.

Planned routes:

```text
GET    /api/v1/BusinessPartnerVouchers
GET    /api/v1/BusinessPartnerVouchers/{id}
GET    /api/v1/BusinessPartnerVouchers/outstanding-invoices
POST   /api/v1/BusinessPartnerVouchers
PUT    /api/v1/BusinessPartnerVouchers/{id}
DELETE /api/v1/BusinessPartnerVouchers/{id}
```

Voucher validation:

- `Receipt` is used for customer receipts.
- `Payment` is used for supplier payments.
- Partner and every allocated invoice belong to the selected company.
- Allocated invoices match the voucher partner and currency.
- Each allocation amount is positive.
- Allocations cannot exceed the invoice's approved outstanding amount.
- Total allocations cannot exceed voucher amount.
- `Amount - SUM(Allocations)` may remain unallocated.
- Duplicate invoice allocation rows are rejected.
- No allocation row is independently editable.
- Outstanding-invoice calculations must be implemented only from approved
  existing CRUD data; do not silently reintroduce partner movements.

### Response contract

Every paginated voucher item contains its complete deterministically ordered
allocation collection. An allocation count may remain but does not replace the
collection.

## Step 6 — Balance Reports

### Prerequisite and approval gate

- Wait for Step 5 completion.
- Then require separate user approval for the report source of truth.

### Current decision

Movement-based stock, partner, and container reports are deferred because the
approved simple CRUD workflow does not generate movements.

Do not:

- Infer or introduce movement writes.
- Add mutable current-balance columns to master data.
- Implement reports against an unapproved source of truth.

After approval, implement only the approved read-only reports using
tenant-safe server-side projection, efficient indexed queries, deterministic
pagination where applicable, Swagger, verification, and frontend contracts.

## Step 7 — Driver Trips

### Prerequisite and approval gate

- Wait for Step 6.
- Then require separate user approval.

### Current decision

Invoice CRUD creates one `DriverTrip` when an internal driver is supplied.
The trip is synchronized on update and soft-deleted with the invoice. External
driver data remains on the invoice only.

## Completion gate for Steps 2–5

Before declaring a document step complete:

- Confirm no status/post/cancel/reversal/voucher/allocation logic was added.
- Confirm invoice side-effect rows are synchronized atomically.
- Confirm all company-owned reads and writes are tenant-filtered.
- Confirm aggregate writes are atomic.
- Confirm header-only concurrency and stale child-only conflicts.
- Confirm audit remains interceptor-owned.
- Confirm list responses contain complete child details.
- Review the entity/EF design before migration creation.
- Review the migration for unrelated changes.
- Verify seed idempotency for fresh and existing databases.
- Run tests, Release build, formatting, and pending-model checks.
- Deliver the exact frontend request/response contract and examples.

## Mandatory automated verification matrix

Every applicable document feature must cover:

### Validation

- Missing and invalid IDs.
- Empty and whitespace-only required strings.
- Exact maximum length and one character beyond it.
- Undefined enum names/numbers.
- Zero, negative, maximum, and overflow decimal values.
- Empty child collection where at least one child is required.
- Duplicate child item/container/invoice IDs.
- Missing and stale row versions.

### Tenant and relationships

- Company A cannot list or get Company B documents.
- Company A cannot update or delete a Company B document.
- Company A cannot reference Company B partner, store, item, unit, container,
  or driver.
- Missing, inactive, and soft-deleted references return the documented error.
- Product documents reject container stores.
- Container references are assigned to the selected partner container store.

### Aggregate and transaction behavior

- Valid create writes header and all children.
- Valid update adds, changes, and removes children.
- Line-only/allocation-only update advances the header row version.
- Header-only update advances the row version.
- Stale token rejects both header-only and child-only changes.
- Forced failure on a later child write rolls back the complete aggregate.
- Soft delete is atomic for header and current children.
- Audit fields are populated by the interceptor.

### Read contract

- Empty and populated lists.
- First, later, and beyond-last pages.
- Maximum and invalid page sizes.
- Deterministic ordering.
- Every paginated item contains its complete ordered children.
- Get-by-ID returns all frontend-required names and calculated values.
- Responses never expose another company's data.

### Database and seed

- Unique document number/code constraints are company-scoped.
- Composite foreign keys prevent cross-company relationships.
- Decimal precision and check constraints match validation.
- Query filters hide soft-deleted rows.
- Seed succeeds on a fresh database.
- Re-running seed does not duplicate or overwrite user data.
- Migration succeeds against empty and representative existing databases.

### API and frontend

- Anonymous request receives `401`.
- Authenticated non-Admin write receives `403`.
- Validation, not-found, conflict, success, create, and delete statuses match
  Swagger.
- Frontend sends the original base64 row version unchanged.
- Frontend reloads the aggregate after a concurrency conflict.
- Frontend production build passes.

## Frontend handoff required after each step

Create a feature contract Markdown file that includes:

- Every route and HTTP method.
- Authorization requirements.
- Query parameters and pagination defaults.
- Exact create and update JSON requests.
- Exact list and detail JSON responses.
- Enum names and values.
- Required/optional fields.
- Server-derived and calculated fields that the client must not send.
- Base64 `RowVersion` behavior.
- `400`, `404`, and `409` ProblemDetails examples.
- Arabic validation/business messages where the UI displays them.
- Empty-state behavior.
- Select endpoints and option shapes.
- A note that `CompanyId` comes from the access token.

For master-detail documents, the client should edit the aggregate in one form
and submit one create/update request. It must not call child-row mutation
endpoints because none should exist.

## Decisions that still require explicit review

Do not silently decide these while implementing:

1. Partner Opening Balance currency: client-selected-and-validated versus
   always derived from the selected partner.
2. Stock Adjustment line model: retain the current single `Quantity` scaffold
   versus adopt Invoice-style count/weight/value fields.
3. Invoice number generation format and concurrency-safe sequence. **Resolved
   for Step 3:** `INV-{CompanyId}-{UTC yyyyMMddHHmmssfff}-{8-char GUID
   suffix}`, with a company-scoped unique filtered index.
4. Exact decimal precision and rounding for Invoice and voucher money.
   **Resolved for Step 3 invoices:** quantity `decimal(18,6)`, money
   `decimal(18,2)`, line totals rounded away from zero to two decimals.
5. Whether delete requests also require a row version. Update requests
   definitely require it.
6. Balance-report source of truth after movements were removed.
7. DriverTrip source and lifecycle after invoice posting was removed.

Record each approved answer in this file, the shared specification, Swagger,
tests, and frontend contract before implementation is considered complete.

## Final portability checklist

Before moving to another PC:

- Commit or safely copy the backend changes on branch `Invoices`.
- Push the branch to the backend remote.
- Commit and push required frontend changes separately.
- Ensure this file and all feature contract documents are tracked.
- Do not move local secrets or user-specific publish settings into Git.
- Confirm migrations in Git match the database applied on the destination.
- Record the last completed sidebar step.
- Keep later sidebar tasks waiting until their prerequisite confirmation.
- On the destination, restore dependencies and run the full Release build,
  tests, format check, pending-model check, and frontend build before starting
  the next step.
