# Graph Report - MiniErp  (2026-08-01)

## Corpus Check
- 519 files · ~143,161 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 4231 nodes · 11450 edges · 211 communities (150 shown, 61 thin omitted)
- Extraction: 98% EXTRACTED · 2% INFERRED · 0% AMBIGUOUS · INFERRED: 218 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `cded24c9`
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
- .Create
- Task
- ApiErrorResponseFactory
- DriverService
- MiniErp.Domain.Enums
- Company
- MiniErp.Application.Common.Abstractions
- MiniErp.Application.Common.Models
- MiniErp.Infrastructure.Persistence.Configurations
- .Validation
- .NotFound
- .SaveSideEffectsAsync
- MiniErp.Application.Common.Results
- StoreContainerErrors
- Task
- InventoryStockService
- .PrepareAsync
- Task
- Result
- AuditableEntity
- ExchangeRateServiceTests
- .Failure
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
- IAsyncDisposable
- ArabicIdentityErrorDescriber
- Task
- MiniErp.Application.Features.ExchangeRates
- MiniErp.Application.Features.Authentication
- .UpdateAsync
- SelectResponse
- .ResolveInboundUnitCostAsync
- CategoryTestDatabase
- ApplicationDbContext
- PaginationRequest
- RefreshToken
- .IsValidRate
- .ValidateStockAsync
- AuditableEntityInterceptor
- .Create
- .Create
- StockAdjustmentErrors
- ICurrentCompanyContext
- .CreateAsync
- ProducesResponseType&lt;ProblemDetails&gt;
- .Login
- .GetAll
- InventoryCountService
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
- PagedResponse
- StockOpeningBalanceLine
- ItemMovement
- MiniErp.Api.Swagger
- .Create
- IOperationFilter
- .Create
- ProducesResponseType&lt;IReadOnlyList&lt;SelectResponse&gt;&gt;
- IScopedService
- CashVoucherRequest
- .UpdateCosts
- CurrentCompanyContext
- .UpdateCostsAsync
- StatementResponses.cs
- CompanyErrors
- .Apply
- CashMasterServiceTests
- JwtOptions
- CashboxMappingRegister
- InventoryCount
- StockOpeningBalanceErrors
- .GetAsync
- ArabicValidationConfiguration
- CashMovementTypeMappingRegister
- FrankfurterExchangeRateProviderTests
- CashVoucherMappingRegister
- Migration
- AddTablesItemAndItemUnit
- .GetCostEntryAsync
- .ApplyPendingMigrationsAsync
- InventoryCountMappingRegister
- MappingConfiguration
- ItemsCategoryMappingRegister
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
- StockAdjustmentMappingRegister
- .Apply
- .Apply
- .Apply
- InvoiceResponses.cs
- .Apply
- StatementsSwaggerDocumentation.cs
- StockOpeningBalanceMappingRegister
- .Apply
- CashManagementValidatorTests
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
- .CreateStandardClaims
- PartnerOpeningBalanceMappingRegister
- MiniErp.Application.Features.PartnerOpeningBalances
- Invoice
- ContainerMappingRegister
- CompanyAndExchangeRateAuthorizationTests
- .GetAllAsync
- MiniErp.Application.Features.Invoices
- CompanyMappingRegister
- DriverTripCostServiceTests
- MiniErp.Application.Features.StockOpeningBalances
- AbstractValidator
- Q: Cross-project MiniErp feature flow impact analysis
- AddCompanyRowVersion
- DriverTripCostMappingRegister
- ItemUnitMappingRegister
- StoreMappingRegister
- .LoginAsync
- AddExchangeRateProvider
- CashMovementType
- InventoryCostReportsSwaggerDocumentation.cs
- InventoryQuantityRules.cs
- .Apply
- MiniErp.Application
- .GetRateAsync
- .Apply
- .ResolveAsync
- UserMappingRegister.cs
- CustomClaimTypes.cs
- ItemMappingRegister
- MappingConfigurationTests
- StockAdjustmentsSwaggerDocumentation.cs

## God Nodes (most connected - your core abstractions)
1. `Result` - 295 edges
2. `InvoiceServiceTests` - 137 edges
3. `MiniErp.Domain.Enums` - 120 edges
4. `ApplicationDbContext` - 120 edges
5. `MiniErp.Application.Common.Results` - 100 edges
6. `MiniErp.Application.Common.Models` - 90 edges
7. `PaginationRequest` - 87 edges
8. `InvoiceErrors` - 68 edges
9. `MiniErp.Application.Common.Abstractions` - 57 edges
10. `MiniErp.Domain.Entities.Companies` - 55 edges

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

## Communities (211 total, 61 thin omitted)

### Community 0 - "InvoiceServiceTests"
Cohesion: 0.05
Nodes (21): InvoiceTestDatabase, InvoicePriceStatus, InvoiceLineRequest, InvoiceType, PaymentTerm, InvoiceService, PreparedInvoice, Fact (+13 more)

### Community 1 - "StockOpeningBalanceService"
Cohesion: 0.07
Nodes (39): OpeningMovementCost, CancellationToken, Task, IStockOpeningBalanceService, StockOpeningBalanceFilterRequest, int, StockOpeningBalanceLineRequest, StockOpeningBalanceRequest (+31 more)

