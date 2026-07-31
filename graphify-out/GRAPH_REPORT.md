# Graph Report - MiniErp  (2026-08-01)

## Corpus Check
- 491 files · ~141,984 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 4036 nodes · 11134 edges · 202 communities (146 shown, 56 thin omitted)
- Extraction: 99% EXTRACTED · 1% INFERRED · 0% AMBIGUOUS · INFERRED: 88 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `821732e9`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- InvoiceServiceTests
- StockOpeningBalanceService
- PartnerOpeningBalanceServiceTests
- StockAdjustmentService
- .CreateAsync
- MiniErp.Infrastructure.Identity
- BusinessPartnerService
- AuthenticationService
- IRegister
- .CreateAsync
- CashMovementTypeService
- ItemsCategoryService
- CashboxService
- MiniErp.Domain.Entities.Companies
- InventoryCostingService
- ApplicationUser
- Task
- .Create
- CountryService
- ApiErrorResponseFactory
- DriverService
- MiniErp.Domain.Enums
- Company
- MiniErp.Application.Common.Abstractions
- MiniErp.Application.Common.Models
- AuditableEntity
- InventoryCountService
- InvoiceRequest
- .UpdateAsync
- MiniErp.Application.Features.Stores
- StoreContainerService
- Task
- InventoryStockService
- .NotFound
- Task
- .Success
- MiniErp.Infrastructure.Persistence.Configurations
- UserService
- MiniErp.Application.Features.Users
- Error
- MiniErp.Api.csproj
- CurrencyCode
- .TryParse
- Task
- CompanyService
- ExchangeRatesController
- CashVoucher
- MiniErp.Api
- Result
- StockOpeningBalanceServiceTests
- AccessTokenCompanyTestDatabase
- .CreateAsync
- ArabicIdentityErrorDescriber
- Task
- Task
- MiniErp.Application.Features.DriverTrips
- .GetAllAsync
- StoreService
- InventoryCountResponse
- CategoryTestDatabase
- ApplicationDbContext
- FinancialStatementService
- InventoryCostAllocation
- ExchangeRate
- .PrepareAsync
- AuditableEntityInterceptor
- .Create
- .Create
- UsersController
- ICurrentCompanyContext
- .CreateAsync
- ProducesResponseType&lt;ProblemDetails&gt;
- .Login
- .GetAll
- .Create
- .Create
- .Create
- http
- ExchangeRateService
- .GetRateAsync
- .Create
- CashVoucherUpdateRequest
- ContainerService
- InvoiceMappingRegister
- .GetCashboxStatement
- .Upsert
- .Create
- EnumRequestOperationDocumentationFilter
- PaginationRequest
- StockOpeningBalance
- InvoicePaymentTermTests
- SwaggerExtensions
- .Create
- MiniErp.Api.Swagger
- InventoryCostStatus
- ProducesResponseType&lt;IReadOnlyList&lt;SelectResponse&gt;&gt;
- IScopedService
- ContainerTestDatabase
- .UpdateCosts
- CurrentCompanyContext
- DriverTripService
- StatementResponses.cs
- .Apply
- .Apply
- .Apply
- JwtOptions
- CashboxMappingRegister
- InventoryCount
- UserRequestValidatorTests
- .GetAsync
- ArabicValidationConfiguration
- CashMovementTypeMappingRegister
- FrankfurterExchangeRateProviderTests
- CashVoucherMappingRegister
- InitialIdentity
- Migration
- .GetCostEntryAsync
- .ApplyPendingMigrationsAsync
- InventoryCountMappingRegister
- MappingConfiguration
- ItemsCategoryMappingRegister
- IOperationFilter
- .Apply
- .Apply
- .Apply
- .Apply
- .Apply
- .Apply
- .Apply
- .Apply
- .Apply
- StockAdjustmentMappingRegister
- .Apply
- .Apply
- .Apply
- InvoiceResponse
- .Apply
- StatementsSwaggerDocumentation.cs
- StockOpeningBalanceMappingRegister
- .Apply
- StockOpeningBalanceRequestValidatorTests
- .Apply
- ApiControllerBase
- PartnerOpeningBalanceAmountRules
- IntialCreate
- AddRefreshTokens
- addCompany
- addCompanyTenant
- AddUserManagement
- AddRefreshTokenCompanyContext
- AddDrivers
- MakeDriverLicenseNumberOptional
- AddBusinessPartners
- MakeDriverLicenseNumberRequired
- MakeDriverAndBusinessPartnerNamesUnique
- AddContainerStoreClassification
- EnforceUniqueActiveContainerStore
- AddReferenceAndContainerData
- improveContainers
- AddStockOpening
- UpdateStockOpeningLineAmounts
- AddPartnerOpeningBalance
- AddInvoiceWorkflow
- AddInvoiceLifecycle
- RemoveInvoiceReturnLinkage
- AddInvoiceDiscountAndPaidAmounts
- AddInvoiceActualDriver
- AllowDuplicateInvoiceNumbers
- AddCashManagement
- AllowDuplicateCashVoucherNumbers
- AddStockAdjustmentsAndInventoryCounts
- AddCompanyStockBalanceCheckSettings
- modifyInvoice
- MiniErp.Infrastructure.Persistence.Migrations
- addexchangerate
- addwbforinvoice
- additemCategory
- InventoryDocumentValidatorTests
- BusinessPartnerMappingRegister
- PartnerOpeningBalanceMappingRegister
- DriverTripBulkCostUpdateRequest
- Invoice
- ContainerMappingRegister
- CompanyAndExchangeRateAuthorizationTests
- StockOpeningBalanceAmountRules
- CountryMappingRegister
- CompanyMappingRegister
- DriverStatementRaw
- DriverMappingRegister
- AbstractValidator
- Q: Cross-project MiniErp feature flow impact analysis
- AddCompanyRowVersion
- DriverTripCostMappingRegister
- ItemUnitMappingRegister
- StoreMappingRegister
- AddExchangeRateProvider
- InventoryQuantityRules.cs
- MiniErp.Application
- .Apply
- MappingConfigurationTests
- StockAdjustmentsSwaggerDocumentation.cs

