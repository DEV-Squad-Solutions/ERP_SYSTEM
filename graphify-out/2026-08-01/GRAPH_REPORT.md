# Graph Report - MiniErp  (2026-08-01)

## Corpus Check
- 490 files · ~141,681 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 4028 nodes · 11102 edges · 208 communities (160 shown, 48 thin omitted)
- Extraction: 99% EXTRACTED · 1% INFERRED · 0% AMBIGUOUS · INFERRED: 87 edges (avg confidence: 0.8)
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
- ApplicationRoles.cs
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
- InvoiceType
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
- CashVoucherService
- Task
- SelectResponse
- AuditableEntityConfiguration
- UserService
- MiniErp.Application.Features.Users
- Error
- MiniErp.Api.csproj
- CurrencyCode
- .TryParse
- Task
- CompanyService
- ExchangeRatesController
- Driver
- MiniErp.Api
- ItemUnitService
- StockOpeningBalanceServiceTests
- AccessTokenCompanyTestDatabase
- .CreateAsync
- ArabicIdentityErrorDescriber
- Task
- Task
- ItemMovement
- .GetAllAsync
- StoreService
- Result
- CategoryTestDatabase
- ApplicationDbContext
- FinancialStatementService
- InventoryCostAllocation
- Task
- .PrepareAsync
- AuditableEntityInterceptor
- .Create
- .Create
- UsersController
- ICurrentCompanyContext
- .CreateAsync
- StoresController
- InvoiceService
- ProducesResponseType&lt;ProblemDetails&gt;
- .Create
- .Create
- .Create
- http
- ExchangeRateService
- .GetRateAsync
- .Create
- .ProcessInbound
- ContainerService
- InvoiceMappingRegister
- .GetCashboxStatement
- .Upsert
- .Create
- EnumRequestOperationDocumentationFilter
- PagedResponse
- StockOpeningBalance
- InvoicePaymentTermTests
- MiniErp.Api.Swagger
- .Create
- IOperationFilter
- InventoryCostReportService
- ProducesResponseType&lt;IReadOnlyList&lt;SelectResponse&gt;&gt;
- .LoadMovementCostsAsync
- .Create
- .UpdateCosts
- InventoryCountRequest
- DriverTripService
- StatementResponses.cs
- .IsValidRate
- .Apply
- StockOpeningBalanceRequest
- JwtOptions
- .GetRateAsync
- InventoryCount
- UserRequestValidatorTests
- .GetAsync
- ArabicValidationConfiguration
- ExchangeRateServiceTests
- .SendAsync
- .GetAllAsync
- Migration
- AddTablesItemAndItemUnit
- .GetCostEntryAsync
- .ApplyPendingMigrationsAsync
- .GetSnapshotsAsync
- MappingConfiguration
- CashManagementValidatorTests
- AllowAnonymousOperationFilter
- .Apply
- .Apply
- .Apply
- .Apply
- .Apply
- .Apply
- .Apply
- .Apply
- .Apply
- InventoryCostReportsSwaggerDocumentation.cs
- .Apply
- .Apply
- .Apply
- InvoiceResponse
- .Apply
- StatementsSwaggerDocumentation.cs
- .AddMovementAsync
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
- MiniErp.Api.Errors
- MiniErp.Application.Features.PartnerOpeningBalances
- DriverTripBulkCostUpdateRequest
- Invoice
- CashboxStatementFilterRequest
- CompanyAndExchangeRateAuthorizationTests
- StockOpeningBalanceLine
- IAsyncDisposable
- MiniErp.Application.Features.Companies
- DriverStatementRaw
- StoreContainerUpsertRequest
- AbstractValidator
- Q: Cross-project MiniErp feature flow impact analysis
- AddCompanyRowVersion
- PaginationRequest
- .SeedInvoiceFilterDataAsync
- InvoiceLineRequest
- AddExchangeRateProvider
- StoreContainerFilterRequest
- CustomClaimTypes.cs
- InventoryQuantityRules.cs
- MiniErp.Application
- .DisposeAsync
- .Apply
- .Apply
- MappingConfigurationTests
- StockAdjustmentsSwaggerDocumentation.cs

## God Nodes (most connected - your core abstractions)
1. `Result` - 293 edges
2. `InvoiceServiceTests` - 137 edges
3. `ApplicationDbContext` - 120 edges
4. `MiniErp.Domain.Enums` - 116 edges
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

## Communities (208 total, 48 thin omitted)

### Community 0 - "InvoiceServiceTests"
Cohesion: 0.09
Nodes (5): Fact, SqliteConnection, Task, InvoiceServiceTests, InvoiceTestDatabase

### Community 1 - "StockOpeningBalanceService"
Cohesion: 0.18
Nodes (12): StockOpeningBalanceResponse, CancellationToken, DateOnly, IEnumerable, int, IQueryable, IReadOnlyCollection, List (+4 more)

### Community 2 - "PartnerOpeningBalanceServiceTests"
Cohesion: 0.05
Nodes (40): PartnerOpeningBalanceTestDatabase, ProducesResponseType&lt;PagedResponse&lt;PartnerOpeningBalanceResponse&gt;&gt;, ProducesResponseType&lt;PartnerOpeningBalanceResponse&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost (+32 more)