### Community 2 - "PartnerOpeningBalanceServiceTests"
Cohesion: 0.06
Nodes (40): PartnerOpeningBalanceTestDatabase, ProducesResponseType&lt;PagedResponse&lt;PartnerOpeningBalanceResponse&gt;&gt;, ProducesResponseType&lt;PartnerOpeningBalanceResponse&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost (+32 more)

### Community 3 - "StockAdjustmentService"
Cohesion: 0.06
Nodes (49): MovementCostSnapshot, ProducesResponseType&lt;PagedResponse&lt;StockAdjustmentListResponse&gt;&gt;, ProducesResponseType&lt;StockAdjustmentResponse&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost (+41 more)

### Community 4 - ".CreateAsync"
Cohesion: 0.29
Nodes (5): Fact, InlineData, Task, Theory, CashVoucherServiceTests

### Community 6 - "BusinessPartnerService"
Cohesion: 0.07
Nodes (30): BusinessPartnerIntegrityTestDatabase, BusinessPartnerContainerStoreResponse, BusinessPartnerFilterRequest, BusinessPartnerFilterRequestValidator, BusinessPartnerRequest, BusinessPartnerRequestValidator, IReadOnlyList, BusinessPartnerResponse (+22 more)

### Community 7 - "AuthenticationService"
Cohesion: 0.25
Nodes (7): CompanySelectionTokenData, TokenResponse, CancellationToken, Task, AuthenticationService, CompanySelectionTokenData, TokenValidationParameters

### Community 8 - "IRegister"
Cohesion: 0.15
Nodes (9): IRegister, TypeAdapterConfig, BusinessPartnerMappingRegister, TypeAdapterConfig, DriverMappingRegister, TypeAdapterConfig, ExchangeRateMappingRegister, TypeAdapterConfig (+1 more)

### Community 9 - ".CreateAsync"
Cohesion: 0.11
Nodes (17): DateOnly, Fact, Task, InventoryCostingServiceTests, DateOnly, Fact, Task, InventoryCostReportServiceTests (+9 more)

### Community 10 - "CashMovementTypeService"
Cohesion: 0.08
Nodes (34): ProducesResponseType&lt;CashMovementTypeResponse&gt;, ProducesResponseType&lt;IReadOnlyList&lt;CashMovementTypeSelectResponse&gt;&gt;, ProducesResponseType&lt;PagedResponse&lt;CashMovementTypeResponse&gt;&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost (+26 more)

### Community 11 - "ItemsCategoryService"
Cohesion: 0.08
Nodes (32): ProducesResponseType&lt;IReadOnlyList&lt;ItemsCategorySelectResponse&gt;&gt;, ProducesResponseType&lt;ItemsCategoryResponse&gt;, ProducesResponseType&lt;PagedResponse&lt;ItemsCategoryResponse&gt;&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost (+24 more)

### Community 12 - "CashboxService"
Cohesion: 0.08
Nodes (32): ProducesResponseType&lt;CashboxResponse&gt;, ProducesResponseType&lt;IReadOnlyList&lt;CashboxSelectResponse&gt;&gt;, ProducesResponseType&lt;PagedResponse&lt;CashboxResponse&gt;&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost (+24 more)

### Community 13 - "MiniErp.Domain.Entities.Companies"
Cohesion: 0.11
Nodes (13): MiniErp.Infrastructure.Seeding, MiniErp.Domain.Entities.BusinessPartners, MiniErp.Domain.Entities.Catalog, MiniErp.Domain.Entities.Companies, MiniErp.Domain.Entities.Logistics, MiniErp.Domain.Common.Entities, MiniErp.Domain.Entities.Containers, MiniErp.Domain.Entities.CashManagement (+5 more)

### Community 14 - "InventoryCostingService"
Cohesion: 0.10
Nodes (22): PendingOutbound, Queue, CancellationToken, DateOnly, IReadOnlyCollection, IReadOnlyDictionary, Task, IInventoryCostingService (+14 more)

### Community 15 - "ApplicationUser"
Cohesion: 0.09
Nodes (25): IdentityUser, Guid, ICollection, ApplicationUser, AsyncServiceScope, Fact, Guid, IConfiguration (+17 more)

### Community 16 - "Task"
Cohesion: 0.10
Nodes (10): InventoryDeletionDatabase, Fact, MemberData, SqliteConnection, Task, Theory, TheoryData, ValueTask (+2 more)

### Community 17 - ".Create"
Cohesion: 0.25
Nodes (12): ProducesResponseType&lt;ContainerResponse&gt;, ProducesResponseType&lt;PagedResponse&lt;ContainerResponse&gt;&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost, HttpPut (+4 more)

### Community 18 - "Task"
Cohesion: 0.07
Nodes (25): CountryTestDatabase, MiniErp.Application.Features.Countries, CountryFilterRequest, CountryFilterRequestValidator, TypeAdapterConfig, CountryMappingRegister, CountryRequest, CountryRequestValidator (+17 more)