## God Nodes (most connected - your core abstractions)
1. `Result` - 295 edges
2. `InvoiceServiceTests` - 137 edges
3. `ApplicationDbContext` - 120 edges
4. `MiniErp.Domain.Enums` - 117 edges
5. `MiniErp.Application.Common.Models` - 89 edges
6. `PaginationRequest` - 87 edges
7. `MiniErp.Application.Common.Results` - 72 edges
8. `MiniErp.Application.Common.Abstractions` - 57 edges
9. `MiniErp.Domain.Entities.Companies` - 55 edges
10. `Company` - 55 edges

## Surprising Connections (you probably didn't know these)
- `TestCurrentCompanyContext` --implements--> `ICurrentCompanyContext`  [EXTRACTED]
  tests/MiniErp.Tests/CashManagement/CashManagementTestDatabase.cs → src/MiniErp.Application/Common/Abstractions/ICurrentCompanyContext.cs
- `TestCurrentCompanyContext` --implements--> `ICurrentCompanyContext`  [EXTRACTED]
  tests/MiniErp.Tests/Inventory/InventoryDocumentTestDatabase.cs → src/MiniErp.Application/Common/Abstractions/ICurrentCompanyContext.cs
- `TestCurrentCompanyContext` --implements--> `ICurrentCompanyContext`  [EXTRACTED]
  tests/MiniErp.Tests/BusinessPartners/BusinessPartnerContainerStoreServiceTests.cs → src/MiniErp.Application/Common/Abstractions/ICurrentCompanyContext.cs
- `TestCurrentCompanyContext` --implements--> `ICurrentCompanyContext`  [EXTRACTED]
  tests/MiniErp.Tests/BusinessPartners/BusinessPartnerIntegrityServiceTests.cs → src/MiniErp.Application/Common/Abstractions/ICurrentCompanyContext.cs
- `TestCurrentCompanyContext` --implements--> `ICurrentCompanyContext`  [EXTRACTED]
  tests/MiniErp.Tests/Containers/ContainerServiceTests.cs → src/MiniErp.Application/Common/Abstractions/ICurrentCompanyContext.cs

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **MiniErp Clean Architecture Layers** — readme_minierp_domain, readme_minierp_application, readme_minierp_infrastructure, readme_minierp_api [EXTRACTED 1.00]
- **Company-Scoped Authentication Flow** — readme_jwt_authentication, readme_company_selection_token, readme_company_scoped_access_token, readme_rotating_refresh_tokens [EXTRACTED 1.00]
- **Application Outcome Handling** — readme_result_pattern, readme_fluentvalidation, readme_global_exception_handling [EXTRACTED 1.00]

## Communities (202 total, 56 thin omitted)

### Community 0 - "InvoiceServiceTests"
Cohesion: 0.05
Nodes (23): Credit, Debit, InvoiceTestDatabase, InvoicePriceStatus, InvoiceLineRequest, InvoiceMovementRules, BusinessPartnerMovementType, InvoiceType (+15 more)

### Community 1 - "StockOpeningBalanceService"
Cohesion: 0.06
Nodes (43): OpeningMovementCost, ProducesResponseType&lt;PagedResponse&lt;StockOpeningBalanceListResponse&gt;&gt;, ProducesResponseType&lt;StockOpeningBalanceResponse&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost (+35 more)

### Community 2 - "PartnerOpeningBalanceServiceTests"
Cohesion: 0.05
Nodes (40): PartnerOpeningBalanceTestDatabase, ProducesResponseType&lt;PagedResponse&lt;PartnerOpeningBalanceResponse&gt;&gt;, ProducesResponseType&lt;PartnerOpeningBalanceResponse&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost (+32 more)

### Community 3 - "StockAdjustmentService"
Cohesion: 0.07
Nodes (36): MovementCostSnapshot, CancellationToken, Task, IStockAdjustmentService, StockAdjustmentFilterRequest, int, StockAdjustmentLineRequest, StockAdjustmentRequest (+28 more)

### Community 4 - ".CreateAsync"
Cohesion: 0.07
Nodes (27): int, CashVoucherRequest, CashVoucherRequestValidator, CashDirection, CashPartyType, PartnerAccountEffect, Fact, InlineData (+19 more)

### Community 5 - "MiniErp.Infrastructure.Identity"
Cohesion: 0.12
Nodes (9): MiniErp.Tests.Authentication, MiniErp.Infrastructure.Services.Users, MiniErp.Infrastructure.Services.Companies, MiniErp.Infrastructure.Identity, MiniErp.Application.Common.Authentication, string, ApplicationRoles, string (+1 more)

### Community 6 - "BusinessPartnerService"
Cohesion: 0.07
Nodes (30): BusinessPartnerIntegrityTestDatabase, BusinessPartnerContainerStoreResponse, BusinessPartnerFilterRequest, BusinessPartnerFilterRequestValidator, BusinessPartnerRequest, BusinessPartnerRequestValidator, IReadOnlyList, BusinessPartnerResponse (+22 more)

### Community 7 - "AuthenticationService"
Cohesion: 0.07
Nodes (24): Claim, CompanySelectionTokenData, MiniErp.Application.Features.Authentication, CompanyAccessResponse, CancellationToken, Task, IAuthenticationService, LoginRequest (+16 more)

### Community 8 - "IRegister"
Cohesion: 0.14
Nodes (9): IRegister, TypeAdapterConfig, ExchangeRateMappingRegister, TypeAdapterConfig, ItemMappingRegister, TypeAdapterConfig, StoreContainerMappingRegister, TypeAdapterConfig (+1 more)

### Community 9 - ".CreateAsync"
Cohesion: 0.11
Nodes (17): DateOnly, Fact, Task, InventoryCostingServiceTests, DateOnly, Fact, Task, InventoryCostReportServiceTests (+9 more)

### Community 10 - "CashMovementTypeService"
Cohesion: 0.07
Nodes (34): ProducesResponseType&lt;CashMovementTypeResponse&gt;, ProducesResponseType&lt;IReadOnlyList&lt;CashMovementTypeSelectResponse&gt;&gt;, ProducesResponseType&lt;PagedResponse&lt;CashMovementTypeResponse&gt;&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost (+26 more)

### Community 11 - "ItemsCategoryService"
Cohesion: 0.07
Nodes (32): ProducesResponseType&lt;IReadOnlyList&lt;ItemsCategorySelectResponse&gt;&gt;, ProducesResponseType&lt;ItemsCategoryResponse&gt;, ProducesResponseType&lt;PagedResponse&lt;ItemsCategoryResponse&gt;&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost (+24 more)