### Community 3 - "StockAdjustmentService"
Cohesion: 0.06
Nodes (39): MovementCostSnapshot, CancellationToken, Task, IStockAdjustmentService, StockAdjustmentFilterRequest, int, StockAdjustmentLineRequest, StockAdjustmentRequest (+31 more)

### Community 4 - ".CreateAsync"
Cohesion: 0.10
Nodes (19): CashDirection, PartnerAccountEffect, Fact, InlineData, Task, Theory, CashMasterServiceTests, Fact (+11 more)

### Community 6 - "BusinessPartnerService"
Cohesion: 0.07
Nodes (33): ProducesResponseType&lt;BusinessPartnerContainerStoreResponse&gt;, ProducesResponseType&lt;BusinessPartnerResponse&gt;, ProducesResponseType&lt;PagedResponse&lt;BusinessPartnerResponse&gt;&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost (+25 more)

### Community 7 - "AuthenticationService"
Cohesion: 0.06
Nodes (33): AllowAnonymous, Claim, CompanySelectionTokenData, MiniErp.Application.Features.Authentication, ProducesResponseType&lt;LoginResponse&gt;, ProducesResponseType&lt;TokenResponse&gt;, CancellationToken, HttpPost (+25 more)

### Community 8 - "IRegister"
Cohesion: 0.03
Nodes (29): IRegister, TypeAdapterConfig, CashboxMappingRegister, TypeAdapterConfig, CashMovementTypeMappingRegister, TypeAdapterConfig, CashVoucherMappingRegister, TypeAdapterConfig (+21 more)

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
Cohesion: 0.07
Nodes (32): ProducesResponseType&lt;CashboxResponse&gt;, ProducesResponseType&lt;IReadOnlyList&lt;CashboxSelectResponse&gt;&gt;, ProducesResponseType&lt;PagedResponse&lt;CashboxResponse&gt;&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost (+24 more)

### Community 13 - "MiniErp.Domain.Entities.Companies"
Cohesion: 0.06
Nodes (13): MiniErp.Infrastructure.Seeding, MiniErp.Domain.Entities.BusinessPartners, MiniErp.Domain.Entities.Catalog, MiniErp.Domain.Entities.Companies, MiniErp.Domain.Entities.Logistics, MiniErp.Domain.Common.Entities, MiniErp.Domain.Entities.Containers, MiniErp.Domain.Entities.CashManagement (+5 more)

### Community 14 - "InventoryCostingService"
Cohesion: 0.23
Nodes (11): InboundCostResult, InventoryCostingKey, CancellationToken, DateOnly, int, IReadOnlyCollection, IReadOnlyDictionary, string (+3 more)

### Community 15 - "ApplicationUser"
Cohesion: 0.09
Nodes (25): IdentityUser, Guid, ICollection, ApplicationUser, AsyncServiceScope, Fact, Guid, IConfiguration (+17 more)

### Community 16 - "Task"
Cohesion: 0.10
Nodes (10): InventoryDeletionDatabase, Fact, MemberData, SqliteConnection, Task, Theory, TheoryData, ValueTask (+2 more)

### Community 17 - "InvoiceType"
Cohesion: 0.09
Nodes (13): Credit, Debit, InvoicePriceStatus, InvoiceMovementRules, BusinessPartnerMovementType, InvoiceType, ItemMovementType, PaymentTerm (+5 more)

### Community 18 - "CountryService"
Cohesion: 0.13
Nodes (13): CountryFilterRequest, CountryFilterRequestValidator, CountryRequest, CountryRequestValidator, CountryResponse, CancellationToken, IReadOnlyList, Task (+5 more)

### Community 19 - "ApiErrorResponseFactory"
Cohesion: 0.07
Nodes (30): ActionExecutingContext, Exception, IDictionary, IExceptionHandler, IFluentValidationAutoValidationResultFactory, IValidationContext, KeyValuePair, ModelStateDictionary (+22 more)

### Community 20 - "DriverService"
Cohesion: 0.15
Nodes (12): DriverFilterRequest, DriverRequest, DriverResponse, CancellationToken, IReadOnlyList, Task, IDriverService, CancellationToken (+4 more)

### Community 21 - "MiniErp.Domain.Enums"
Cohesion: 0.08
Nodes (17): MiniErp.Infrastructure.Services.ExchangeRates, MiniErp.Infrastructure.Services.InventoryCounts, MiniErp.Infrastructure.Services.StockAdjustments, MiniErp.Infrastructure.Services.CashMovementTypes, MiniErp.Infrastructure.Services.PartnerOpeningBalances, MiniErp.Tests.ExchangeRates, MiniErp.Tests, MiniErp.Application.Features.ExchangeRates (+9 more)

### Community 22 - "Company"
Cohesion: 0.10
Nodes (29): IServiceProvider, SeedBusinessPartner, SeedCompany, SeedContainer, SeedCountry, SeedDriver, SeedStore, SeedUser (+21 more)