### Community 19 - "ApiErrorResponseFactory"
Cohesion: 0.07
Nodes (30): ActionExecutingContext, Exception, IDictionary, IExceptionHandler, IFluentValidationAutoValidationResultFactory, IValidationContext, KeyValuePair, ModelStateDictionary (+22 more)

### Community 20 - "DriverService"
Cohesion: 0.13
Nodes (14): DriverFilterRequest, DriverFilterRequestValidator, DriverRequest, DriverRequestValidator, DriverResponse, CancellationToken, IReadOnlyList, Task (+6 more)

### Community 21 - "MiniErp.Domain.Enums"
Cohesion: 0.05
Nodes (12): MiniErp.Tests.Inventory, MiniErp.Infrastructure.Services.InventoryCounts, MiniErp.Infrastructure.Services.StockAdjustments, MiniErp.Application.Features.Cashboxes, MiniErp.Application.Features.InventoryCounts, MiniErp.Tests.CashManagement, MiniErp.Application.Features.CashVouchers, MiniErp.Application.Features.DriverTrips (+4 more)

### Community 22 - "Company"
Cohesion: 0.10
Nodes (29): IServiceProvider, SeedBusinessPartner, SeedCompany, SeedContainer, SeedCountry, SeedDriver, SeedStore, SeedUser (+21 more)

### Community 23 - "MiniErp.Application.Common.Abstractions"
Cohesion: 0.09
Nodes (29): MiniErp.Infrastructure.Services.Containers, MiniErp.Infrastructure.Services.BusinessPartners, MiniErp.Infrastructure.Services.CashMovementTypes, MiniErp.Tests.Companies, MiniErp.Infrastructure, MiniErp.Tests.Authentication, MiniErp.Infrastructure.Services.Stores, MiniErp.Tests.BusinessPartners (+21 more)

### Community 24 - "MiniErp.Application.Common.Models"
Cohesion: 0.09
Nodes (10): MiniErp.Infrastructure.Services.ItemsCategories, MiniErp.Api.Extensions, MiniErp.Application.Features.InventoryCostReports, MiniErp.Application.Common.Models, MiniErp.Application.Features.Drivers, MiniErp.Application.Features.Statements, MiniErp.Api.Controllers, MiniErp.Infrastructure.Services.Drivers (+2 more)

### Community 25 - "MiniErp.Infrastructure.Persistence.Configurations"
Cohesion: 0.07
Nodes (26): MiniErp.Infrastructure.Persistence.Configurations, ICollection, Container, DateOnly, ContainerMovement, StoreContainer, InvoiceContainerLine, Country (+18 more)

### Community 26 - ".Validation"
Cohesion: 0.06
Nodes (10): DateOnly, ExchangeRateErrors, IEnumerable, InvoiceCalculationErrorKind, InvoiceContainerStoreRequirement, InvoiceErrors, InvoiceFilterErrorKind, DateOnly (+2 more)

### Community 27 - ".NotFound"
Cohesion: 0.06
Nodes (4): CashVoucherErrors, InventoryCostReportErrors, StatementErrors, Guid

### Community 28 - ".SaveSideEffectsAsync"
Cohesion: 0.13
Nodes (14): Credit, Debit, InvoiceMovementRules, BusinessPartnerMovementType, ItemMovementType, CancellationToken, List, PaymentPreparation (+6 more)

### Community 29 - "MiniErp.Application.Common.Results"
Cohesion: 0.04
Nodes (21): MiniErp.Application.Features.Stores, MiniErp.Infrastructure.Services.StoreContainers, MiniErp.Api.Errors, MiniErp.Application.Features.ItemUnits, MiniErp.Application.Features.BusinessPartners, MiniErp.Application.Features.Companies, MiniErp.Application.Features.Items, MiniErp.Application.Features.Containers (+13 more)

### Community 30 - "StoreContainerErrors"
Cohesion: 0.15
Nodes (7): IEnumerable, StoreContainerErrors, CancellationToken, int, IReadOnlyList, Task, StoreContainerService

### Community 31 - "Task"
Cohesion: 0.13
Nodes (13): DriverTestDatabase, DateOnly, DateTimeOffset, Fact, InlineData, SqliteConnection, Task, Theory (+5 more)

### Community 32 - "InventoryStockService"
Cohesion: 0.12
Nodes (22): CancellationToken, DateOnly, DateTime, IReadOnlyCollection, IReadOnlyDictionary, Task, IInventoryStockService, InventoryMovementReference (+14 more)

### Community 33 - ".PrepareAsync"
Cohesion: 0.29
Nodes (8): CashVoucherResponse, CancellationToken, int, IQueryable, Task, CashVoucherService, VoucherPreparation, VoucherPreparation

### Community 34 - "Task"
Cohesion: 0.11
Nodes (13): CompanyTestDatabase, Guid, ICurrentUserService, Fact, Guid, InlineData, SqliteConnection, Task (+5 more)

### Community 35 - "Result"
Cohesion: 0.12
Nodes (16): Result, Result, CancellationToken, IReadOnlyList, Task, IItemService, ItemFilterRequest, ItemFilterRequestValidator (+8 more)