### Community 12 - "CashboxService"
Cohesion: 0.08
Nodes (32): ProducesResponseType&lt;CashboxResponse&gt;, ProducesResponseType&lt;IReadOnlyList&lt;CashboxSelectResponse&gt;&gt;, ProducesResponseType&lt;PagedResponse&lt;CashboxResponse&gt;&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost (+24 more)

### Community 13 - "MiniErp.Domain.Entities.Companies"
Cohesion: 0.12
Nodes (12): MiniErp.Infrastructure.Seeding, MiniErp.Domain.Entities.BusinessPartners, MiniErp.Domain.Entities.Catalog, MiniErp.Domain.Entities.Companies, MiniErp.Domain.Entities.Logistics, MiniErp.Domain.Common.Entities, MiniErp.Domain.Entities.Containers, MiniErp.Domain.Entities.CashManagement (+4 more)

### Community 14 - "InventoryCostingService"
Cohesion: 0.09
Nodes (24): InboundCostResult, PendingOutbound, Queue, CancellationToken, DateOnly, IReadOnlyCollection, IReadOnlyDictionary, Task (+16 more)

### Community 15 - "ApplicationUser"
Cohesion: 0.09
Nodes (25): IdentityUser, Guid, ICollection, ApplicationUser, AsyncServiceScope, Fact, Guid, IConfiguration (+17 more)

### Community 16 - "Task"
Cohesion: 0.10
Nodes (10): InventoryDeletionDatabase, Fact, MemberData, SqliteConnection, Task, Theory, TheoryData, ValueTask (+2 more)

### Community 17 - ".Create"
Cohesion: 0.25
Nodes (12): ProducesResponseType&lt;ContainerResponse&gt;, ProducesResponseType&lt;PagedResponse&lt;ContainerResponse&gt;&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost, HttpPut (+4 more)

### Community 18 - "CountryService"
Cohesion: 0.13
Nodes (13): CountryFilterRequest, CountryFilterRequestValidator, CountryRequest, CountryRequestValidator, CountryResponse, CancellationToken, IReadOnlyList, Task (+5 more)

### Community 19 - "ApiErrorResponseFactory"
Cohesion: 0.06
Nodes (33): ActionExecutingContext, MiniErp.Api.Errors, MiniErp.Api.Exceptions, MiniErp.Api.Validation, Exception, IDictionary, IExceptionHandler, IFluentValidationAutoValidationResultFactory (+25 more)

### Community 20 - "DriverService"
Cohesion: 0.08
Nodes (26): ProducesResponseType&lt;DriverResponse&gt;, ProducesResponseType&lt;PagedResponse&lt;DriverResponse&gt;&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost, HttpPut (+18 more)

### Community 21 - "MiniErp.Domain.Enums"
Cohesion: 0.05
Nodes (18): MiniErp.Infrastructure.Services.ExchangeRates, MiniErp.Infrastructure.Services.CashMovementTypes, MiniErp.Infrastructure.Services.PartnerOpeningBalances, MiniErp.Tests.ExchangeRates, MiniErp.Tests, MiniErp.Application.Features.ExchangeRates, MiniErp.Application.Features.PartnerOpeningBalances, MiniErp.Application.Features.CashVouchers (+10 more)

### Community 22 - "Company"
Cohesion: 0.10
Nodes (29): IServiceProvider, SeedBusinessPartner, SeedCompany, SeedContainer, SeedCountry, SeedDriver, SeedStore, SeedUser (+21 more)

### Community 23 - "MiniErp.Application.Common.Abstractions"
Cohesion: 0.08
Nodes (28): MiniErp.Infrastructure.Services.Containers, MiniErp.Tests.Inventory, MiniErp.Infrastructure.Services.ItemsCategories, MiniErp.Infrastructure.Services.BusinessPartners, MiniErp.Tests.Companies, MiniErp.Infrastructure, MiniErp.Infrastructure.Services.Stores, MiniErp.Tests.BusinessPartners (+20 more)

### Community 24 - "MiniErp.Application.Common.Models"
Cohesion: 0.05
Nodes (18): MiniErp.Infrastructure.Services.InventoryCounts, MiniErp.Infrastructure.Services.StockAdjustments, MiniErp.Application.Features.ItemUnits, MiniErp.Api.Extensions, MiniErp.Application.Features.InventoryCostReports, MiniErp.Application.Common.Models, MiniErp.Application.Features.InventoryCounts, MiniErp.Application.Features.Companies (+10 more)

### Community 25 - "AuditableEntity"
Cohesion: 0.09
Nodes (20): DateTime, AuditableEntity, ICollection, Container, DateOnly, ContainerMovement, StoreContainer, ICollection (+12 more)

### Community 26 - "InventoryCountService"
Cohesion: 0.14
Nodes (9): InventoryCountFilterRequest, CancellationToken, IEnumerable, int, IQueryable, IReadOnlyCollection, Task, InventoryCountService (+1 more)

### Community 27 - "InvoiceRequest"
Cohesion: 0.22
Nodes (7): int, InvoiceRequest, InvoiceUpdateRequest, IReadOnlyList, InvoiceValidationRules, PreparedInvoice, InvoiceService

### Community 28 - ".UpdateAsync"
Cohesion: 0.20
Nodes (12): CancellationToken, int, Task, InvoiceService, CancellationToken, IEnumerable, IReadOnlyCollection, List (+4 more)

### Community 29 - "MiniErp.Application.Features.Stores"
Cohesion: 0.11
Nodes (5): MiniErp.Application.Features.Stores, MiniErp.Infrastructure.Services.StoreContainers, MiniErp.Application.Features.BusinessPartners, MiniErp.Application.Features.Containers, MiniErp.Application.Features.StoreContainers

### Community 30 - "StoreContainerService"
Cohesion: 0.11
Nodes (16): CancellationToken, IReadOnlyList, Task, IStoreContainerService, StoreContainerFilterRequest, StoreContainerFilterRequestValidator, StoreContainerResponse, int (+8 more)

### Community 31 - "Task"
Cohesion: 0.13
Nodes (13): DriverTestDatabase, DateOnly, DateTimeOffset, Fact, InlineData, SqliteConnection, Task, Theory (+5 more)

### Community 32 - "InventoryStockService"
Cohesion: 0.11
Nodes (22): CancellationToken, DateOnly, DateTime, IReadOnlyCollection, IReadOnlyDictionary, Task, IInventoryStockService, InventoryMovementReference (+14 more)