### Community 23 - "MiniErp.Application.Common.Abstractions"
Cohesion: 0.07
Nodes (33): MiniErp.Infrastructure.Services.Containers, MiniErp.Tests.Inventory, MiniErp.Infrastructure.Services.BusinessPartners, MiniErp.Tests.Companies, MiniErp.Infrastructure, MiniErp.Tests.Authentication, MiniErp.Infrastructure.Services.Stores, MiniErp.Tests.BusinessPartners (+25 more)

### Community 24 - "MiniErp.Application.Common.Models"
Cohesion: 0.05
Nodes (19): MiniErp.Infrastructure.Services.ItemsCategories, MiniErp.Application.Features.Cashboxes, MiniErp.Application.Features.ItemUnits, MiniErp.Api.Extensions, MiniErp.Application.Features.InventoryCostReports, MiniErp.Application.Common.Models, MiniErp.Application.Features.InventoryCounts, MiniErp.Application.Features.Items (+11 more)

### Community 25 - "AuditableEntity"
Cohesion: 0.09
Nodes (24): DateTime, AuditableEntity, ICollection, CashMovementType, ICollection, Container, DateOnly, ContainerMovement (+16 more)

### Community 26 - "InventoryCountService"
Cohesion: 0.12
Nodes (12): InventoryCountFilterRequest, InventoryCountLineResponse, InventoryCountListResponse, InventoryCountResponse, CancellationToken, IEnumerable, int, IQueryable (+4 more)

### Community 27 - "InvoiceRequest"
Cohesion: 0.22
Nodes (10): int, InvoiceContainerLineRequest, InvoiceRequest, InvoiceUpdateRequest, IReadOnlyList, InvoiceContainerLineRequestValidator, InvoiceLineRequestValidator, InvoiceRequestValidator (+2 more)

### Community 28 - ".UpdateAsync"
Cohesion: 0.20
Nodes (12): CancellationToken, int, Task, InvoiceService, CancellationToken, IEnumerable, IReadOnlyCollection, List (+4 more)

### Community 29 - "MiniErp.Application.Features.Stores"
Cohesion: 0.07
Nodes (13): MiniErp.Application.Features.Stores, MiniErp.Infrastructure.Services.StoreContainers, MiniErp.Application.Features.BusinessPartners, MiniErp.Application.Features.Containers, MiniErp.Application.Features.StoreContainers, TypeAdapterConfig, BusinessPartnerMappingRegister, TypeAdapterConfig (+5 more)

### Community 30 - "StoreContainerService"
Cohesion: 0.26
Nodes (5): CancellationToken, int, IReadOnlyList, Task, StoreContainerService

### Community 31 - "Task"
Cohesion: 0.13
Nodes (13): DriverTestDatabase, DateOnly, DateTimeOffset, Fact, InlineData, SqliteConnection, Task, Theory (+5 more)

### Community 32 - "InventoryStockService"
Cohesion: 0.11
Nodes (22): CancellationToken, DateOnly, DateTime, IReadOnlyCollection, IReadOnlyDictionary, Task, IInventoryStockService, InventoryMovementReference (+14 more)

### Community 33 - "CashVoucherService"
Cohesion: 0.15
Nodes (11): int, CashVoucherRequest, CashVoucherUpdateRequest, CashVoucherResponse, CancellationToken, int, IQueryable, Task (+3 more)

### Community 34 - "Task"
Cohesion: 0.12
Nodes (11): CompanyTestDatabase, Fact, Guid, InlineData, SqliteConnection, Task, Theory, ValueTask (+3 more)

### Community 35 - "SelectResponse"
Cohesion: 0.12
Nodes (15): SelectResponse, CancellationToken, IReadOnlyList, Task, IItemService, ItemFilterRequest, ItemFilterRequestValidator, ItemRequest (+7 more)

### Community 36 - "AuditableEntityConfiguration"
Cohesion: 0.09
Nodes (25): Item, ICollection, ItemUnit, InventoryCountLine, ItemStoreBalance, StockAdjustmentLine, InvoiceLine, EntityTypeBuilder (+17 more)

### Community 37 - "UserService"
Cohesion: 0.20
Nodes (12): UserCompanyResponse, UserResponse, CancellationToken, Guid, HashSet, IdentityResult, IQueryable, IReadOnlyCollection (+4 more)

### Community 38 - "MiniErp.Application.Features.Users"
Cohesion: 0.10
Nodes (16): MiniErp.Application.Features.Users, MiniErp.Tests.Users, CancellationToken, Guid, IReadOnlyList, Task, IUserService, UserCompaniesRequest (+8 more)

### Community 39 - "Error"
Cohesion: 0.11
Nodes (6): Error, CancellationToken, Task, InvoiceService, PreparedInvoice, PaymentPreparation

### Community 40 - "MiniErp.Api.csproj"
Cohesion: 0.08
Nodes (26): Asp.Versioning.Mvc (10.0.0), Asp.Versioning.Mvc.ApiExplorer (10.0.0), Bogus (35.6.5), FluentValidation (12.1.1), FluentValidation.DependencyInjectionExtensions (12.1.1), Mapster (10.0.11), Microsoft.AspNetCore.Authentication.JwtBearer (10.0.9), Microsoft.AspNetCore.Identity.EntityFrameworkCore (10.0.10) (+18 more)