### Community 36 - "AuditableEntity"
Cohesion: 0.08
Nodes (21): DateTime, AuditableEntity, Item, ICollection, ItemUnit, InventoryCountLine, ItemStoreBalance, StockAdjustmentLine (+13 more)

### Community 37 - "ExchangeRateServiceTests"
Cohesion: 0.11
Nodes (19): DbConnection, DbTransaction, DbTransactionInterceptor, ExchangeRateRow, ExchangeRateTestDatabase, IsolationCaptureInterceptor, IsolationLevel, CancellationToken (+11 more)

### Community 38 - ".Failure"
Cohesion: 0.05
Nodes (44): MiniErp.Application.Features.Users, MiniErp.Tests.Users, ProducesResponseType&lt;IReadOnlyList&lt;string&gt;&gt;, ProducesResponseType&lt;PagedResponse&lt;UserResponse&gt;&gt;, ProducesResponseType&lt;UserResponse&gt;, CancellationToken, Guid, HttpDelete (+36 more)

### Community 39 - "Error"
Cohesion: 0.03
Nodes (16): Error, BusinessPartnerErrors, CashboxErrors, CashMovementTypeErrors, ContainerErrors, CountryErrors, DriverErrors, IEnumerable (+8 more)

### Community 40 - "MiniErp.Api.csproj"
Cohesion: 0.08
Nodes (26): Asp.Versioning.Mvc (10.0.0), Asp.Versioning.Mvc.ApiExplorer (10.0.0), Bogus (35.6.5), FluentValidation (12.1.1), FluentValidation.DependencyInjectionExtensions (12.1.1), Mapster (10.0.11), Microsoft.AspNetCore.Authentication.JwtBearer (10.0.9), Microsoft.AspNetCore.Identity.EntityFrameworkCore (10.0.10) (+18 more)

### Community 41 - "CurrencyCode"
Cohesion: 0.06
Nodes (29): BusinessPartner, DateOnly, BusinessPartnerMovement, DateOnly, PartnerOpeningBalance, ICollection, Cashbox, DateOnly (+21 more)

### Community 42 - ".TryParse"
Cohesion: 0.09
Nodes (19): MiniErp.Tests.Common, MiniErp.Application.Common.Parsing, MiniErp.Api.ModelBinding, CultureInfo, IModelBinder, IModelBinderProvider, ModelBinderProviderContext, ModelBindingContext (+11 more)

### Community 43 - "Task"
Cohesion: 0.16
Nodes (9): IsActive, IsDeleted, StoreContainerTestDatabase, Fact, SqliteConnection, Task, ValueTask, StoreContainerServiceTests (+1 more)

### Community 44 - "CompanyService"
Cohesion: 0.14
Nodes (15): CompanyFilterRequest, CompanyFilterRequestValidator, CompanyRequest, CompanyUpdateRequest, CompanyRequestValidator, CompanyUpdateRequestValidator, CompanyResponse, CancellationToken (+7 more)

### Community 45 - "ExchangeRatesController"
Cohesion: 0.20
Nodes (16): ProducesResponseType&lt;ExchangeRateImportPreviewResponse&gt;, ProducesResponseType&lt;ExchangeRateImportResponse&gt;, ProducesResponseType&lt;ExchangeRateResolutionResponse&gt;, ProducesResponseType&lt;ExchangeRateResponse&gt;, ProducesResponseType&lt;PagedResponse&lt;ExchangeRateResponse&gt;&gt;, Authorize, CancellationToken, DateOnly (+8 more)

### Community 46 - "Driver"
Cohesion: 0.20
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

### Community 51 - "IAsyncDisposable"
Cohesion: 0.17
Nodes (11): IAsyncDisposable, StoreTestDatabase, Fact, MemberData, SqliteConnection, Task, Theory, TheoryData (+3 more)

### Community 52 - "ArabicIdentityErrorDescriber"
Cohesion: 0.20
Nodes (3): IdentityError, IdentityErrorDescriber, ArabicIdentityErrorDescriber

### Community 53 - "Task"
Cohesion: 0.19
Nodes (7): ContainerTestDatabase, Fact, SqliteConnection, Task, ValueTask, ContainerServiceTests, ContainerTestDatabase

### Community 54 - "MiniErp.Application.Features.ExchangeRates"
Cohesion: 0.09
Nodes (10): MiniErp.Infrastructure.Services.ExchangeRates, MiniErp.Tests.ExchangeRates, MiniErp.Tests, MiniErp.Application.Features.ExchangeRates, ExchangeRateImportPreviewItemResponse, IExchangeRateProvider, FrankfurterExchangeRateProvider, FrankfurterRateResponse (+2 more)

### Community 55 - "MiniErp.Application.Features.Authentication"
Cohesion: 0.17
Nodes (8): MiniErp.Application.Features.Authentication, CancellationToken, Task, IAuthenticationService, RefreshTokenRequest, RefreshTokenRequestValidator, SelectCompanyRequest, SelectCompanyRequestValidator