### Community 33 - ".NotFound"
Cohesion: 0.14
Nodes (8): CashVoucherResponse, CancellationToken, int, IQueryable, Task, CashVoucherService, VoucherPreparation, VoucherPreparation

### Community 34 - "Task"
Cohesion: 0.06
Nodes (31): CompanyTestDatabase, DbConnection, DbTransaction, DbTransactionInterceptor, ExchangeRateRow, ExchangeRateTestDatabase, IAsyncDisposable, IsolationCaptureInterceptor (+23 more)

### Community 35 - ".Success"
Cohesion: 0.12
Nodes (14): CancellationToken, IReadOnlyList, Task, IItemService, ItemFilterRequest, ItemFilterRequestValidator, ItemRequest, ItemRequestValidator (+6 more)

### Community 36 - "MiniErp.Infrastructure.Persistence.Configurations"
Cohesion: 0.07
Nodes (31): MiniErp.Infrastructure.Persistence.Configurations, Item, ICollection, ItemUnit, InventoryCountLine, DateOnly, ICollection, ItemMovement (+23 more)

### Community 37 - "UserService"
Cohesion: 0.20
Nodes (12): UserCompanyResponse, UserResponse, CancellationToken, Guid, HashSet, IdentityResult, IQueryable, IReadOnlyCollection (+4 more)

### Community 38 - "MiniErp.Application.Features.Users"
Cohesion: 0.10
Nodes (15): MiniErp.Application.Features.Users, CancellationToken, Guid, IReadOnlyList, Task, IUserService, UserCompaniesRequest, UserCompaniesRequestValidator (+7 more)

### Community 39 - "Error"
Cohesion: 0.14
Nodes (4): Error, InvoiceService, PreparedInvoice, PaymentPreparation

### Community 40 - "MiniErp.Api.csproj"
Cohesion: 0.08
Nodes (26): Asp.Versioning.Mvc (10.0.0), Asp.Versioning.Mvc.ApiExplorer (10.0.0), Bogus (35.6.5), FluentValidation (12.1.1), FluentValidation.DependencyInjectionExtensions (12.1.1), Mapster (10.0.11), Microsoft.AspNetCore.Authentication.JwtBearer (10.0.9), Microsoft.AspNetCore.Identity.EntityFrameworkCore (10.0.10) (+18 more)

### Community 41 - "CurrencyCode"
Cohesion: 0.10
Nodes (15): BusinessPartner, DateOnly, BusinessPartnerMovement, DateOnly, PartnerOpeningBalance, InvoicePayment, CurrencyCode, EntityTypeBuilder (+7 more)

### Community 42 - ".TryParse"
Cohesion: 0.09
Nodes (19): MiniErp.Tests.Common, MiniErp.Application.Common.Parsing, MiniErp.Api.ModelBinding, CultureInfo, IModelBinder, IModelBinderProvider, ModelBinderProviderContext, ModelBindingContext (+11 more)

### Community 43 - "Task"
Cohesion: 0.16
Nodes (9): IsActive, IsDeleted, StoreContainerTestDatabase, Fact, SqliteConnection, Task, ValueTask, StoreContainerServiceTests (+1 more)

### Community 44 - "CompanyService"
Cohesion: 0.14
Nodes (13): CompanyFilterRequest, CompanyFilterRequestValidator, CompanyRequest, CompanyUpdateRequest, CompanyResponse, CancellationToken, IReadOnlyList, Task (+5 more)

### Community 45 - "ExchangeRatesController"
Cohesion: 0.20
Nodes (16): ProducesResponseType&lt;ExchangeRateImportPreviewResponse&gt;, ProducesResponseType&lt;ExchangeRateImportResponse&gt;, ProducesResponseType&lt;ExchangeRateResolutionResponse&gt;, ProducesResponseType&lt;ExchangeRateResponse&gt;, ProducesResponseType&lt;PagedResponse&lt;ExchangeRateResponse&gt;&gt;, Authorize, CancellationToken, DateOnly (+8 more)

### Community 46 - "CashVoucher"
Cohesion: 0.10
Nodes (17): ICollection, CashMovementType, DateOnly, DateTime, CashVoucher, DateOnly, Driver, DateOnly (+9 more)

### Community 47 - "MiniErp.Api"
Cohesion: 0.10
Nodes (29): ASP.NET Core Identity, Bogus, Clean Architecture, Company-Scoped Access Token, Company-Scoped Tenancy, Company Selection Token, Database Migrations, Entity Framework Core (+21 more)

### Community 48 - "Result"
Cohesion: 0.12
Nodes (16): Result, Result, CancellationToken, IReadOnlyList, Task, IItemUnitService, ItemUnitFilterRequest, ItemUnitFilterRequestValidator (+8 more)

### Community 49 - "StockOpeningBalanceServiceTests"
Cohesion: 0.21
Nodes (8): StockOpeningBalanceTestDatabase, Fact, IReadOnlyList, SqliteConnection, Task, ValueTask, StockOpeningBalanceServiceTests, StockOpeningBalanceTestDatabase

### Community 50 - "AccessTokenCompanyTestDatabase"
Cohesion: 0.13
Nodes (15): AccessTokenCompanyTestDatabase, AsyncServiceScope, ClaimsPrincipal, Fact, Guid, IConfiguration, int, ServiceProvider (+7 more)

### Community 51 - ".CreateAsync"
Cohesion: 0.17
Nodes (10): StoreTestDatabase, Fact, MemberData, SqliteConnection, Task, Theory, TheoryData, ValueTask (+2 more)

### Community 52 - "ArabicIdentityErrorDescriber"
Cohesion: 0.20
Nodes (3): IdentityError, IdentityErrorDescriber, ArabicIdentityErrorDescriber

### Community 53 - "Task"
Cohesion: 0.33
Nodes (4): ContainerTestDatabase, Fact, Task, ContainerServiceTests

### Community 54 - "Task"
Cohesion: 0.17
Nodes (9): CountryTestDatabase, Fact, InlineData, SqliteConnection, Task, Theory, ValueTask, CountryServiceTests (+1 more)

### Community 55 - "MiniErp.Application.Features.DriverTrips"
Cohesion: 0.12
Nodes (4): MiniErp.Application.Features.Cashboxes, MiniErp.Infrastructure.Services.DriverTrips, MiniErp.Application.Features.DriverTrips, MiniErp.Infrastructure.Services.Cashboxes