### Community 41 - "CurrencyCode"
Cohesion: 0.07
Nodes (28): BusinessPartner, DateOnly, BusinessPartnerMovement, DateOnly, PartnerOpeningBalance, DateOnly, ICollection, Cashbox (+20 more)

### Community 42 - ".TryParse"
Cohesion: 0.09
Nodes (19): MiniErp.Tests.Common, MiniErp.Application.Common.Parsing, MiniErp.Api.ModelBinding, CultureInfo, IModelBinder, IModelBinderProvider, ModelBinderProviderContext, ModelBindingContext (+11 more)

### Community 43 - "Task"
Cohesion: 0.16
Nodes (9): IsActive, IsDeleted, StoreContainerTestDatabase, Fact, SqliteConnection, Task, ValueTask, StoreContainerServiceTests (+1 more)

### Community 44 - "CompanyService"
Cohesion: 0.13
Nodes (15): CompanyFilterRequest, CompanyFilterRequestValidator, CompanyRequest, CompanyUpdateRequest, CompanyRequestValidator, CompanyUpdateRequestValidator, CompanyResponse, CancellationToken (+7 more)

### Community 45 - "ExchangeRatesController"
Cohesion: 0.07
Nodes (35): ProducesResponseType&lt;ExchangeRateImportResponse&gt;, ProducesResponseType&lt;ExchangeRateResolutionResponse&gt;, ProducesResponseType&lt;ExchangeRateResponse&gt;, ProducesResponseType&lt;PagedResponse&lt;ExchangeRateResponse&gt;&gt;, Authorize, CancellationToken, DateOnly, HttpDelete (+27 more)

### Community 46 - "Driver"
Cohesion: 0.24
Nodes (8): DateOnly, Driver, DateOnly, DriverTrip, EntityTypeBuilder, DriverConfiguration, EntityTypeBuilder, DriverTripConfiguration

### Community 47 - "MiniErp.Api"
Cohesion: 0.10
Nodes (29): ASP.NET Core Identity, Bogus, Clean Architecture, Company-Scoped Access Token, Company-Scoped Tenancy, Company Selection Token, Database Migrations, Entity Framework Core (+21 more)

### Community 48 - "ItemUnitService"
Cohesion: 0.13
Nodes (14): CancellationToken, IReadOnlyList, Task, IItemUnitService, ItemUnitFilterRequest, ItemUnitFilterRequestValidator, ItemUnitRequest, ItemUnitRequestValidator (+6 more)

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
Cohesion: 0.20
Nodes (7): ContainerTestDatabase, Fact, SqliteConnection, Task, ValueTask, ContainerServiceTests, ContainerTestDatabase

### Community 54 - "Task"
Cohesion: 0.18
Nodes (9): CountryTestDatabase, Fact, InlineData, SqliteConnection, Task, Theory, ValueTask, CountryServiceTests (+1 more)

### Community 55 - "ItemMovement"
Cohesion: 0.33
Nodes (5): DateOnly, ICollection, ItemMovement, EntityTypeBuilder, ItemMovementConfiguration

### Community 56 - ".GetAllAsync"
Cohesion: 0.14
Nodes (15): InvoiceFilterRequest, InvoiceContainerLineResponse, InvoiceItemBalanceResponse, InvoiceListResponse, InvoicePagedResponse, InvoiceSummaryResponse, CancellationToken, DateOnly (+7 more)

### Community 57 - "StoreService"
Cohesion: 0.12
Nodes (14): CancellationToken, IReadOnlyList, Task, IStoreService, StoreFilterRequest, StoreFilterRequestValidator, StoreRequest, StoreRequestValidator (+6 more)

### Community 58 - "Result"
Cohesion: 0.20
Nodes (8): Result, Result, CancellationToken, Task, ICashVoucherService, CancellationToken, Task, IInventoryCountService

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

### Community 63 - "Task"
Cohesion: 0.19
Nodes (9): BusinessPartnerIntegrityTestDatabase, Fact, InlineData, SqliteConnection, Task, Theory, ValueTask, BusinessPartnerIntegrityServiceTests (+1 more)

### Community 64 - ".PrepareAsync"
Cohesion: 0.20
Nodes (8): decimal, int, InvoiceAmountRules, CancellationToken, IReadOnlyList, PreparedInvoice, Task, InvoiceService

### Community 65 - "AuditableEntityInterceptor"
Cohesion: 0.16
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
Cohesion: 0.32
Nodes (4): BusinessPartnerContainerStoreTestDatabase, Fact, Task, BusinessPartnerContainerStoreServiceTests

### Community 71 - "StoresController"
Cohesion: 0.25
Nodes (12): ProducesResponseType&lt;PagedResponse&lt;StoreResponse&gt;&gt;, ProducesResponseType&lt;StoreResponse&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost, HttpPut (+4 more)