### Community 56 - ".UpdateAsync"
Cohesion: 0.12
Nodes (18): InvoiceFilterRequest, InvoiceResponse, CancellationToken, int, Task, PreparedInvoice, InvoiceService, InvoiceService (+10 more)

### Community 57 - "SelectResponse"
Cohesion: 0.12
Nodes (15): SelectResponse, CancellationToken, IReadOnlyList, Task, IStoreService, StoreFilterRequest, StoreFilterRequestValidator, StoreRequest (+7 more)

### Community 58 - ".ResolveInboundUnitCostAsync"
Cohesion: 0.15
Nodes (5): InboundCostResult, DateOnly, string, InventoryErrors, InboundCostResult

### Community 59 - "CategoryTestDatabase"
Cohesion: 0.19
Nodes (9): CategoryTestDatabase, Fact, InlineData, SqliteConnection, Task, Theory, ValueTask, CategoryTestDatabase (+1 more)

### Community 60 - "ApplicationDbContext"
Cohesion: 0.13
Nodes (12): DbContextOptions, DbSet, IdentityDbContext, ModelBuilder, Guid, IdentityRole, ApplicationDbContext, SqliteConnection (+4 more)

### Community 61 - "PaginationRequest"
Cohesion: 0.12
Nodes (17): DriverStatementRaw, PartnerStatementRaw, int, PaginationRequest, PaginationRequestValidator, DriverStatementSourceType, PartnerStatementSourceType, CancellationToken (+9 more)

### Community 62 - "RefreshToken"
Cohesion: 0.12
Nodes (13): IEntityTypeConfiguration, CompanySettings, DateTimeOffset, Guid, RefreshToken, Guid, UserCompany, EntityTypeBuilder (+5 more)

### Community 63 - ".IsValidRate"
Cohesion: 0.14
Nodes (7): DateOnly, int, ExchangeRateRules, Fact, InlineData, Theory, ExchangeRateRulesTests

### Community 64 - ".ValidateStockAsync"
Cohesion: 0.15
Nodes (7): decimal, int, InvoiceAmountRules, CancellationToken, IReadOnlyList, Task, InvoiceService

### Community 65 - "AuditableEntityInterceptor"
Cohesion: 0.18
Nodes (10): DbContext, DbContextEventData, EntityEntry, InterceptionResult, SaveChangesInterceptor, CancellationToken, DateTime, string (+2 more)

### Community 66 - ".Create"
Cohesion: 0.24
Nodes (12): ProducesResponseType&lt;CashVoucherResponse&gt;, ProducesResponseType&lt;PagedResponse&lt;CashVoucherResponse&gt;&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost, HttpPut (+4 more)

### Community 67 - ".Create"
Cohesion: 0.21
Nodes (14): ProducesResponseType&lt;InvoiceItemBalanceResponse&gt;, ProducesResponseType&lt;InvoicePagedResponse&gt;, ProducesResponseType&lt;InvoiceResponse&gt;, Authorize, CancellationToken, DateOnly, HttpDelete, HttpGet (+6 more)

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

### Community 74 - "InventoryCountService"
Cohesion: 0.07
Nodes (36): ProducesResponseType&lt;InventoryCountResponse&gt;, ProducesResponseType&lt;PagedResponse&lt;InventoryCountListResponse&gt;&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost, HttpPut (+28 more)

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
Cohesion: 0.09
Nodes (25): ExchangeRateFilterRequest, ExchangeRateImportPreviewResponse, ExchangeRateImportRequest, ExchangeRateImportItemResponse, ExchangeRateImportItemStatus, ExchangeRateImportResponse, ExchangeRateRequest, ExchangeRateUpdateRequest (+17 more)

### Community 79 - ".GetRateAsync"
Cohesion: 0.21
Nodes (6): HttpStatusCode, DateOnly, ExternalExchangeRateErrors, CancellationToken, DateOnly, Task

### Community 80 - ".Create"
Cohesion: 0.25
Nodes (12): ProducesResponseType&lt;DriverResponse&gt;, ProducesResponseType&lt;PagedResponse&lt;DriverResponse&gt;&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost, HttpPut (+4 more)

### Community 81 - "CashVoucherUpdateRequest"
Cohesion: 0.19
Nodes (9): Expression, CashVoucherFilterRequest, CashVoucherFilterRequestValidator, CashVoucherUpdateRequest, CashVoucherUpdateRequestValidator, CashVoucherValidationRules, CancellationToken, Task (+1 more)

### Community 82 - "ContainerService"
Cohesion: 0.13
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

### Community 88 - "PagedResponse"
Cohesion: 0.21
Nodes (9): CancellationToken, IOrderedQueryable, Task, IPaginationService, PagedResponse, CancellationToken, IOrderedQueryable, Task (+1 more)

### Community 89 - "StockOpeningBalanceLine"
Cohesion: 0.19
Nodes (6): decimal, int, StockOpeningBalanceAmountRules, StockOpeningBalanceLine, EntityTypeBuilder, StockOpeningBalanceLineConfiguration

### Community 90 - "ItemMovement"
Cohesion: 0.14
Nodes (12): InventoryCostAllocation, DateOnly, ICollection, ItemMovement, ICollection, Store, EntityTypeBuilder, InventoryCostAllocationConfiguration (+4 more)

