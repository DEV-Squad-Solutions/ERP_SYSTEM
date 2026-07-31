# MiniErp Cross-Project Graphify Impact Analysis

Updated after implementing the CBE/Frankfurter exchange-rate import flow across the backend and frontend.

## Commands used

```powershell
graphify check-update F:\MiniErp
graphify extract F:\client\client --code-only --out F:\client\client
graphify cluster-only F:\client\client
graphify merge-graphs F:\MiniErp\graphify-out\graph.json F:\client\client\graphify-out\graph.json --out F:\MiniErp\graphify-out\merged-graph.json
graphify export html
```

`graphify export html` was also used to produce the merged visualization snapshot at `graphify-out/merged-graph.html`.

## Graph summary

- Backend (`MiniErp`): **4,036 nodes**, **11,134 edges**.
- Frontend (`client`): **307 nodes**, **505 edges**.
- Merged graph: **4,343 nodes**, **11,639 edges**.
- Explicit cross-repository edges in Graphify AST output: **0**.
- The endpoint bridge below is deterministic analysis of frontend path literals (`/Invoices`, `/Auth/login`, etc.) against ASP.NET controller names/routes; Graphify itself does not claim those as AST edges.

## Frontend architecture

### Pages and shells
- `src/App.tsx`
- `src/components/CashVoucherPage.tsx`
- `src/components/CustomerSetupPage.tsx`
- `src/components/DriverTripCostPage.tsx`
- `src/components/EntityPage.tsx`
- `src/components/ErpShell.tsx`
- `src/components/FinancialStatementPage.tsx`
- `src/components/InventoryCostReportPage.tsx`
- `src/components/InventoryCountPage.tsx`
- `src/components/InvoicePage.tsx`
- `src/components/StockAdjustmentPage.tsx`
- `src/components/StockOpeningBalancePage.tsx`
- `src/components/StoreContainerPage.tsx`

### Reusable components and API primitives
- `src/api.ts`
- `src/components/ContainerPicker.tsx`
- `src/components/InlineBusinessPartnerModal.tsx`
- `src/components/InlineContainerModal.tsx`
- `src/main.tsx`
- `src/vite-env.d.ts`

- `src/api.ts` centralizes `apiRequest`, bearer-token headers, `ProblemDetails`/`ApiError`, pagination, JWT decoding, role extraction, and company extraction.
- `src/components/EntityPage.tsx` provides generic CRUD/pagination behavior from an endpoint string.
- `src/components/ErpShell.tsx` registers generic entity pages and endpoint configurations.

## Endpoint and feature contract bridge