### Community 73 - "ProducesResponseType&lt;ProblemDetails&gt;"
Cohesion: 0.27
Nodes (12): ProducesResponseType&lt;CompanyResponse&gt;, ProducesResponseType&lt;PagedResponse&lt;CompanyResponse&gt;&gt;, ProducesResponseType&lt;ProblemDetails&gt;, CancellationToken, HttpDelete, HttpGet, HttpPost, HttpPut (+4 more)

### Community 74 - ".Create"
Cohesion: 0.26
Nodes (12): ProducesResponseType&lt;InventoryCountResponse&gt;, ProducesResponseType&lt;PagedResponse&lt;InventoryCountListResponse&gt;&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost, HttpPut (+4 more)

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
Cohesion: 0.16
Nodes (9): ExchangeRateResponse, DateTime, CancellationToken, DateOnly, DbUpdateException, int, IOrderedQueryable, Task (+1 more)

### Community 79 - ".GetRateAsync"
Cohesion: 0.18
Nodes (8): HttpStatusCode, CancellationToken, DateOnly, Task, FrankfurterExchangeRateProvider, FrankfurterRateResponse, string, FrankfurterOptions

### Community 80 - ".Create"
Cohesion: 0.24
Nodes (12): ProducesResponseType&lt;PagedResponse&lt;StockAdjustmentListResponse&gt;&gt;, ProducesResponseType&lt;StockAdjustmentResponse&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost, HttpPut (+4 more)

### Community 81 - ".ProcessInbound"
Cohesion: 0.24
Nodes (6): PendingOutbound, Queue, DateTime, int, InventoryCostRules, PendingOutbound

### Community 82 - "ContainerService"
Cohesion: 0.08
Nodes (26): ProducesResponseType&lt;ContainerResponse&gt;, ProducesResponseType&lt;PagedResponse&lt;ContainerResponse&gt;&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost, HttpPut (+18 more)

### Community 84 - ".GetCashboxStatement"
Cohesion: 0.33
Nodes (8): ProducesResponseType&lt;CashboxStatementResponse&gt;, ProducesResponseType&lt;DriverStatementResponse&gt;, ProducesResponseType&lt;PartnerStatementResponse&gt;, CancellationToken, HttpGet, IActionResult, Task, StatementsController

### Community 85 - ".Upsert"
Cohesion: 0.25
Nodes (11): ProducesResponseType&lt;IReadOnlyList&lt;StoreContainerResponse&gt;&gt;, ProducesResponseType&lt;PagedResponse&lt;StoreContainerResponse&gt;&gt;, ProducesResponseType&lt;StoreContainerResponse&gt;, ProducesResponseType&lt;StoreContainerWorkspaceResponse&gt;, Authorize, CancellationToken, HttpGet, HttpPut (+3 more)

### Community 86 - ".Create"
Cohesion: 0.25
Nodes (12): ProducesResponseType&lt;CountryResponse&gt;, ProducesResponseType&lt;PagedResponse&lt;CountryResponse&gt;&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost, HttpPut (+4 more)

### Community 87 - "EnumRequestOperationDocumentationFilter"
Cohesion: 0.20
Nodes (9): EnumProperty, HashSet, int, IReadOnlyList, OpenApiOperation, OperationFilterContext, Type, EnumProperty (+1 more)

### Community 88 - "PagedResponse"
Cohesion: 0.12
Nodes (14): Guid, ICurrentUserService, CancellationToken, IOrderedQueryable, Task, IPaginationService, IScopedService, PagedResponse (+6 more)

### Community 89 - "StockOpeningBalance"
Cohesion: 0.27
Nodes (7): DateOnly, ICollection, StockOpeningBalance, EntityTypeBuilder, StockOpeningBalanceConfiguration, IReadOnlyDictionary, ItemSnapshot

### Community 90 - "InvoicePaymentTermTests"
Cohesion: 0.27
Nodes (4): Fact, InlineData, Theory, InvoicePaymentTermTests

### Community 91 - "MiniErp.Api.Swagger"
Cohesion: 0.15
Nodes (8): MiniErp.Api.Swagger, OpenApiOperation, OperationFilterContext, InventoryCountsSwaggerDocumentation, IConfiguration, IServiceCollection, WebApplication, SwaggerExtensions

### Community 92 - ".Create"
Cohesion: 0.15
Nodes (7): OpenApiOperation, OperationFilterContext, CashMovementTypesSwaggerDocumentation, OpenApiOperation, OperationFilterContext, StoresSwaggerDocumentation, SwaggerOperationDescription

### Community 93 - "IOperationFilter"
Cohesion: 0.17
Nodes (8): IOperationFilter, OpenApiOperation, OperationFilterContext, DriversSwaggerDocumentation, OpenApiOperation, OperationFilterContext, string, UnifiedErrorResponseSwaggerFilter

### Community 94 - "InventoryCostReportService"
Cohesion: 0.17
Nodes (9): MovementProjection, InvoiceLineResponse, InventoryCostStatus, DateOnly, DateTime, int, AllocationProjection, InventoryCostReportService (+1 more)