### Community 91 - "MiniErp.Api.Swagger"
Cohesion: 0.15
Nodes (8): MiniErp.Api.Swagger, OpenApiOperation, OperationFilterContext, InventoryCountsSwaggerDocumentation, IConfiguration, IServiceCollection, WebApplication, SwaggerExtensions

### Community 92 - ".Create"
Cohesion: 0.15
Nodes (7): OpenApiOperation, OperationFilterContext, CashMovementTypesSwaggerDocumentation, OpenApiOperation, OperationFilterContext, StoresSwaggerDocumentation, SwaggerOperationDescription

### Community 93 - "IOperationFilter"
Cohesion: 0.17
Nodes (8): IOperationFilter, OpenApiOperation, OperationFilterContext, DriversSwaggerDocumentation, OpenApiOperation, OperationFilterContext, string, UnifiedErrorResponseSwaggerFilter

### Community 94 - ".Create"
Cohesion: 0.24
Nodes (12): ProducesResponseType&lt;PagedResponse&lt;StockOpeningBalanceListResponse&gt;&gt;, ProducesResponseType&lt;StockOpeningBalanceResponse&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost, HttpPut (+4 more)

### Community 95 - "ProducesResponseType&lt;IReadOnlyList&lt;SelectResponse&gt;&gt;"
Cohesion: 0.21
Nodes (14): ProducesResponseType&lt;BusinessPartnerContainerStoreResponse&gt;, ProducesResponseType&lt;BusinessPartnerResponse&gt;, ProducesResponseType&lt;IReadOnlyList&lt;SelectResponse&gt;&gt;, ProducesResponseType&lt;PagedResponse&lt;BusinessPartnerResponse&gt;&gt;, Authorize, CancellationToken, HttpDelete, HttpGet (+6 more)

### Community 96 - "IScopedService"
Cohesion: 0.17
Nodes (4): IScopedService, AuthenticationErrors, Guid, CurrentUserService

### Community 97 - "CashVoucherRequest"
Cohesion: 0.26
Nodes (8): int, CashVoucherRequest, CashVoucherRequestValidator, CashDirection, DateOnly, Fact, Task, FinancialStatementServiceTests

### Community 98 - ".UpdateCosts"
Cohesion: 0.14
Nodes (14): ProducesResponseType&lt;DriverTripBulkCostUpdateResponse&gt;, ProducesResponseType&lt;PagedResponse&lt;DriverTripCostResponse&gt;&gt;, Authorize, CancellationToken, HttpGet, HttpPut, IActionResult, Task (+6 more)

### Community 99 - "CurrentCompanyContext"
Cohesion: 0.29
Nodes (4): ClaimsPrincipal, CompanyClaimResolver, int, CurrentCompanyContext

### Community 101 - "StatementResponses.cs"
Cohesion: 0.17
Nodes (12): CancellationToken, Task, IFinancialStatementService, CashboxStatementItemResponse, CashboxStatementResponse, CashboxStatementSummaryResponse, DriverStatementItemResponse, DriverStatementResponse (+4 more)

### Community 102 - "CompanyErrors"
Cohesion: 0.16
Nodes (3): CompanyErrors, Fact, ErrorCatalogTests

### Community 103 - ".Apply"
Cohesion: 0.27
Nodes (6): IOpenApiSchema, ISchemaFilter, SchemaFilterContext, Type, EnumDocumentationFormatter, EnumSchemaDocumentationFilter

### Community 104 - "CashMasterServiceTests"
Cohesion: 0.25
Nodes (6): PartnerAccountEffect, Fact, InlineData, Task, Theory, CashMasterServiceTests

### Community 105 - "JwtOptions"
Cohesion: 0.27
Nodes (7): IConfiguration, IServiceCollection, DependencyInjection, string, JwtOptions, JwtTokenOptions, RefreshTokenOptions

### Community 107 - "InventoryCount"
Cohesion: 0.28
Nodes (6): DateOnly, DateTime, ICollection, InventoryCount, EntityTypeBuilder, InventoryCountConfiguration

### Community 109 - ".GetAsync"
Cohesion: 0.08
Nodes (21): MovementProjection, PaginationErrors, CancellationToken, Task, IInventoryCostReportService, InventoryCostReportFilterRequest, InventoryCostReportFilterRequestValidator, InventoryCostAllocationReportResponse (+13 more)

### Community 110 - "ArabicValidationConfiguration"
Cohesion: 0.29
Nodes (5): MiniErp.Application.Common.Validation, LanguageManager, IReadOnlyDictionary, ArabicLanguageManager, ArabicValidationConfiguration

### Community 112 - "FrankfurterExchangeRateProviderTests"
Cohesion: 0.22
Nodes (9): HttpMessageHandler, HttpRequestMessage, HttpResponseMessage, CancellationToken, Fact, Task, FrankfurterExchangeRateProviderTests, StubHandler (+1 more)

### Community 114 - "Migration"
Cohesion: 0.50
Nodes (3): Migration, MigrationBuilder, InitialIdentity