| Resource | Frontend callers | Backend endpoint/controller | Application service |
|---|---|---|---|
| `Auth` | `src/App.tsx` | `src/MiniErp.Api/Controllers/AuthController.cs` (`Post login`; `Post select-company`; `Post refresh`; `Post logout`) | `IAuthenticationService` |
| `BusinessPartners` | `src/components/CashVoucherPage.tsx`, `src/components/CustomerSetupPage.tsx`, `src/components/ErpShell.tsx`, `src/components/FinancialStatementPage.tsx`, `src/components/InlineBusinessPartnerModal.tsx`, `src/components/InvoicePage.tsx` | `src/MiniErp.Api/Controllers/BusinessPartnersController.cs` (`Get`; Get select`; Get {id:int}`; Get {id:int}/container-store`; Post`; Put {id:int}`; Delete {id:int}`) | `IBusinessPartnerService` |
| `CashMovementTypes` | `src/components/CashVoucherPage.tsx`, `src/components/ErpShell.tsx`, `src/components/InvoicePage.tsx` | `src/MiniErp.Api/Controllers/CashMovementTypesController.cs` (`Get`; Get select`; Get {id:int}`; Post`; Put {id:int}`; Delete {id:int}`) | `ICashMovementTypeService` |
| `CashVouchers` | `src/components/CashVoucherPage.tsx` | `src/MiniErp.Api/Controllers/CashVouchersController.cs` (`Get`; Get {id:int}`; Post`; Put {id:int}`; Delete {id:int}`) | `ICashVoucherService` |
| `Cashboxes` | `src/components/CashVoucherPage.tsx`, `src/components/ErpShell.tsx`, `src/components/FinancialStatementPage.tsx`, `src/components/InvoicePage.tsx` | `src/MiniErp.Api/Controllers/CashboxesController.cs` (`Get`; Get select`; Get {id:int}`; Post`; Put {id:int}`; Delete {id:int}`) | `ICashboxService` |
| `Companies` | `src/components/ErpShell.tsx` | `src/MiniErp.Api/Controllers/CompaniesController.cs` (`Get`; Get select`; Get {id:int}`; Post`; Put {id:int}`; Delete {id:int}`) | `ICompanyService` |
| `Containers` | `src/components/CustomerSetupPage.tsx`, `src/components/ErpShell.tsx`, `src/components/InlineContainerModal.tsx`, `src/components/StoreContainerPage.tsx` | `src/MiniErp.Api/Controllers/ContainersController.cs` (`Get`; Get select`; Get {id:int}`; Post`; Put {id:int}`; Delete {id:int}`) | `IContainerService` |
| `Countries` | `src/components/ErpShell.tsx`, `src/components/InvoicePage.tsx` | `src/MiniErp.Api/Controllers/CountriesController.cs` (`Get`; Get select`; Get {id:int}`; Post`; Put {id:int}`; Delete {id:int}`) | `ICountryService` |
| `DriverTrips` | `src/components/CashVoucherPage.tsx`, `src/components/DriverTripCostPage.tsx` | `src/MiniErp.Api/Controllers/DriverTripsController.cs` (`Get cost-entry`; Put bulk-costs`) | `IDriverTripService` |
| `Drivers` | `src/components/CashVoucherPage.tsx`, `src/components/DriverTripCostPage.tsx`, `src/components/ErpShell.tsx`, `src/components/FinancialStatementPage.tsx`, `src/components/InvoicePage.tsx` | `src/MiniErp.Api/Controllers/DriversController.cs` (`Get`; Get select`; Get {id:int}`; Post`; Put {id:int}`; Delete {id:int}`) | `IDriverService` |
| `ExchangeRates` | `src/components/ErpShell.tsx` | `src/MiniErp.Api/Controllers/ExchangeRatesController.cs` (`Get`; Get {id:int}`; Get resolve`; Post`; Put {id:int}`; Delete {id:int}`) | `IExchangeRateService` |
| `InventoryCostReports` | `src/components/InventoryCostReportPage.tsx` | `src/MiniErp.Api/Controllers/InventoryCostReportsController.cs` (`Get`) | `IInventoryCostReportService` |
| `InventoryCounts` | `src/components/InventoryCountPage.tsx` | `src/MiniErp.Api/Controllers/InventoryCountsController.cs` (`Get`; Get {id:int}`; Post`; Put {id:int}`; Post {id:int}/reconcile`; Delete {id:int}`) | `IInventoryCountService` |
| `Invoices` | `src/components/InvoicePage.tsx` | `src/MiniErp.Api/Controllers/InvoicesController.cs` (`Get`; Get item-balance`; Get {id:int}`; Post`; Put {id:int}`; Delete {id:int}`) | `IInvoiceService` |
| `ItemUnits` | `src/components/ErpShell.tsx` | `src/MiniErp.Api/Controllers/ItemUnitsController.cs` (`Get`; Get select`; Get {id:int}`; Post`; Put {id:int}`; Delete {id:int}`) | `IItemUnitService` |
| `Items` | `src/components/ErpShell.tsx`, `src/components/InventoryCostReportPage.tsx`, `src/components/InvoicePage.tsx`, `src/components/StockAdjustmentPage.tsx`, `src/components/StockOpeningBalancePage.tsx` | `src/MiniErp.Api/Controllers/ItemsController.cs` (`Get`; Get select`; Get {id:int}`; Post`; Put {id:int}`; Delete {id:int}`) | `IItemService` |
| `ItemsCategories` | `src/components/ErpShell.tsx`, `src/components/InvoicePage.tsx` | `src/MiniErp.Api/Controllers/ItemsCategoriesController.cs` (`Get`; Get select`; Get {id:int}`; Post`; Put {id:int}`; Delete {id:int}`) | `IItemsCategoryService` |
| `PartnerOpeningBalances` | `src/components/ErpShell.tsx` | `src/MiniErp.Api/Controllers/PartnerOpeningBalancesController.cs` (`Get`; Get {id:int}`; Post`; Put {id:int}`; Delete {id:int}`) | `IPartnerOpeningBalanceService` |
| `Statements` | `src/components/FinancialStatementPage.tsx` | `src/MiniErp.Api/Controllers/StatementsController.cs` (`Get cashbox`; Get partner`; Get driver`) | `IFinancialStatementService` |
| `StockAdjustments` | `src/components/StockAdjustmentPage.tsx` | `src/MiniErp.Api/Controllers/StockAdjustmentsController.cs` (`Get`; Get {id:int}`; Post`; Put {id:int}`; Delete {id:int}`) | `IStockAdjustmentService` |
| `StockOpeningBalances` | `src/components/StockOpeningBalancePage.tsx` | `src/MiniErp.Api/Controllers/StockOpeningBalancesController.cs` (`Get`; Get {id:int}`; Post`; Put {id:int}`; Delete {id:int}`) | `IStockOpeningBalanceService` |
| `StoreContainers` | `src/components/CustomerSetupPage.tsx`, `src/components/StoreContainerPage.tsx` | `src/MiniErp.Api/Controllers/StoreContainersController.cs` (`Get`; Get select`; Get workspace`; Get {id:int}`; Put upsert`) | `IStoreContainerService` |
| `Stores` | `src/components/CustomerSetupPage.tsx`, `src/components/ErpShell.tsx`, `src/components/InventoryCostReportPage.tsx`, `src/components/InventoryCountPage.tsx`, `src/components/InvoicePage.tsx`, `src/components/StockAdjustmentPage.tsx`, `src/components/StockOpeningBalancePage.tsx`, `src/components/StoreContainerPage.tsx` | `src/MiniErp.Api/Controllers/StoresController.cs` (`Get`; Get select`; Get container-select`; Get {id:int}`; Post`; Put {id:int}`; Delete {id:int}`) | `IStoreService` |
| `Users` | `src/components/ErpShell.tsx` | `src/MiniErp.Api/Controllers/UsersController.cs` (`Get`; Get roles`; Get {id:guid}`; Post`; Put {id:guid}`; Put {id:guid}/companies`; Delete {id:guid}`) | `IUserService` |

Resources exposed by the backend but not found as frontend path literals in the current scan should be treated as backend-only or unverified UI integrations.

## DTO and persistence relationships

| Backend controller | Request/response DTOs visible at the endpoint | Persistence/domain follow-through |
|---|---|---|
| `AuthController` | `LoginRequest`, `LoginResponse`, `RefreshTokenRequest`, `SelectCompanyRequest`, `TokenResponse` | feature-specific domain entity set; `ApplicationDbContext` / EF configurations / migrations |
| `BusinessPartnersController` | `BusinessPartnerContainerStoreResponse`, `BusinessPartnerFilterRequest`, `BusinessPartnerRequest`, `BusinessPartnerResponse`, `PagedResponse`, `PaginationRequest`, `SelectResponse` | `MiniErp.Domain.Entities.BusinessPartners`, `BusinessPartner`, `.ApplyExchangeRate()`, `PartnerOpeningBalance`, `PartnerOpeningBalanceAmountRules`, `.IsValidAmount()`; `ApplicationDbContext` / EF configurations / migrations |
| `CashMovementTypesController` | `CashMovementTypeFilterRequest`, `CashMovementTypeRequest`, `CashMovementTypeResponse`, `CashMovementTypeSelectRequest`, `CashMovementTypeSelectResponse`, `CashMovementTypeUpdateRequest`, `PagedResponse`, `PaginationRequest` | `MiniErp.Domain.Entities.CashManagement`, `CashMovementType`; `ApplicationDbContext` / EF configurations / migrations |
| `CashVouchersController` | `CashVoucherFilterRequest`, `CashVoucherRequest`, `CashVoucherResponse`, `CashVoucherUpdateRequest`, `PagedResponse`, `PaginationRequest` | `CashVoucher`, `.Touch()`, `.ApplyExchangeRate()`; `ApplicationDbContext` / EF configurations / migrations |
| `CashboxesController` | `CashboxFilterRequest`, `CashboxRequest`, `CashboxResponse`, `CashboxSelectResponse`, `CashboxUpdateRequest`, `PagedResponse`, `PaginationRequest` | feature-specific domain entity set; `ApplicationDbContext` / EF configurations / migrations |
| `CompaniesController` | `CompanyFilterRequest`, `CompanyRequest`, `CompanyResponse`, `PagedResponse`, `PaginationRequest`, `SelectResponse` | `MiniErp.Domain.Entities.Companies`, `Company`, `CompanySettings`, `ExchangeRate`, `.Touch()`, `ExchangeRateRules`; `ApplicationDbContext` / EF configurations / migrations |
| `ContainersController` | `ContainerFilterRequest`, `ContainerRequest`, `ContainerResponse`, `PagedResponse`, `PaginationRequest`, `SelectResponse` | `MiniErp.Domain.Entities.Containers`, `Container`, `StoreContainer`, `InvoiceContainerLine`; `ApplicationDbContext` / EF configurations / migrations |
| `CountriesController` | `CountryFilterRequest`, `CountryRequest`, `CountryResponse`, `PagedResponse`, `PaginationRequest`, `SelectResponse` | feature-specific domain entity set; `ApplicationDbContext` / EF configurations / migrations |
| `DriverTripsController` | `DriverTripBulkCostUpdateRequest`, `DriverTripBulkCostUpdateResponse`, `DriverTripCostFilterRequest`, `DriverTripCostResponse`, `PagedResponse`, `PaginationRequest` | `DriverTrip`; `ApplicationDbContext` / EF configurations / migrations |
| `DriversController` | `DriverFilterRequest`, `DriverRequest`, `DriverResponse`, `PagedResponse`, `PaginationRequest`, `SelectResponse` | `MiniErp.Domain.Entities.Logistics`, `Driver`, `DriverTrip`; `ApplicationDbContext` / EF configurations / migrations |
| `ExchangeRatesController` | `ExchangeRateFilterRequest`, `ExchangeRateRequest`, `ExchangeRateResolutionResponse`, `ExchangeRateResponse`, `ExchangeRateUpdateRequest`, `PagedResponse`, `PaginationRequest` | `.ApplyExchangeRate()`, `.ApplyOpeningExchangeRate()`, `ExchangeRate`, `.Touch()`, `ExchangeRateRules`, `.IsValidRate()`; `ApplicationDbContext` / EF configurations / migrations |
| `InventoryCostReportsController` | `InventoryCostReportFilterRequest`, `InventoryCostReportResponse`, `PaginationRequest` | feature-specific domain entity set; `ApplicationDbContext` / EF configurations / migrations |
| `InventoryCountsController` | `InventoryCountFilterRequest`, `InventoryCountListResponse`, `InventoryCountReconcileRequest`, `InventoryCountRequest`, `InventoryCountResponse`, `InventoryCountUpdateRequest`, `PagedResponse`, `PaginationRequest` | `InventoryCount`, `.Touch()`, `InventoryCountLine`; `ApplicationDbContext` / EF configurations / migrations |
| `InvoicesController` | `InvoiceFilterRequest`, `InvoiceItemBalanceResponse`, `InvoicePagedResponse`, `InvoiceRequest`, `InvoiceResponse`, `InvoiceUpdateRequest`, `PaginationRequest` | `MiniErp.Domain.Entities.Invoicing`, `Invoice`, `.CalculateTotal()`, `.Touch()`, `.GetPaymentStatus()`, `.ApplyExchangeRate()`; `ApplicationDbContext` / EF configurations / migrations |
| `ItemUnitsController` | `ItemUnitFilterRequest`, `ItemUnitRequest`, `ItemUnitResponse`, `PagedResponse`, `PaginationRequest`, `SelectResponse` | `ItemUnit`; `ApplicationDbContext` / EF configurations / migrations |
| `ItemsController` | `ItemFilterRequest`, `ItemRequest`, `ItemResponse`, `PagedResponse`, `PaginationRequest`, `SelectResponse` | `MiniErp.Domain.Entities.Catalog`, `Item`, `ItemUnit`, `ItemsCategory`, `.ApplyCostSnapshot()`, `ItemStoreBalance`; `ApplicationDbContext` / EF configurations / migrations |
| `ItemsCategoriesController` | `ItemsCategoryFilterRequest`, `ItemsCategoryRequest`, `ItemsCategoryResponse`, `ItemsCategorySelectResponse`, `ItemsCategoryUpdateRequest`, `PagedResponse`, `PaginationRequest` | feature-specific domain entity set; `ApplicationDbContext` / EF configurations / migrations |
| `PartnerOpeningBalancesController` | `PagedResponse`, `PaginationRequest`, `PartnerOpeningBalanceFilterRequest`, `PartnerOpeningBalanceRequest`, `PartnerOpeningBalanceResponse`, `PartnerOpeningBalanceUpdateRequest` | `PartnerOpeningBalance`, `.ApplyExchangeRate()`, `PartnerOpeningBalanceAmountRules`, `.IsValidAmount()`; `ApplicationDbContext` / EF configurations / migrations |
| `StatementsController` | `CashboxStatementFilterRequest`, `CashboxStatementResponse`, `DriverStatementFilterRequest`, `DriverStatementResponse`, `PaginationRequest`, `PartnerStatementFilterRequest`, `PartnerStatementResponse` | feature-specific domain entity set; `ApplicationDbContext` / EF configurations / migrations |
| `StockAdjustmentsController` | `PagedResponse`, `PaginationRequest`, `StockAdjustmentFilterRequest`, `StockAdjustmentListResponse`, `StockAdjustmentRequest`, `StockAdjustmentResponse`, `StockAdjustmentUpdateRequest` | `StockAdjustment`, `.Touch()`, `StockAdjustmentLine`, `StockAdjustmentMovementRules`, `.GetMovementType()`, `.IsInbound()`; `ApplicationDbContext` / EF configurations / migrations |
| `StockOpeningBalancesController` | `PagedResponse`, `PaginationRequest`, `StockOpeningBalanceFilterRequest`, `StockOpeningBalanceListResponse`, `StockOpeningBalanceRequest`, `StockOpeningBalanceResponse`, `StockOpeningBalanceUpdateRequest` | `StockOpeningBalance`, `StockOpeningBalanceAmountRules`, `.TryCalculate()`, `.HasPrecision()`, `StockOpeningBalanceLine`, `.CalculateAmounts()`; `ApplicationDbContext` / EF configurations / migrations |
| `StoreContainersController` | `PagedResponse`, `PaginationRequest`, `SelectResponse`, `StoreContainerFilterRequest`, `StoreContainerResponse`, `StoreContainerUpsertRequest`, `StoreContainerWorkspaceResponse` | `StoreContainer`; `ApplicationDbContext` / EF configurations / migrations |
| `StoresController` | `PagedResponse`, `PaginationRequest`, `SelectResponse`, `StoreFilterRequest`, `StoreRequest`, `StoreResponse` | `StoreContainer`, `ItemStoreBalance`, `.Apply()`, `Store`; `ApplicationDbContext` / EF configurations / migrations |
| `UsersController` | `PagedResponse`, `PaginationRequest`, `UserCompaniesRequest`, `UserCreateRequest`, `UserFilterRequest`, `UserResponse`, `UserUpdateRequest` | feature-specific domain entity set; `ApplicationDbContext` / EF configurations / migrations |

## Complete feature-flow traces

### Authentication and authorization

1. `client/src/App.tsx` calls `client/src/api.ts::apiRequest` for `/Auth/login`, `/Auth/select-company`, and `/Auth/logout`.
2. Those paths match `src/MiniErp.Api/Controllers/AuthController.cs`, which delegates to `IAuthenticationService` and `AuthenticationService`.
3. `AuthenticationService` creates/validates access, company-selection, and refresh tokens, uses `ApplicationUser`/refresh-token state, and references `ApplicationDbContext`.
4. The client sends `Authorization: Bearer <accessToken>` for protected calls and reads roles/company claims locally; backend controllers enforce `[Authorize]` and role restrictions such as `Roles = "Admin"`.

### Invoice flow

1. `client/src/components/InvoicePage.tsx` imports `apiRequest` and calls `/Invoices`, `/Invoices/{id}`, `/Invoices/item-balance`, plus select endpoints for related data.
2. The matching backend endpoint is `src/MiniErp.Api/Controllers/InvoicesController.cs` -> `IInvoiceService` -> `InvoiceService` partials (`Queries`, `Validation`, `Lines`, `SideEffects`, `Errors`).
3. DTOs include `InvoiceRequest`, `InvoiceUpdateRequest`, `InvoiceResponse`, `InvoicePagedResponse`, `InvoiceItemBalanceResponse`, and `InvoiceFilterRequest`.
4. The service reaches invoice domain entities (`Invoice`, `InvoiceLine`, `InvoicePayment`, container lines), inventory/costing rules, and `ApplicationDbContext`, whose EF configurations/migrations persist the changes.

### Inventory count / stock flow

1. `InventoryCountPage.tsx`, `StockAdjustmentPage.tsx`, `StockOpeningBalancePage.tsx`, and `InventoryCostReportPage.tsx` call the corresponding inventory paths through `apiRequest`.
2. These match `InventoryCountsController`, `StockAdjustmentsController`, `StockOpeningBalancesController`, and `InventoryCostReportsController`, then their application interfaces and Infrastructure services.
3. Request/response DTOs are feature-specific; persistence runs through inventory entities, movement/cost rules, `ApplicationDbContext`, EF configurations, and migrations.

### Generic master-data flow

`ErpShell.tsx` + `EntityPage.tsx` provide reusable CRUD for companies, exchange rates, users, countries, partners, stores, containers, drivers, item categories, item units, items, cashboxes, and cash movement types. Each configured endpoint maps to a same-named backend controller and service; changing a shared generic field can affect many screens at once.

## Current change-impact baseline

No feature change was requested in this run, so there are no concrete affected-file lists yet. For a requested change, report:

- **Backend files:** controller, feature request/response DTOs, validators, application interface, Infrastructure service, domain entity/rules, EF configuration/migration, and tests.
- **Frontend files:** page/component, shared `api.ts` types/helpers, generic `EntityPage`/`ErpShell` configuration, and any dependent modal/select components.
- **API contract:** whether route, HTTP verb, query/body fields, status codes, auth requirements, or response shape changes.
- **Models:** whether request/response TypeScript types and C# DTOs change, including pagination/problem-details behavior.
- **Security:** whether bearer-token handling, company claims, roles, `[Authorize]`, or Admin-only operations change.
- **Breaking changes:** removed/renamed routes or fields, changed enum values, required fields, status codes, pagination semantics, or permission changes.

## Recommended implementation plan

1. Pick the feature slice and start from the merged graph/contract table.
2. Trace the existing UI -> `apiRequest` -> route -> controller -> application service -> domain/persistence path.
3. Decide the API contract first; explicitly mark any breaking field/route/auth change.
4. Update backend DTOs/validators/services/domain/EF artifacts and backend tests as needed.
5. Update frontend page/components/shared types and endpoint callers as needed.
6. Run backend tests/build and frontend `npm run build`; verify endpoint and auth behavior.
7. Rebuild/update both Graphify graphs, merge again, and compare affected nodes/paths before review.

Graphify provides visibility and impact analysis only; it does not synchronize or edit either project automatically.


## Current CBE / Frankfurter import flow

The current merged graph contains a source-verified import path (Graphify does not create an AST edge between repositories):

1. `F:\client\client\src\components\ExchangeRatePage.tsx` automatically selects the configured CBE provider, company base currency, todayâ€™s local date, and eligible currencies.
2. The form checks `GET /api/v1/ExchangeRates?dateFrom=...&dateTo=...` for same-date records and protects existing rates by default.
3. Date/currency changes call Admin-only `POST /api/v1/ExchangeRates/import/preview`; this calls Frankfurter without writing and displays each returned rate and actual provider date.
4. The final `POST /api/v1/ExchangeRates/import` performs the provider fetch again, then compares and saves in a serializable transaction. Successful import increments the frontend reload signal and refreshes the grid.
5. The backend route is `ExchangeRatesController.PreviewImport`/`Import` -> `IExchangeRateService` -> `ExchangeRateService` -> `IExchangeRateProvider` -> `FrankfurterExchangeRateProvider` -> `ApplicationDbContext`.

### Provider availability note

The CBE provider publishes 19 currencies, but SAR/EGP and AED/EGP are not exposed as direct CBE pairs. Live requests to `/v2/rate/SAR/EGP?providers=CBE` and `/v2/rate/AED/EGP?providers=CBE` return 404, so the preview correctly reports â€œnot available.â€ The importer deliberately does not synthesize EUR-based cross-rates. USD/EGP can still return the latest available CBE date when the requested date has no publication.

Frankfurterâ€™s official CBE page documents the providerâ€™s 19-currency coverage and direct USD/EGP example: <https://frankfurter.dev/providers/cbe/>. The v2 documentation defines a 404 as a missing rate/resource and explains provider pinning: <https://frankfurter.dev/>.

### Verification

- Backend API build: passed (0 errors; existing migration-name warnings only in the full build output).
- Frontend production build: passed (`tsc -b` and Vite).
- Backend tests remain blocked by unrelated pre-existing invoice test constructor errors in `InvoicePaymentTermTests.cs` and `InvoiceServiceTests.cs`.
- Merged graph after this refresh: **4,343 nodes**, **11,639 edges**.