### Community 95 - "ProducesResponseType&lt;IReadOnlyList&lt;SelectResponse&gt;&gt;"
Cohesion: 0.23
Nodes (13): ProducesResponseType&lt;DriverResponse&gt;, ProducesResponseType&lt;IReadOnlyList&lt;SelectResponse&gt;&gt;, ProducesResponseType&lt;PagedResponse&lt;DriverResponse&gt;&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost (+5 more)

### Community 96 - ".LoadMovementCostsAsync"
Cohesion: 0.15
Nodes (9): OpeningMovementCost, StockOpeningBalanceFilterRequest, StockOpeningBalanceFilterRequestValidator, StockOpeningBalanceLineResponse, StockOpeningBalanceListResponse, Dictionary, IReadOnlyList, ItemId (+1 more)

### Community 97 - ".Create"
Cohesion: 0.24
Nodes (12): ProducesResponseType&lt;PagedResponse&lt;StockOpeningBalanceListResponse&gt;&gt;, ProducesResponseType&lt;StockOpeningBalanceResponse&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost, HttpPut (+4 more)

### Community 98 - ".UpdateCosts"
Cohesion: 0.24
Nodes (9): ProducesResponseType&lt;DriverTripBulkCostUpdateResponse&gt;, ProducesResponseType&lt;PagedResponse&lt;DriverTripCostResponse&gt;&gt;, Authorize, CancellationToken, HttpGet, HttpPut, IActionResult, Task (+1 more)

### Community 99 - "InventoryCountRequest"
Cohesion: 0.21
Nodes (10): int, InventoryCountIncreaseCostRequest, InventoryCountLineUpdateRequest, InventoryCountReconcileRequest, InventoryCountRequest, InventoryCountUpdateRequest, InventoryCountLineUpdateRequestValidator, InventoryCountReconcileRequestValidator (+2 more)

### Community 100 - "DriverTripService"
Cohesion: 0.31
Nodes (4): CancellationToken, int, Task, DriverTripService

### Community 101 - "StatementResponses.cs"
Cohesion: 0.20
Nodes (9): CashboxStatementItemResponse, CashboxStatementResponse, CashboxStatementSummaryResponse, DriverStatementItemResponse, DriverStatementResponse, DriverStatementSummaryResponse, PartnerStatementItemResponse, PartnerStatementResponse (+1 more)

### Community 102 - ".IsValidRate"
Cohesion: 0.15
Nodes (6): int, ExchangeRateRules, Fact, InlineData, Theory, ExchangeRateRulesTests

### Community 103 - ".Apply"
Cohesion: 0.27
Nodes (6): IOpenApiSchema, ISchemaFilter, SchemaFilterContext, Type, EnumDocumentationFormatter, EnumSchemaDocumentationFilter

### Community 104 - "StockOpeningBalanceRequest"
Cohesion: 0.20
Nodes (10): CancellationToken, Task, IStockOpeningBalanceService, int, StockOpeningBalanceLineRequest, StockOpeningBalanceRequest, StockOpeningBalanceUpdateRequest, StockOpeningBalanceLineRequestValidator (+2 more)

### Community 105 - "JwtOptions"
Cohesion: 0.14
Nodes (11): ClaimsPrincipal, CompanyClaimResolver, IConfiguration, IServiceCollection, DependencyInjection, int, CurrentCompanyContext, string (+3 more)

### Community 106 - ".GetRateAsync"
Cohesion: 0.18
Nodes (9): ExchangeRateImportItemResponse, ExchangeRateImportItemStatus, ExchangeRateImportResponse, CancellationToken, DateOnly, Task, ExternalExchangeRate, IExchangeRateProvider (+1 more)

### Community 107 - "InventoryCount"
Cohesion: 0.32
Nodes (6): DateOnly, DateTime, ICollection, InventoryCount, EntityTypeBuilder, InventoryCountConfiguration

### Community 108 - "UserRequestValidatorTests"
Cohesion: 0.60
Nodes (3): Fact, Task, UserRequestValidatorTests

### Community 109 - ".GetAsync"
Cohesion: 0.18
Nodes (9): CancellationToken, Task, IInventoryCostReportService, InventoryCostReportFilterRequest, InventoryCostReportFilterRequestValidator, InventoryCostAllocationReportResponse, InventoryCostReportItemResponse, InventoryCostReportResponse (+1 more)

### Community 110 - "ArabicValidationConfiguration"
Cohesion: 0.29
Nodes (5): MiniErp.Application.Common.Validation, LanguageManager, IReadOnlyDictionary, ArabicLanguageManager, ArabicValidationConfiguration

### Community 111 - "ExchangeRateServiceTests"
Cohesion: 0.11
Nodes (19): DbConnection, DbTransaction, DbTransactionInterceptor, ExchangeRateRow, ExchangeRateTestDatabase, IsolationCaptureInterceptor, IsolationLevel, CancellationToken (+11 more)

### Community 112 - ".SendAsync"
Cohesion: 0.21
Nodes (9): HttpMessageHandler, HttpRequestMessage, HttpResponseMessage, CancellationToken, Fact, Task, FrankfurterExchangeRateProviderTests, StubHandler (+1 more)