### Community 56 - ".GetAllAsync"
Cohesion: 0.12
Nodes (16): InvoiceFilterRequest, InvoiceFilterRequestValidator, InvoiceContainerLineResponse, InvoiceItemBalanceResponse, InvoiceListResponse, InvoicePagedResponse, InvoiceSummaryResponse, CancellationToken (+8 more)

### Community 57 - "StoreService"
Cohesion: 0.12
Nodes (14): SelectResponse, CancellationToken, IReadOnlyList, Task, IStoreService, StoreFilterRequest, StoreFilterRequestValidator, StoreRequest (+6 more)

### Community 58 - "InventoryCountResponse"
Cohesion: 0.33
Nodes (6): CancellationToken, Task, IInventoryCountService, InventoryCountLineResponse, InventoryCountListResponse, InventoryCountResponse

### Community 59 - "CategoryTestDatabase"
Cohesion: 0.19
Nodes (9): CategoryTestDatabase, Fact, InlineData, SqliteConnection, Task, Theory, ValueTask, CategoryTestDatabase (+1 more)

### Community 60 - "ApplicationDbContext"
Cohesion: 0.13
Nodes (12): DbContextOptions, DbSet, IdentityDbContext, ModelBuilder, Guid, IdentityRole, ApplicationDbContext, SqliteConnection (+4 more)

### Community 61 - "FinancialStatementService"
Cohesion: 0.19
Nodes (7): DriverStatementRaw, PartnerStatementRaw, CancellationToken, int, IQueryable, Task, FinancialStatementService

### Community 62 - "InventoryCostAllocation"
Cohesion: 0.10
Nodes (16): IEntityTypeConfiguration, CompanySettings, InventoryCostAllocation, DateTimeOffset, Guid, RefreshToken, Guid, UserCompany (+8 more)

### Community 63 - "ExchangeRate"
Cohesion: 0.15
Nodes (10): DateOnly, ICollection, Cashbox, DateOnly, ExchangeRate, ExchangeRateSource, EntityTypeBuilder, CashboxConfiguration (+2 more)

### Community 64 - ".PrepareAsync"
Cohesion: 0.20
Nodes (8): decimal, int, InvoiceAmountRules, CancellationToken, IReadOnlyList, PreparedInvoice, Task, InvoiceService

### Community 65 - "AuditableEntityInterceptor"
Cohesion: 0.18
Nodes (10): DbContext, DbContextEventData, EntityEntry, InterceptionResult, SaveChangesInterceptor, CancellationToken, DateTime, string (+2 more)

### Community 66 - ".Create"
Cohesion: 0.24
Nodes (12): ProducesResponseType&lt;CashVoucherResponse&gt;, ProducesResponseType&lt;PagedResponse&lt;CashVoucherResponse&gt;&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost, HttpPut (+4 more)

### Community 67 - ".Create"
Cohesion: 0.21
Nodes (14): ProducesResponseType&lt;InvoiceItemBalanceResponse&gt;, ProducesResponseType&lt;InvoicePagedResponse&gt;, ProducesResponseType&lt;InvoiceResponse&gt;, Authorize, CancellationToken, DateOnly, HttpDelete, HttpGet (+6 more)

### Community 68 - "UsersController"
Cohesion: 0.24
Nodes (13): ProducesResponseType&lt;IReadOnlyList&lt;string&gt;&gt;, ProducesResponseType&lt;PagedResponse&lt;UserResponse&gt;&gt;, ProducesResponseType&lt;UserResponse&gt;, CancellationToken, Guid, HttpDelete, HttpGet, HttpPost (+5 more)

### Community 69 - "ICurrentCompanyContext"
Cohesion: 0.13
Nodes (14): ICurrentCompanyContext, SeedCompanyContext, TestCurrentCompanyContext, TestCurrentCompanyContext, TestCurrentCompanyContext, TestCurrentCompanyContext, TestCurrentCompanyContext, TestCurrentCompanyContext (+6 more)

### Community 70 - ".CreateAsync"
Cohesion: 0.21
Nodes (7): BusinessPartnerContainerStoreTestDatabase, Fact, SqliteConnection, Task, ValueTask, BusinessPartnerContainerStoreServiceTests, BusinessPartnerContainerStoreTestDatabase

### Community 71 - "ProducesResponseType&lt;ProblemDetails&gt;"
Cohesion: 0.26
Nodes (13): ProducesResponseType&lt;PagedResponse&lt;StoreResponse&gt;&gt;, ProducesResponseType&lt;ProblemDetails&gt;, ProducesResponseType&lt;StoreResponse&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost (+5 more)

### Community 72 - ".Login"
Cohesion: 0.36
Nodes (9): AllowAnonymous, ProducesResponseType&lt;LoginResponse&gt;, ProducesResponseType&lt;TokenResponse&gt;, CancellationToken, HttpPost, IActionResult, ProducesResponseType, Task (+1 more)

### Community 73 - ".GetAll"
Cohesion: 0.26
Nodes (11): ProducesResponseType&lt;CompanyResponse&gt;, ProducesResponseType&lt;PagedResponse&lt;CompanyResponse&gt;&gt;, CancellationToken, HttpDelete, HttpGet, HttpPost, HttpPut, IActionResult (+3 more)

### Community 74 - ".Create"
Cohesion: 0.13
Nodes (22): ProducesResponseType&lt;InventoryCountResponse&gt;, ProducesResponseType&lt;PagedResponse&lt;InventoryCountListResponse&gt;&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost, HttpPut (+14 more)

### Community 75 - ".Create"
Cohesion: 0.25
Nodes (12): ProducesResponseType&lt;ItemResponse&gt;, ProducesResponseType&lt;PagedResponse&lt;ItemResponse&gt;&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost, HttpPut (+4 more)

### Community 76 - ".Create"
Cohesion: 0.25
Nodes (12): ProducesResponseType&lt;ItemUnitResponse&gt;, ProducesResponseType&lt;PagedResponse&lt;ItemUnitResponse&gt;&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost, HttpPut (+4 more)

### Community 77 - "http"
Cohesion: 0.12
Nodes (17): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables, launchBrowser, launchUrl, applicationUrl (+9 more)

### Community 78 - "ExchangeRateService"
Cohesion: 0.05
Nodes (35): ExchangeRateFilterRequest, ExchangeRateImportPreviewResponse, ExchangeRateImportRequest, ExchangeRateImportItemResponse, ExchangeRateImportItemStatus, ExchangeRateImportResponse, ExchangeRateRequest, ExchangeRateUpdateRequest (+27 more)