### Community 116 - ".GetCostEntryAsync"
Cohesion: 0.14
Nodes (11): DriverTripCostFilterRequest, DriverTripCostFilterRequestValidator, DriverTripBulkCostUpdateResponse, DriverTripCostResponse, CancellationToken, Task, IDriverTripService, CancellationToken (+3 more)

### Community 117 - ".ApplyPendingMigrationsAsync"
Cohesion: 0.33
Nodes (4): CancellationToken, Task, WebApplication, DatabaseMigrationExtensions

### Community 119 - "MappingConfiguration"
Cohesion: 0.40
Nodes (3): bool, object, MappingConfiguration

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

### Community 132 - ".Apply"
Cohesion: 0.40
Nodes (3): OpenApiOperation, OperationFilterContext, InvoicesSwaggerDocumentation

### Community 133 - ".Apply"
Cohesion: 0.40
Nodes (3): OpenApiOperation, OperationFilterContext, ItemsCategoriesSwaggerDocumentation

### Community 134 - ".Apply"
Cohesion: 0.40
Nodes (3): OpenApiOperation, OperationFilterContext, ItemsSwaggerDocumentation

### Community 135 - "InvoiceResponses.cs"
Cohesion: 0.22
Nodes (9): CancellationToken, DateOnly, Task, IInvoiceService, InvoiceContainerLineResponse, InvoiceItemBalanceResponse, InvoiceListResponse, InvoicePagedResponse (+1 more)

### Community 136 - ".Apply"
Cohesion: 0.40
Nodes (3): OpenApiOperation, OperationFilterContext, PartnerOpeningBalancesSwaggerDocumentation

### Community 137 - "StatementsSwaggerDocumentation.cs"
Cohesion: 0.40
Nodes (3): OpenApiOperation, OperationFilterContext, StatementsSwaggerDocumentation

### Community 139 - ".Apply"
Cohesion: 0.40
Nodes (3): OpenApiOperation, OperationFilterContext, StockOpeningBalancesSwaggerDocumentation

### Community 140 - "CashManagementValidatorTests"
Cohesion: 0.23
Nodes (5): CashPartyType, Fact, InlineData, Theory, CashManagementValidatorTests

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

### Community 178 - ".CreateStandardClaims"
Cohesion: 0.22
Nodes (6): Claim, CompanyAccessResponse, DateTimeOffset, Guid, IEnumerable, List

### Community 180 - "MiniErp.Application.Features.PartnerOpeningBalances"
Cohesion: 0.18
Nodes (3): MiniErp.Infrastructure.Services.PartnerOpeningBalances, MiniErp.Application.Features.PartnerOpeningBalances, MiniErp.Tests.PartnerOpeningBalances

### Community 181 - "Invoice"
Cohesion: 0.12
Nodes (12): ICollection, ItemsCategory, DateOnly, DateTime, ICollection, Invoice, InvoiceContentType, PaymentStatus (+4 more)

### Community 184 - "CompanyAndExchangeRateAuthorizationTests"
Cohesion: 0.25
Nodes (5): MiniErp.Tests.Authorization, Fact, InlineData, Theory, CompanyAndExchangeRateAuthorizationTests

### Community 185 - ".GetAllAsync"
Cohesion: 0.38
Nodes (6): CancellationToken, IReadOnlyList, Task, IStoreContainerService, StoreContainerResponse, StoreContainerWorkspaceResponse

### Community 188 - "DriverTripCostServiceTests"
Cohesion: 0.58
Nodes (3): Fact, Task, DriverTripCostServiceTests

### Community 189 - "MiniErp.Application.Features.StockOpeningBalances"
Cohesion: 0.25
Nodes (3): MiniErp.Tests.StockOpeningBalances, MiniErp.Infrastructure.Services.StockOpeningBalances, MiniErp.Application.Features.StockOpeningBalances

### Community 190 - "AbstractValidator"
Cohesion: 0.08
Nodes (26): AbstractValidator, int, ExchangeRateImportRequestValidator, ExchangeRateFilterRequestValidator, ExchangeRateRequestValidator, ExchangeRateUpdateRequestValidator, InventoryCountFilterRequestValidator, InvoiceFilterRequestValidator (+18 more)

### Community 191 - "Q: Cross-project MiniErp feature flow impact analysis"
Cohesion: 0.40
Nodes (4): Answer, Outcome, Q: Cross-project MiniErp feature flow impact analysis, Source Nodes

### Community 196 - ".LoginAsync"
Cohesion: 0.33
Nodes (3): LoginRequest, LoginRequestValidator, LoginResponse

### Community 198 - "CashMovementType"
Cohesion: 0.40
Nodes (4): ICollection, CashMovementType, EntityTypeBuilder, CashMovementTypeConfiguration

### Community 199 - "InventoryCostReportsSwaggerDocumentation.cs"
Cohesion: 0.40
Nodes (3): OpenApiOperation, OperationFilterContext, InventoryCostReportsSwaggerDocumentation

### Community 201 - ".Apply"
Cohesion: 0.40
Nodes (3): OpenApiOperation, OperationFilterContext, StoreContainersSwaggerDocumentation