### Community 113 - ".GetAllAsync"
Cohesion: 0.38
Nodes (6): CancellationToken, IReadOnlyList, Task, IStoreContainerService, StoreContainerResponse, StoreContainerWorkspaceResponse

### Community 114 - "Migration"
Cohesion: 0.50
Nodes (3): Migration, MigrationBuilder, InitialIdentity

### Community 116 - ".GetCostEntryAsync"
Cohesion: 0.21
Nodes (7): DriverTripCostFilterRequest, DriverTripCostFilterRequestValidator, DriverTripBulkCostUpdateResponse, DriverTripCostResponse, CancellationToken, Task, IDriverTripService

### Community 117 - ".ApplyPendingMigrationsAsync"
Cohesion: 0.33
Nodes (4): CancellationToken, Task, WebApplication, DatabaseMigrationExtensions

### Community 118 - ".GetSnapshotsAsync"
Cohesion: 0.31
Nodes (7): CancellationToken, DateOnly, IReadOnlyCollection, IReadOnlyDictionary, Task, IInventoryCostingService, InventoryCostSnapshot

### Community 119 - "MappingConfiguration"
Cohesion: 0.40
Nodes (3): bool, object, MappingConfiguration

### Community 120 - "CashManagementValidatorTests"
Cohesion: 0.23
Nodes (5): CashPartyType, Fact, InlineData, Theory, CashManagementValidatorTests

### Community 121 - "AllowAnonymousOperationFilter"
Cohesion: 0.40
Nodes (3): OpenApiOperation, OperationFilterContext, AllowAnonymousOperationFilter

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

### Community 131 - "InventoryCostReportsSwaggerDocumentation.cs"
Cohesion: 0.40
Nodes (3): OpenApiOperation, OperationFilterContext, InventoryCostReportsSwaggerDocumentation

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

### Community 178 - "MiniErp.Api.Errors"
Cohesion: 0.25
Nodes (3): MiniErp.Api.Errors, MiniErp.Api.Exceptions, MiniErp.Api.Validation

### Community 179 - "MiniErp.Application.Features.PartnerOpeningBalances"
Cohesion: 0.17
Nodes (4): MiniErp.Application.Features.PartnerOpeningBalances, MiniErp.Tests.PartnerOpeningBalances, TypeAdapterConfig, PartnerOpeningBalanceMappingRegister

### Community 180 - "DriverTripBulkCostUpdateRequest"
Cohesion: 0.33
Nodes (5): int, DriverTripBulkCostUpdateRequest, DriverTripCostUpdateItem, DriverTripBulkCostUpdateRequestValidator, DriverTripCostUpdateItemValidator

### Community 181 - "Invoice"
Cohesion: 0.11
Nodes (15): ICollection, ItemsCategory, DateOnly, DateTime, ICollection, Invoice, Country, InvoiceContentType (+7 more)

### Community 183 - "CashboxStatementFilterRequest"
Cohesion: 0.29
Nodes (6): CashboxStatementFilterRequest, DriverStatementFilterRequest, PartnerStatementFilterRequest, CashboxStatementFilterRequestValidator, DriverStatementFilterRequestValidator, PartnerStatementFilterRequestValidator

### Community 184 - "CompanyAndExchangeRateAuthorizationTests"
Cohesion: 0.25
Nodes (5): MiniErp.Tests.Authorization, Fact, InlineData, Theory, CompanyAndExchangeRateAuthorizationTests

### Community 185 - "StockOpeningBalanceLine"
Cohesion: 0.21
Nodes (6): decimal, int, StockOpeningBalanceAmountRules, StockOpeningBalanceLine, EntityTypeBuilder, StockOpeningBalanceLineConfiguration

### Community 186 - "IAsyncDisposable"
Cohesion: 0.29
Nodes (4): IAsyncDisposable, SqliteConnection, ValueTask, BusinessPartnerContainerStoreTestDatabase

### Community 187 - "MiniErp.Application.Features.Companies"
Cohesion: 0.20
Nodes (3): MiniErp.Application.Features.Companies, TypeAdapterConfig, CompanyMappingRegister

### Community 188 - "DriverStatementRaw"
Cohesion: 0.33
Nodes (7): DriverStatementSourceType, PartnerStatementSourceType, DateOnly, DateTime, CashboxStatementRaw, DriverStatementRaw, PartnerStatementRaw

### Community 189 - "StoreContainerUpsertRequest"
Cohesion: 0.40
Nodes (3): int, StoreContainerUpsertRequest, StoreContainerUpsertRequestValidator

### Community 190 - "AbstractValidator"
Cohesion: 0.06
Nodes (20): AbstractValidator, Expression, PaginationRequestValidator, BusinessPartnerFilterRequestValidator, CashVoucherFilterRequest, CashVoucherFilterRequestValidator, CashVoucherRequestValidator, CashVoucherUpdateRequestValidator (+12 more)

### Community 191 - "Q: Cross-project MiniErp feature flow impact analysis"
Cohesion: 0.40
Nodes (4): Answer, Outcome, Q: Cross-project MiniErp feature flow impact analysis, Source Nodes