### Community 79 - ".GetRateAsync"
Cohesion: 0.13
Nodes (13): HttpStatusCode, CancellationToken, DateOnly, Task, ExternalExchangeRate, IExchangeRateProvider, CancellationToken, DateOnly (+5 more)

### Community 80 - ".Create"
Cohesion: 0.24
Nodes (12): ProducesResponseType&lt;PagedResponse&lt;StockAdjustmentListResponse&gt;&gt;, ProducesResponseType&lt;StockAdjustmentResponse&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost, HttpPut (+4 more)

### Community 81 - "CashVoucherUpdateRequest"
Cohesion: 0.29
Nodes (6): CashVoucherFilterRequest, CashVoucherFilterRequestValidator, CashVoucherUpdateRequest, CancellationToken, Task, ICashVoucherService

### Community 82 - "ContainerService"
Cohesion: 0.12
Nodes (14): ContainerFilterRequest, ContainerFilterRequestValidator, ContainerRequest, ContainerRequestValidator, ContainerResponse, CancellationToken, IReadOnlyList, Task (+6 more)

### Community 84 - ".GetCashboxStatement"
Cohesion: 0.17
Nodes (14): ProducesResponseType&lt;CashboxStatementResponse&gt;, ProducesResponseType&lt;DriverStatementResponse&gt;, ProducesResponseType&lt;PartnerStatementResponse&gt;, CancellationToken, HttpGet, IActionResult, Task, StatementsController (+6 more)

### Community 85 - ".Upsert"
Cohesion: 0.25
Nodes (11): ProducesResponseType&lt;IReadOnlyList&lt;StoreContainerResponse&gt;&gt;, ProducesResponseType&lt;PagedResponse&lt;StoreContainerResponse&gt;&gt;, ProducesResponseType&lt;StoreContainerResponse&gt;, ProducesResponseType&lt;StoreContainerWorkspaceResponse&gt;, Authorize, CancellationToken, HttpGet, HttpPut (+3 more)

### Community 86 - ".Create"
Cohesion: 0.25
Nodes (12): ProducesResponseType&lt;CountryResponse&gt;, ProducesResponseType&lt;PagedResponse&lt;CountryResponse&gt;&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost, HttpPut (+4 more)

### Community 87 - "EnumRequestOperationDocumentationFilter"
Cohesion: 0.20
Nodes (9): EnumProperty, HashSet, int, IReadOnlyList, OpenApiOperation, OperationFilterContext, Type, EnumProperty (+1 more)

### Community 88 - "PaginationRequest"
Cohesion: 0.18
Nodes (11): CancellationToken, IOrderedQueryable, Task, IPaginationService, PagedResponse, int, PaginationRequest, CancellationToken (+3 more)

### Community 89 - "StockOpeningBalance"
Cohesion: 0.18
Nodes (8): DateOnly, ICollection, StockOpeningBalance, StockOpeningBalanceLine, EntityTypeBuilder, StockOpeningBalanceConfiguration, EntityTypeBuilder, StockOpeningBalanceLineConfiguration

### Community 90 - "InvoicePaymentTermTests"
Cohesion: 0.27
Nodes (4): Fact, InlineData, Theory, InvoicePaymentTermTests

### Community 91 - "SwaggerExtensions"
Cohesion: 0.29
Nodes (4): IConfiguration, IServiceCollection, WebApplication, SwaggerExtensions

### Community 92 - ".Create"
Cohesion: 0.15
Nodes (7): OpenApiOperation, OperationFilterContext, StoreContainersSwaggerDocumentation, OpenApiOperation, OperationFilterContext, StoresSwaggerDocumentation, SwaggerOperationDescription

### Community 93 - "MiniErp.Api.Swagger"
Cohesion: 0.17
Nodes (8): MiniErp.Api.Swagger, OpenApiOperation, OperationFilterContext, InventoryCostReportsSwaggerDocumentation, OpenApiOperation, OperationFilterContext, string, UnifiedErrorResponseSwaggerFilter

### Community 94 - "InventoryCostStatus"
Cohesion: 0.33
Nodes (5): InvoiceLineResponse, InventoryCostStatus, DateOnly, DateTime, MovementProjection

### Community 95 - "ProducesResponseType&lt;IReadOnlyList&lt;SelectResponse&gt;&gt;"
Cohesion: 0.21
Nodes (14): ProducesResponseType&lt;BusinessPartnerContainerStoreResponse&gt;, ProducesResponseType&lt;BusinessPartnerResponse&gt;, ProducesResponseType&lt;IReadOnlyList&lt;SelectResponse&gt;&gt;, ProducesResponseType&lt;PagedResponse&lt;BusinessPartnerResponse&gt;&gt;, Authorize, CancellationToken, HttpDelete, HttpGet (+6 more)

### Community 96 - "IScopedService"
Cohesion: 0.25
Nodes (5): Guid, ICurrentUserService, IScopedService, Guid, CurrentUserService

### Community 97 - "ContainerTestDatabase"
Cohesion: 0.29
Nodes (3): SqliteConnection, ValueTask, ContainerTestDatabase

### Community 98 - ".UpdateCosts"
Cohesion: 0.24
Nodes (9): ProducesResponseType&lt;DriverTripBulkCostUpdateResponse&gt;, ProducesResponseType&lt;PagedResponse&lt;DriverTripCostResponse&gt;&gt;, Authorize, CancellationToken, HttpGet, HttpPut, IActionResult, Task (+1 more)

### Community 99 - "CurrentCompanyContext"
Cohesion: 0.29
Nodes (4): ClaimsPrincipal, CompanyClaimResolver, int, CurrentCompanyContext

### Community 100 - "DriverTripService"
Cohesion: 0.31
Nodes (4): CancellationToken, int, Task, DriverTripService

### Community 101 - "StatementResponses.cs"
Cohesion: 0.17
Nodes (12): CancellationToken, Task, IFinancialStatementService, CashboxStatementItemResponse, CashboxStatementResponse, CashboxStatementSummaryResponse, DriverStatementItemResponse, DriverStatementResponse (+4 more)

### Community 102 - ".Apply"
Cohesion: 0.40
Nodes (3): OpenApiOperation, OperationFilterContext, CashMovementTypesSwaggerDocumentation