### Community 203 - ".GetRateAsync"
Cohesion: 0.40
Nodes (4): CancellationToken, DateOnly, Task, ExternalExchangeRate

### Community 204 - ".Apply"
Cohesion: 0.40
Nodes (3): OpenApiOperation, OperationFilterContext, ItemUnitsSwaggerDocumentation

### Community 205 - ".ResolveAsync"
Cohesion: 0.40
Nodes (4): CancellationToken, DateOnly, Task, TestExchangeRateResolver

### Community 212 - "StockAdjustmentsSwaggerDocumentation.cs"
Cohesion: 0.40
Nodes (3): OpenApiOperation, OperationFilterContext, StockAdjustmentsSwaggerDocumentation

## Knowledge Gaps
- **97 isolated node(s):** `Asp.Versioning.Mvc (10.0.0)`, `Asp.Versioning.Mvc.ApiExplorer (10.0.0)`, `FluentValidation.DependencyInjectionExtensions (12.1.1)`, `Microsoft.EntityFrameworkCore.Design (10.0.10)`, `Microsoft.OpenApi (2.11.0)` (+92 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **61 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Result` connect `Result` to `StockOpeningBalanceService`, `PartnerOpeningBalanceServiceTests`, `StockAdjustmentService`, `BusinessPartnerService`, `InvoiceResponses.cs`, `AuthenticationService`, `CashMovementTypeService`, `ItemsCategoryService`, `CashboxService`, `ApplicationUser`, `Task`, `ApiErrorResponseFactory`, `DriverService`, `.Validation`, `StoreContainerErrors`, `.PrepareAsync`, `Task`, `.Failure`, `Error`, `CompanyService`, `ItemUnitService`, `MiniErp.Application.Features.Authentication`, `.UpdateAsync`, `.GetAllAsync`, `SelectResponse`, `PaginationRequest`, `.LoginAsync`, `InventoryCountService`, `.GetRateAsync`, `.ResolveAsync`, `ExchangeRateService`, `.GetRateAsync`, `CashVoucherUpdateRequest`, `ContainerService`, `PagedResponse`, `IScopedService`, `.UpdateCostsAsync`, `StatementResponses.cs`, `.GetAsync`, `.GetCostEntryAsync`?**
  _High betweenness centrality (0.141) - this node is a cross-community bridge._
- **Why does `Error` connect `Error` to `InvoiceServiceTests`, `StockOpeningBalanceService`, `PartnerOpeningBalanceServiceTests`, `StockAdjustmentService`, `BusinessPartnerService`, `CashMovementTypeService`, `CashboxService`, `InventoryCostingService`, `ApiErrorResponseFactory`, `DriverService`, `.Validation`, `.NotFound`, `StoreContainerErrors`, `InventoryStockService`, `.PrepareAsync`, `Result`, `.Failure`, `CompanyService`, `SelectResponse`, `.ResolveInboundUnitCostAsync`, `PaginationRequest`, `.ValidateStockAsync`, `StockAdjustmentErrors`, `InventoryCountService`, `ExchangeRateService`, `.GetRateAsync`, `IScopedService`, `.UpdateCostsAsync`, `CompanyErrors`, `StockOpeningBalanceErrors`, `.GetAsync`?**
  _High betweenness centrality (0.141) - this node is a cross-community bridge._
- **Why does `ApplicationDbContext` connect `ApplicationDbContext` to `InvoiceServiceTests`, `StockOpeningBalanceService`, `PartnerOpeningBalanceServiceTests`, `StockAdjustmentService`, `BusinessPartnerService`, `.CreateAsync`, `MiniErp.Domain.Entities.Companies`, `ApplicationUser`, `Task`, `Task`, `Company`, `MiniErp.Infrastructure.Persistence.Configurations`, `Task`, `Task`, `AuditableEntity`, `ExchangeRateServiceTests`, `CurrencyCode`, `Task`, `Driver`, `StockOpeningBalanceServiceTests`, `AccessTokenCompanyTestDatabase`, `IAsyncDisposable`, `Invoice`, `Task`, `CategoryTestDatabase`, `RefreshToken`, `CashMovementType`, `.CreateAsync`, `StockOpeningBalanceLine`, `ItemMovement`, `InventoryCount`, `.GetCostEntryAsync`?**
  _High betweenness centrality (0.118) - this node is a cross-community bridge._
- **What connects `Asp.Versioning.Mvc (10.0.0)`, `Asp.Versioning.Mvc.ApiExplorer (10.0.0)`, `FluentValidation.DependencyInjectionExtensions (12.1.1)` to the rest of the system?**
  _97 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `InvoiceServiceTests` be split into smaller, more focused modules?**
  _Cohesion score 0.051485414706277724 - nodes in this community are weakly interconnected._
- **Should `StockOpeningBalanceService` be split into smaller, more focused modules?**
  _Cohesion score 0.06527682843472317 - nodes in this community are weakly interconnected._
- **Should `PartnerOpeningBalanceServiceTests` be split into smaller, more focused modules?**
  _Cohesion score 0.05581395348837209 - nodes in this community are weakly interconnected._