### Community 193 - "PaginationRequest"
Cohesion: 0.39
Nodes (5): int, PaginationRequest, CancellationToken, Task, IFinancialStatementService

### Community 204 - ".Apply"
Cohesion: 0.40
Nodes (3): OpenApiOperation, OperationFilterContext, ItemUnitsSwaggerDocumentation

### Community 205 - ".Apply"
Cohesion: 0.40
Nodes (3): OpenApiOperation, OperationFilterContext, StoreContainersSwaggerDocumentation

### Community 212 - "StockAdjustmentsSwaggerDocumentation.cs"
Cohesion: 0.40
Nodes (3): OpenApiOperation, OperationFilterContext, StockAdjustmentsSwaggerDocumentation

## Knowledge Gaps
- **97 isolated node(s):** `Asp.Versioning.Mvc (10.0.0)`, `Asp.Versioning.Mvc.ApiExplorer (10.0.0)`, `FluentValidation.DependencyInjectionExtensions (12.1.1)`, `Microsoft.EntityFrameworkCore.Design (10.0.10)`, `Microsoft.OpenApi (2.11.0)` (+92 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **48 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Result` connect `Result` to `StockOpeningBalanceService`, `PartnerOpeningBalanceServiceTests`, `StockAdjustmentService`, `BusinessPartnerService`, `AuthenticationService`, `InvoiceResponse`, `CashMovementTypeService`, `ItemsCategoryService`, `CashboxService`, `ApplicationUser`, `CountryService`, `ApiErrorResponseFactory`, `DriverService`, `InventoryCountService`, `.UpdateAsync`, `StoreContainerService`, `CashVoucherService`, `Task`, `SelectResponse`, `UserService`, `MiniErp.Application.Features.Users`, `Error`, `CompanyService`, `ExchangeRatesController`, `ItemUnitService`, `.GetAllAsync`, `StoreService`, `FinancialStatementService`, `.PrepareAsync`, `PaginationRequest`, `ExchangeRateService`, `.GetRateAsync`, `ContainerService`, `PagedResponse`, `.LoadMovementCostsAsync`, `DriverTripService`, `StockOpeningBalanceRequest`, `.GetRateAsync`, `.GetAsync`, `.GetAllAsync`, `.GetCostEntryAsync`?**
  _High betweenness centrality (0.168) - this node is a cross-community bridge._
- **Why does `ApplicationDbContext` connect `ApplicationDbContext` to `InvoiceServiceTests`, `PartnerOpeningBalanceServiceTests`, `StockAdjustmentService`, `.CreateAsync`, `MiniErp.Domain.Entities.Companies`, `ApplicationUser`, `Task`, `Company`, `AuditableEntity`, `Task`, `Task`, `AuditableEntityConfiguration`, `CurrencyCode`, `Task`, `Driver`, `StockOpeningBalanceServiceTests`, `AccessTokenCompanyTestDatabase`, `.CreateAsync`, `Invoice`, `Task`, `ItemMovement`, `Task`, `StockOpeningBalanceLine`, `IAsyncDisposable`, `CategoryTestDatabase`, `InventoryCostAllocation`, `Task`, `.CreateAsync`, `StockOpeningBalance`, `InventoryCount`, `ExchangeRateServiceTests`?**
  _High betweenness centrality (0.119) - this node is a cross-community bridge._
- **Why does `Error` connect `Error` to `StockOpeningBalanceService`, `PartnerOpeningBalanceServiceTests`, `StockAdjustmentService`, `BusinessPartnerService`, `AuthenticationService`, `CashMovementTypeService`, `ItemsCategoryService`, `CashboxService`, `InventoryCostingService`, `CountryService`, `ApiErrorResponseFactory`, `DriverService`, `InventoryCountService`, `.UpdateAsync`, `StoreContainerService`, `InventoryStockService`, `CashVoucherService`, `SelectResponse`, `UserService`, `CompanyService`, `ItemUnitService`, `ItemMovement`, `StoreService`, `Result`, `FinancialStatementService`, `.PrepareAsync`, `InvoiceLineRequest`, `ExchangeRateService`, `.GetRateAsync`, `ContainerService`, `DriverTripService`, `.GetSnapshotsAsync`?**
  _High betweenness centrality (0.093) - this node is a cross-community bridge._
- **What connects `Asp.Versioning.Mvc (10.0.0)`, `Asp.Versioning.Mvc.ApiExplorer (10.0.0)`, `FluentValidation.DependencyInjectionExtensions (12.1.1)` to the rest of the system?**
  _97 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `InvoiceServiceTests` be split into smaller, more focused modules?**
  _Cohesion score 0.09241971620612398 - nodes in this community are weakly interconnected._
- **Should `PartnerOpeningBalanceServiceTests` be split into smaller, more focused modules?**
  _Cohesion score 0.054431960049937576 - nodes in this community are weakly interconnected._
- **Should `StockAdjustmentService` be split into smaller, more focused modules?**
  _Cohesion score 0.060144346431435444 - nodes in this community are weakly interconnected._