### Community 103 - ".Apply"
Cohesion: 0.27
Nodes (6): IOpenApiSchema, ISchemaFilter, SchemaFilterContext, Type, EnumDocumentationFormatter, EnumSchemaDocumentationFilter

### Community 104 - ".Apply"
Cohesion: 0.40
Nodes (3): OpenApiOperation, OperationFilterContext, DriversSwaggerDocumentation

### Community 105 - "JwtOptions"
Cohesion: 0.27
Nodes (7): IConfiguration, IServiceCollection, DependencyInjection, string, JwtOptions, JwtTokenOptions, RefreshTokenOptions

### Community 107 - "InventoryCount"
Cohesion: 0.28
Nodes (6): DateOnly, DateTime, ICollection, InventoryCount, EntityTypeBuilder, InventoryCountConfiguration

### Community 108 - "UserRequestValidatorTests"
Cohesion: 0.39
Nodes (4): MiniErp.Tests.Users, Fact, Task, UserRequestValidatorTests

### Community 109 - ".GetAsync"
Cohesion: 0.11
Nodes (15): MovementProjection, CancellationToken, Task, IInventoryCostReportService, InventoryCostReportFilterRequest, InventoryCostReportFilterRequestValidator, InventoryCostAllocationReportResponse, InventoryCostReportItemResponse (+7 more)

### Community 110 - "ArabicValidationConfiguration"
Cohesion: 0.29
Nodes (5): MiniErp.Application.Common.Validation, LanguageManager, IReadOnlyDictionary, ArabicLanguageManager, ArabicValidationConfiguration

### Community 112 - "FrankfurterExchangeRateProviderTests"
Cohesion: 0.22
Nodes (9): HttpMessageHandler, HttpRequestMessage, HttpResponseMessage, CancellationToken, Fact, Task, FrankfurterExchangeRateProviderTests, StubHandler (+1 more)

### Community 115 - "Migration"
Cohesion: 0.50
Nodes (3): Migration, MigrationBuilder, AddTablesItemAndItemUnit

### Community 116 - ".GetCostEntryAsync"
Cohesion: 0.21
Nodes (7): DriverTripCostFilterRequest, DriverTripCostFilterRequestValidator, DriverTripBulkCostUpdateResponse, DriverTripCostResponse, CancellationToken, Task, IDriverTripService

### Community 117 - ".ApplyPendingMigrationsAsync"
Cohesion: 0.33
Nodes (4): CancellationToken, Task, WebApplication, DatabaseMigrationExtensions

### Community 119 - "MappingConfiguration"
Cohesion: 0.40
Nodes (3): bool, object, MappingConfiguration

### Community 121 - "IOperationFilter"
Cohesion: 0.18
Nodes (7): IOperationFilter, OpenApiOperation, OperationFilterContext, AllowAnonymousOperationFilter, OpenApiOperation, OperationFilterContext, InventoryCountsSwaggerDocumentation

### Community 122 - ".Apply"
Cohesion: 0.40
Nodes (3): OpenApiOperation, OperationFilterContext, AuthenticationSwaggerDocumentation

### Community 123 - ".Apply"
Cohesion: 0.40
Nodes (3): OpenApiOperation, OperationFilterContext, BusinessPartnersSwaggerDocumentation

### Community 124 - ".Apply"
Cohesion: 0.40
Nodes (3): OpenApiOperation, OperationFilterContext, CashboxesSwaggerDocumentation

### Community 125 - ".Apply"
Cohesion: 0.40
Nodes (3): OpenApiOperation, OperationFilterContext, CashVouchersSwaggerDocumentation

### Community 126 - ".Apply"
Cohesion: 0.40
Nodes (3): OpenApiOperation, OperationFilterContext, CompaniesSwaggerDocumentation

### Community 127 - ".Apply"
Cohesion: 0.40
Nodes (3): OpenApiOperation, OperationFilterContext, ContainersSwaggerDocumentation

### Community 128 - ".Apply"
Cohesion: 0.40
Nodes (3): OpenApiOperation, OperationFilterContext, CountriesSwaggerDocumentation

### Community 129 - ".Apply"
Cohesion: 0.40
Nodes (3): OpenApiOperation, OperationFilterContext, DriverTripsSwaggerDocumentation

### Community 130 - ".Apply"
Cohesion: 0.40
Nodes (3): OpenApiOperation, OperationFilterContext, ExchangeRatesSwaggerDocumentation

### Community 132 - ".Apply"
Cohesion: 0.40
Nodes (3): OpenApiOperation, OperationFilterContext, InvoicesSwaggerDocumentation

### Community 133 - ".Apply"
Cohesion: 0.40
Nodes (3): OpenApiOperation, OperationFilterContext, ItemsCategoriesSwaggerDocumentation

### Community 134 - ".Apply"
Cohesion: 0.40
Nodes (3): OpenApiOperation, OperationFilterContext, ItemsSwaggerDocumentation

### Community 135 - "InvoiceResponse"
Cohesion: 0.40
Nodes (5): CancellationToken, DateOnly, Task, IInvoiceService, InvoiceResponse

### Community 136 - ".Apply"
Cohesion: 0.40
Nodes (3): OpenApiOperation, OperationFilterContext, PartnerOpeningBalancesSwaggerDocumentation

### Community 137 - "StatementsSwaggerDocumentation.cs"
Cohesion: 0.40
Nodes (3): OpenApiOperation, OperationFilterContext, StatementsSwaggerDocumentation

### Community 139 - ".Apply"
Cohesion: 0.40
Nodes (3): OpenApiOperation, OperationFilterContext, StockOpeningBalancesSwaggerDocumentation

### Community 140 - "StockOpeningBalanceRequestValidatorTests"
Cohesion: 0.35
Nodes (4): Fact, InlineData, Theory, StockOpeningBalanceRequestValidatorTests

### Community 141 - ".Apply"
Cohesion: 0.40
Nodes (3): OpenApiOperation, OperationFilterContext, UsersSwaggerDocumentation

### Community 142 - "ApiControllerBase"
Cohesion: 0.20
Nodes (8): ControllerBase, ProducesResponseType&lt;InventoryCostReportResponse&gt;, ApiControllerBase, CancellationToken, HttpGet, IActionResult, Task, InventoryCostReportsController

### Community 143 - "PartnerOpeningBalanceAmountRules"
Cohesion: 0.40
Nodes (3): decimal, int, PartnerOpeningBalanceAmountRules

### Community 173 - "MiniErp.Infrastructure.Persistence.Migrations"
Cohesion: 0.33
Nodes (3): MiniErp.Infrastructure.Persistence.Migrations, MigrationBuilder, AddInvoiceContentType

### Community 180 - "DriverTripBulkCostUpdateRequest"
Cohesion: 0.33
Nodes (5): int, DriverTripBulkCostUpdateRequest, DriverTripCostUpdateItem, DriverTripBulkCostUpdateRequestValidator, DriverTripCostUpdateItemValidator

### Community 181 - "Invoice"
Cohesion: 0.10
Nodes (15): ICollection, ItemsCategory, DateOnly, DateTime, ICollection, Invoice, Country, InvoiceContentType (+7 more)

### Community 184 - "CompanyAndExchangeRateAuthorizationTests"
Cohesion: 0.25
Nodes (5): MiniErp.Tests.Authorization, Fact, InlineData, Theory, CompanyAndExchangeRateAuthorizationTests

### Community 185 - "StockOpeningBalanceAmountRules"
Cohesion: 0.40
Nodes (3): decimal, int, StockOpeningBalanceAmountRules

### Community 188 - "DriverStatementRaw"
Cohesion: 0.33
Nodes (7): DriverStatementSourceType, PartnerStatementSourceType, DateOnly, DateTime, CashboxStatementRaw, DriverStatementRaw, PartnerStatementRaw

### Community 190 - "AbstractValidator"
Cohesion: 0.06
Nodes (29): AbstractValidator, Expression, PaginationRequestValidator, CashVoucherUpdateRequestValidator, CashVoucherValidationRules, CompanyRequestValidator, CompanyUpdateRequestValidator, int (+21 more)

### Community 191 - "Q: Cross-project MiniErp feature flow impact analysis"
Cohesion: 0.40
Nodes (4): Answer, Outcome, Q: Cross-project MiniErp feature flow impact analysis, Source Nodes

### Community 204 - ".Apply"
Cohesion: 0.40
Nodes (3): OpenApiOperation, OperationFilterContext, ItemUnitsSwaggerDocumentation

### Community 212 - "StockAdjustmentsSwaggerDocumentation.cs"
Cohesion: 0.40
Nodes (3): OpenApiOperation, OperationFilterContext, StockAdjustmentsSwaggerDocumentation

## Knowledge Gaps
- **98 isolated node(s):** `Asp.Versioning.Mvc (10.0.0)`, `Asp.Versioning.Mvc.ApiExplorer (10.0.0)`, `FluentValidation.DependencyInjectionExtensions (12.1.1)`, `Microsoft.EntityFrameworkCore.Design (10.0.10)`, `Microsoft.OpenApi (2.11.0)` (+93 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **56 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Result` connect `Result` to `StockOpeningBalanceService`, `PartnerOpeningBalanceServiceTests`, `StockAdjustmentService`, `BusinessPartnerService`, `AuthenticationService`, `InvoiceResponse`, `CashMovementTypeService`, `ItemsCategoryService`, `CashboxService`, `ApplicationUser`, `CountryService`, `ApiErrorResponseFactory`, `DriverService`, `InventoryCountService`, `.UpdateAsync`, `StoreContainerService`, `.NotFound`, `Task`, `.Success`, `UserService`, `MiniErp.Application.Features.Users`, `Error`, `CompanyService`, `.GetAllAsync`, `StoreService`, `InventoryCountResponse`, `FinancialStatementService`, `.PrepareAsync`, `ExchangeRateService`, `.GetRateAsync`, `CashVoucherUpdateRequest`, `ContainerService`, `PaginationRequest`, `IScopedService`, `DriverTripService`, `StatementResponses.cs`, `.GetAsync`, `.GetCostEntryAsync`?**
  _High betweenness centrality (0.173) - this node is a cross-community bridge._
- **Why does `ApplicationDbContext` connect `ApplicationDbContext` to `InvoiceServiceTests`, `StockOpeningBalanceService`, `PartnerOpeningBalanceServiceTests`, `StockAdjustmentService`, `BusinessPartnerService`, `.CreateAsync`, `MiniErp.Domain.Entities.Companies`, `ApplicationUser`, `Task`, `Company`, `AuditableEntity`, `Task`, `Task`, `MiniErp.Infrastructure.Persistence.Configurations`, `CurrencyCode`, `Task`, `CashVoucher`, `StockOpeningBalanceServiceTests`, `AccessTokenCompanyTestDatabase`, `.CreateAsync`, `Invoice`, `Task`, `Task`, `CategoryTestDatabase`, `InventoryCostAllocation`, `ExchangeRate`, `.CreateAsync`, `StockOpeningBalance`, `ContainerTestDatabase`, `InventoryCount`?**
  _High betweenness centrality (0.114) - this node is a cross-community bridge._
- **Why does `Error` connect `Error` to `InvoiceServiceTests`, `StockOpeningBalanceService`, `PartnerOpeningBalanceServiceTests`, `StockAdjustmentService`, `BusinessPartnerService`, `AuthenticationService`, `CashMovementTypeService`, `ItemsCategoryService`, `CashboxService`, `InventoryCostingService`, `CountryService`, `ApiErrorResponseFactory`, `DriverService`, `InventoryCountService`, `.UpdateAsync`, `StoreContainerService`, `InventoryStockService`, `.NotFound`, `.Success`, `UserService`, `CompanyService`, `Result`, `StoreService`, `FinancialStatementService`, `.PrepareAsync`, `ExchangeRateService`, `.GetRateAsync`, `ContainerService`, `DriverTripService`, `.GetAsync`?**
  _High betweenness centrality (0.100) - this node is a cross-community bridge._
- **What connects `Asp.Versioning.Mvc (10.0.0)`, `Asp.Versioning.Mvc.ApiExplorer (10.0.0)`, `FluentValidation.DependencyInjectionExtensions (12.1.1)` to the rest of the system?**
  _98 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `InvoiceServiceTests` be split into smaller, more focused modules?**
  _Cohesion score 0.05006493506493506 - nodes in this community are weakly interconnected._
- **Should `StockOpeningBalanceService` be split into smaller, more focused modules?**
  _Cohesion score 0.061122538936232734 - nodes in this community are weakly interconnected._
- **Should `PartnerOpeningBalanceServiceTests` be split into smaller, more focused modules?**
  _Cohesion score 0.054431960049937576 - nodes in this community are weakly interconnected._