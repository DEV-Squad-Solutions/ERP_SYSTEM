# Graph Report - MiniErp  (2026-07-31)

## Corpus Check
- 482 files · ~140,141 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 3963 nodes · 10933 edges · 214 communities (156 shown, 58 thin omitted)
- Extraction: 99% EXTRACTED · 1% INFERRED · 0% AMBIGUOUS · INFERRED: 84 edges (avg confidence: 0.8)
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
- CashVoucherServiceTests
- MiniErp.Application.Common.Results
- BusinessPartnerService
- AuthenticationService
- IRegister
- .CreateAsync
- CashMovementTypeService
- ItemsCategoryService
- CashboxService
- MiniErp.Domain.Entities.Companies
- InventoryCostingService
- UserTestDatabase
- Task
- .UpdateAsync
- CountryService
- ApiErrorResponseFactory
- DriverService
- MiniErp.Domain.Enums
- Company
- MiniErp.Application.Common.Abstractions
- MiniErp.Application.Common.Models
- MiniErp.Infrastructure.Persistence.Configurations
- InventoryCountService
- AbstractValidator
- MiniErp.Application.Features.Authentication
- MiniErp.Application.Features.Stores
- .Validation
- Task
- InventoryStockService
- .NotFound
- Task
- ItemService
- AuditableEntity
- UserService
- IUserService
- Error
- MiniErp.Api.csproj
- CurrencyCode
- .TryParse
- Task
- CompanyService
- ExchangeRateService
- Invoice
- MiniErp.Api
- ItemUnitService
- StockOpeningBalanceServiceTests
- AccessTokenCompanyTestDatabase
- .CreateAsync
- ArabicIdentityErrorDescriber
- Task
- Task
- ItemMovement
- .AddAsync
- StoreService
- Result
- IAsyncDisposable
- ApplicationDbContext
- PaginationRequest
- RefreshToken
- .Create
- .PrepareAsync
- AuditableEntityInterceptor
- .Create
- .Create
- UsersController
- ICurrentCompanyContext
- .CreateAsync
- StoresController
- InvoiceService
- .GetAll
- .Create
- .Create
- ProducesResponseType&lt;ProblemDetails&gt;
- http
- ExchangeRate
- .Create
- .Create
- Task
- ProducesResponseType&lt;IReadOnlyList&lt;SelectResponse&gt;&gt;
- InvoiceMappingRegister
- .GetCashboxStatement
- .Upsert
- MiniErp.Application.Features.ExchangeRates
- EnumRequestOperationDocumentationFilter
- PagedResponse
- StockOpeningBalance
- InvoicePaymentTermTests
- SwaggerExtensions
- .Create
- MiniErp.Api.Swagger
- .GetAsync
- .Create
- InventoryCostReportFilterRequest
- BusinessPartnersController
- ApiControllerBase
- InventoryCountRequest
- IScopedService
- ICashboxService
- .ConvertToBase
- .Apply
- CashVoucherRequest
- JwtOptions
- CompanyMappingRegister
- InventoryCount
- UserRequestValidatorTests
- MiniErp.Application.Features.StockAdjustments
- ArabicValidationConfiguration
- ExchangeRateServiceTests
- CashMasterServiceTests
- .GetAllAsync
- Migration
- AddTablesItemAndItemUnit
- CashVoucherResponse
- .ApplyPendingMigrationsAsync
- .Login
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
- MiniErp.Application.Features.StockOpeningBalances
- .Apply
- StatementsSwaggerDocumentation.cs
- MiniErp.Application.Features.Companies
- IOperationFilter
- .CreateAsync
- .Apply
- ApplicationUser
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
- MiniErp.Infrastructure.Persistence.Migrations
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
- AddInvoiceContentType
- addexchangerate
- addwbforinvoice
- additemCategory
- InventoryDocumentValidatorTests
- .CreateStandardClaims
- MiniErp.Application.Features.PartnerOpeningBalances
- DriverTripBulkCostUpdateRequest
- ItemsCategory
- ItemsCategoryRequest
- CompanyAndExchangeRateAuthorizationTests
- StockOpeningBalanceAmountRules
- Country
- .Apply
- UserCreateRequest
- StoreContainerUpsertRequest
- CashVoucherUpdateRequest
- Q: Cross-project MiniErp feature flow impact analysis
- AddCompanyRowVersion
- CashboxMappingRegister
- CashMovementTypeMappingRegister
- CashVoucherMappingRegister
- InventoryCountMappingRegister
- ItemsCategoryMappingRegister
- CompanyFilterRequest
- PartnerOpeningBalanceMappingRegister
- StockAdjustmentMappingRegister
- StockOpeningBalanceMappingRegister
- MiniErp.Application
- CompanyIdsValidator
- ContainerMappingRegister
- CountryMappingRegister
- DriverMappingRegister
- DriverTripCostMappingRegister
- RolesValidator
- InventoryCountsSwaggerDocumentation.cs
- UserMappingRegister
- MappingConfigurationTests
- StockAdjustmentsSwaggerDocumentation.cs
- StoreContainerMappingRegister

## God Nodes (most connected - your core abstractions)
1. `Result` - 289 edges
2. `InvoiceServiceTests` - 137 edges
3. `ApplicationDbContext` - 120 edges
4. `MiniErp.Domain.Enums` - 110 edges
5. `MiniErp.Application.Common.Models` - 89 edges
6. `PaginationRequest` - 87 edges
7. `MiniErp.Application.Common.Results` - 70 edges
8. `MiniErp.Application.Common.Abstractions` - 57 edges
9. `Company` - 55 edges
10. `MiniErp.Domain.Entities.Companies` - 54 edges

## Surprising Connections (you probably didn't know these)
- `TestCurrentCompanyContext` --implements--> `ICurrentCompanyContext`  [EXTRACTED]
  tests/MiniErp.Tests/BusinessPartners/BusinessPartnerIntegrityServiceTests.cs → src/MiniErp.Application/Common/Abstractions/ICurrentCompanyContext.cs
- `TestCurrentCompanyContext` --implements--> `ICurrentCompanyContext`  [EXTRACTED]
  tests/MiniErp.Tests/CashManagement/CashManagementTestDatabase.cs → src/MiniErp.Application/Common/Abstractions/ICurrentCompanyContext.cs
- `TestCurrentCompanyContext` --implements--> `ICurrentCompanyContext`  [EXTRACTED]
  tests/MiniErp.Tests/Inventory/InventoryDocumentTestDatabase.cs → src/MiniErp.Application/Common/Abstractions/ICurrentCompanyContext.cs
- `TestCurrentCompanyContext` --implements--> `ICurrentCompanyContext`  [EXTRACTED]
  tests/MiniErp.Tests/BusinessPartners/BusinessPartnerContainerStoreServiceTests.cs → src/MiniErp.Application/Common/Abstractions/ICurrentCompanyContext.cs
- `TestCurrentCompanyContext` --implements--> `ICurrentCompanyContext`  [EXTRACTED]
  tests/MiniErp.Tests/Containers/ContainerServiceTests.cs → src/MiniErp.Application/Common/Abstractions/ICurrentCompanyContext.cs

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **MiniErp Clean Architecture Layers** — readme_minierp_domain, readme_minierp_application, readme_minierp_infrastructure, readme_minierp_api [EXTRACTED 1.00]
- **Company-Scoped Authentication Flow** — readme_jwt_authentication, readme_company_selection_token, readme_company_scoped_access_token, readme_rotating_refresh_tokens [EXTRACTED 1.00]
- **Application Outcome Handling** — readme_result_pattern, readme_fluentvalidation, readme_global_exception_handling [EXTRACTED 1.00]

## Communities (214 total, 58 thin omitted)

### Community 0 - "InvoiceServiceTests"
Cohesion: 0.06
Nodes (15): InvoiceTestDatabase, InvoicePriceStatus, InvoiceLineRequest, InvoiceType, PaymentTerm, DateOnly, Fact, InlineData (+7 more)

### Community 1 - "StockOpeningBalanceService"
Cohesion: 0.05
Nodes (49): OpeningMovementCost, ProducesResponseType&lt;PagedResponse&lt;StockOpeningBalanceListResponse&gt;&gt;, ProducesResponseType&lt;StockOpeningBalanceResponse&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost (+41 more)

### Community 2 - "PartnerOpeningBalanceServiceTests"
Cohesion: 0.05
Nodes (40): PartnerOpeningBalanceTestDatabase, ProducesResponseType&lt;PagedResponse&lt;PartnerOpeningBalanceResponse&gt;&gt;, ProducesResponseType&lt;PartnerOpeningBalanceResponse&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost (+32 more)

### Community 3 - "StockAdjustmentService"
Cohesion: 0.07
Nodes (35): MovementCostSnapshot, CancellationToken, Task, IStockAdjustmentService, StockAdjustmentFilterRequest, int, StockAdjustmentLineRequest, StockAdjustmentRequest (+27 more)

### Community 4 - "CashVoucherServiceTests"
Cohesion: 0.26
Nodes (5): Fact, InlineData, Task, Theory, CashVoucherServiceTests

### Community 5 - "MiniErp.Application.Common.Results"
Cohesion: 0.08
Nodes (14): MiniErp.Tests.Authentication, MiniErp.Infrastructure.Services.Users, MiniErp.Infrastructure.Services.Companies, MiniErp.Application.Features.Users, MiniErp.Infrastructure.Identity, MiniErp.Application.Common.Authentication, MiniErp.Application.Common.Results, MiniErp.Tests.Users (+6 more)

### Community 6 - "BusinessPartnerService"
Cohesion: 0.07
Nodes (31): BusinessPartnerIntegrityTestDatabase, BusinessPartnerContainerStoreResponse, BusinessPartnerFilterRequest, BusinessPartnerFilterRequestValidator, BusinessPartnerRequest, BusinessPartnerRequestValidator, IReadOnlyList, BusinessPartnerResponse (+23 more)

### Community 7 - "AuthenticationService"
Cohesion: 0.17
Nodes (9): CompanySelectionTokenData, LoginResponse, TokenResponse, CancellationToken, Guid, Task, AuthenticationService, CompanySelectionTokenData (+1 more)

### Community 8 - "IRegister"
Cohesion: 0.15
Nodes (9): IRegister, TypeAdapterConfig, BusinessPartnerMappingRegister, TypeAdapterConfig, ExchangeRateMappingRegister, TypeAdapterConfig, ItemUnitMappingRegister, TypeAdapterConfig (+1 more)

### Community 9 - ".CreateAsync"
Cohesion: 0.11
Nodes (17): DateOnly, Fact, Task, InventoryCostingServiceTests, DateOnly, Fact, Task, InventoryCostReportServiceTests (+9 more)

### Community 10 - "CashMovementTypeService"
Cohesion: 0.07
Nodes (34): ProducesResponseType&lt;CashMovementTypeResponse&gt;, ProducesResponseType&lt;IReadOnlyList&lt;CashMovementTypeSelectResponse&gt;&gt;, ProducesResponseType&lt;PagedResponse&lt;CashMovementTypeResponse&gt;&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost (+26 more)

### Community 11 - "ItemsCategoryService"
Cohesion: 0.15
Nodes (12): CancellationToken, IReadOnlyList, Task, IItemsCategoryService, ItemsCategoryResponse, ItemsCategorySelectResponse, CancellationToken, int (+4 more)

### Community 12 - "CashboxService"
Cohesion: 0.24
Nodes (7): CashboxResponse, CancellationToken, int, IQueryable, IReadOnlyList, Task, CashboxService

### Community 13 - "MiniErp.Domain.Entities.Companies"
Cohesion: 0.10
Nodes (14): MiniErp.Infrastructure.Seeding, MiniErp.Domain.Entities.BusinessPartners, MiniErp.Domain.Entities.Catalog, MiniErp.Tests.ExchangeRates, MiniErp.Domain.Entities.Companies, MiniErp.Domain.Entities.Logistics, MiniErp.Domain.Common.Entities, MiniErp.Domain.Entities.Containers (+6 more)

### Community 14 - "InventoryCostingService"
Cohesion: 0.09
Nodes (24): InboundCostResult, PendingOutbound, Queue, CancellationToken, DateOnly, IReadOnlyCollection, IReadOnlyDictionary, Task (+16 more)

### Community 15 - "UserTestDatabase"
Cohesion: 0.16
Nodes (11): AsyncServiceScope, Guid, IConfiguration, IdentityRole, RoleManager, ServiceProvider, SqliteConnection, UserManager (+3 more)

### Community 16 - "Task"
Cohesion: 0.10
Nodes (10): InventoryDeletionDatabase, Fact, MemberData, SqliteConnection, Task, Theory, TheoryData, ValueTask (+2 more)

### Community 17 - ".UpdateAsync"
Cohesion: 0.11
Nodes (16): Credit, Debit, InvoiceMovementRules, BusinessPartnerMovementType, ItemMovementType, CancellationToken, IEnumerable, IReadOnlyCollection (+8 more)

### Community 18 - "CountryService"
Cohesion: 0.09
Nodes (25): ProducesResponseType&lt;CountryResponse&gt;, ProducesResponseType&lt;PagedResponse&lt;CountryResponse&gt;&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost, HttpPut (+17 more)

### Community 19 - "ApiErrorResponseFactory"
Cohesion: 0.06
Nodes (33): ActionExecutingContext, MiniErp.Api.Errors, MiniErp.Api.Exceptions, MiniErp.Api.Validation, Exception, IDictionary, IExceptionHandler, IFluentValidationAutoValidationResultFactory (+25 more)

### Community 20 - "DriverService"
Cohesion: 0.15
Nodes (12): DriverRequest, DriverRequestValidator, DriverResponse, CancellationToken, IReadOnlyList, Task, IDriverService, CancellationToken (+4 more)

### Community 21 - "MiniErp.Domain.Enums"
Cohesion: 0.07
Nodes (11): MiniErp.Infrastructure.Services.CashMovementTypes, MiniErp.Application.Features.Cashboxes, MiniErp.Application.Features.CashVouchers, MiniErp.Application.Features.DriverTrips, MiniErp.Application.Features.Invoices, MiniErp.Tests.Invoices, MiniErp.Domain.Enums, MiniErp.Domain.Entities.Invoicing (+3 more)

### Community 22 - "Company"
Cohesion: 0.09
Nodes (31): IServiceProvider, SeedBusinessPartner, SeedCompany, SeedContainer, SeedCountry, SeedDriver, SeedStore, SeedUser (+23 more)

### Community 23 - "MiniErp.Application.Common.Abstractions"
Cohesion: 0.09
Nodes (26): MiniErp.Infrastructure.Services.Containers, MiniErp.Infrastructure.Services.StockAdjustments, MiniErp.Infrastructure.Services.BusinessPartners, MiniErp.Tests.Companies, MiniErp.Infrastructure, MiniErp.Infrastructure.Services.Stores, MiniErp.Tests.BusinessPartners, MiniErp.Tests.ItemsCategories (+18 more)

### Community 24 - "MiniErp.Application.Common.Models"
Cohesion: 0.09
Nodes (10): MiniErp.Infrastructure.Services.ItemsCategories, MiniErp.Application.Features.ItemUnits, MiniErp.Api.Extensions, MiniErp.Application.Common.Models, MiniErp.Application.Features.Drivers, MiniErp.Infrastructure.Services.ItemUnits, MiniErp.Application.Features.Statements, MiniErp.Application.Features.Countries (+2 more)

### Community 25 - "MiniErp.Infrastructure.Persistence.Configurations"
Cohesion: 0.07
Nodes (25): MiniErp.Infrastructure.Persistence.Configurations, ICollection, CashMovementType, ICollection, Container, DateOnly, ContainerMovement, StoreContainer (+17 more)

### Community 26 - "InventoryCountService"
Cohesion: 0.11
Nodes (14): InventoryCountFilterRequest, InventoryCountLineResponse, InventoryCountListResponse, InventoryCountResponse, CancellationToken, DateTime, IEnumerable, int (+6 more)

### Community 27 - "AbstractValidator"
Cohesion: 0.09
Nodes (21): AbstractValidator, PaginationRequestValidator, InventoryCountFilterRequestValidator, InvoiceFilterRequestValidator, int, InvoiceContainerLineRequest, InvoiceRequest, InvoiceUpdateRequest (+13 more)

### Community 28 - "MiniErp.Application.Features.Authentication"
Cohesion: 0.12
Nodes (11): MiniErp.Application.Features.Authentication, CompanyAccessResponse, CancellationToken, Task, IAuthenticationService, LoginRequest, LoginRequestValidator, RefreshTokenRequest (+3 more)

### Community 29 - "MiniErp.Application.Features.Stores"
Cohesion: 0.14
Nodes (5): MiniErp.Application.Features.Stores, MiniErp.Infrastructure.Services.StoreContainers, MiniErp.Application.Features.BusinessPartners, MiniErp.Application.Features.Containers, MiniErp.Application.Features.StoreContainers

### Community 30 - ".Validation"
Cohesion: 0.24
Nodes (5): CancellationToken, int, IReadOnlyList, Task, StoreContainerService

### Community 31 - "Task"
Cohesion: 0.13
Nodes (13): DriverTestDatabase, DateOnly, DateTimeOffset, Fact, InlineData, SqliteConnection, Task, Theory (+5 more)

### Community 32 - "InventoryStockService"
Cohesion: 0.12
Nodes (22): CancellationToken, DateOnly, DateTime, IReadOnlyCollection, IReadOnlyDictionary, Task, IInventoryStockService, InventoryMovementReference (+14 more)

### Community 33 - ".NotFound"
Cohesion: 0.15
Nodes (7): CancellationToken, int, IQueryable, Task, CashVoucherService, VoucherPreparation, VoucherPreparation

### Community 34 - "Task"
Cohesion: 0.10
Nodes (15): CompanyTestDatabase, Guid, ICurrentUserService, Guid, CurrentUserService, Fact, Guid, InlineData (+7 more)

### Community 35 - "ItemService"
Cohesion: 0.11
Nodes (17): MiniErp.Application.Features.Items, CancellationToken, IReadOnlyList, Task, IItemService, ItemFilterRequest, ItemFilterRequestValidator, TypeAdapterConfig (+9 more)

### Community 36 - "AuditableEntity"
Cohesion: 0.08
Nodes (24): DateTime, AuditableEntity, Item, ICollection, ItemUnit, InventoryCountLine, ItemStoreBalance, StockAdjustmentLine (+16 more)

### Community 37 - "UserService"
Cohesion: 0.18
Nodes (12): UserCompanyResponse, UserResponse, CancellationToken, Guid, HashSet, IdentityResult, IQueryable, IReadOnlyCollection (+4 more)

### Community 38 - "IUserService"
Cohesion: 0.21
Nodes (8): CancellationToken, Guid, IReadOnlyList, Task, IUserService, UserCompaniesRequest, UserFilterRequest, UserUpdateRequest

### Community 39 - "Error"
Cohesion: 0.17
Nodes (4): Error, InvoiceService, PreparedInvoice, PaymentPreparation

### Community 40 - "MiniErp.Api.csproj"
Cohesion: 0.08
Nodes (26): Asp.Versioning.Mvc (10.0.0), Asp.Versioning.Mvc.ApiExplorer (10.0.0), Bogus (35.6.5), FluentValidation (12.1.1), FluentValidation.DependencyInjectionExtensions (12.1.1), Mapster (10.0.11), Microsoft.AspNetCore.Authentication.JwtBearer (10.0.9), Microsoft.AspNetCore.Identity.EntityFrameworkCore (10.0.10) (+18 more)

### Community 41 - "CurrencyCode"
Cohesion: 0.09
Nodes (18): BusinessPartner, DateOnly, BusinessPartnerMovement, DateOnly, PartnerOpeningBalance, DateOnly, DateTime, CashVoucher (+10 more)

### Community 42 - ".TryParse"
Cohesion: 0.09
Nodes (19): MiniErp.Tests.Common, MiniErp.Application.Common.Parsing, MiniErp.Api.ModelBinding, CultureInfo, IModelBinder, IModelBinderProvider, ModelBinderProviderContext, ModelBindingContext (+11 more)

### Community 43 - "Task"
Cohesion: 0.16
Nodes (9): IsActive, IsDeleted, StoreContainerTestDatabase, Fact, SqliteConnection, Task, ValueTask, StoreContainerServiceTests (+1 more)

### Community 44 - "CompanyService"
Cohesion: 0.30
Nodes (5): CompanyResponse, CancellationToken, IReadOnlyList, Task, CompanyService

### Community 45 - "ExchangeRateService"
Cohesion: 0.06
Nodes (37): ProducesResponseType&lt;ExchangeRateResolutionResponse&gt;, ProducesResponseType&lt;ExchangeRateResponse&gt;, ProducesResponseType&lt;PagedResponse&lt;ExchangeRateResponse&gt;&gt;, Authorize, CancellationToken, DateOnly, HttpDelete, HttpGet (+29 more)

### Community 46 - "Invoice"
Cohesion: 0.10
Nodes (16): DateOnly, DateTime, ICollection, Invoice, DateOnly, Driver, DateOnly, DriverTrip (+8 more)

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
Cohesion: 0.19
Nodes (7): ContainerTestDatabase, Fact, SqliteConnection, Task, ValueTask, ContainerServiceTests, ContainerTestDatabase

### Community 54 - "Task"
Cohesion: 0.17
Nodes (9): CountryTestDatabase, Fact, InlineData, SqliteConnection, Task, Theory, ValueTask, CountryServiceTests (+1 more)

### Community 55 - "ItemMovement"
Cohesion: 0.14
Nodes (12): InventoryCostAllocation, DateOnly, ICollection, ItemMovement, ICollection, Store, EntityTypeBuilder, InventoryCostAllocationConfiguration (+4 more)

### Community 56 - ".AddAsync"
Cohesion: 0.11
Nodes (20): InvoiceFilterRequest, InvoiceContainerLineResponse, InvoiceItemBalanceResponse, InvoiceListResponse, InvoicePagedResponse, InvoiceResponse, InvoiceSummaryResponse, CancellationToken (+12 more)

### Community 57 - "StoreService"
Cohesion: 0.06
Nodes (29): SelectResponse, ContainerFilterRequest, ContainerFilterRequestValidator, ContainerRequest, ContainerRequestValidator, ContainerResponse, CancellationToken, IReadOnlyList (+21 more)

### Community 58 - "Result"
Cohesion: 0.12
Nodes (15): Result, Result, CompanyUpdateRequest, CompanyUpdateRequestValidator, CancellationToken, IReadOnlyList, Task, ICompanyService (+7 more)

### Community 59 - "IAsyncDisposable"
Cohesion: 0.17
Nodes (10): CategoryTestDatabase, IAsyncDisposable, Fact, InlineData, SqliteConnection, Task, Theory, ValueTask (+2 more)

### Community 60 - "ApplicationDbContext"
Cohesion: 0.13
Nodes (12): DbContextOptions, DbSet, IdentityDbContext, ModelBuilder, Guid, IdentityRole, ApplicationDbContext, SqliteConnection (+4 more)

### Community 61 - "PaginationRequest"
Cohesion: 0.07
Nodes (31): DriverStatementRaw, PartnerStatementRaw, int, PaginationRequest, CancellationToken, Task, IInventoryCostReportService, CancellationToken (+23 more)

### Community 62 - "RefreshToken"
Cohesion: 0.12
Nodes (13): IEntityTypeConfiguration, CompanySettings, DateTimeOffset, Guid, RefreshToken, Guid, UserCompany, EntityTypeBuilder (+5 more)

### Community 63 - ".Create"
Cohesion: 0.18
Nodes (15): ProducesResponseType&lt;CashboxResponse&gt;, ProducesResponseType&lt;IReadOnlyList&lt;CashboxSelectResponse&gt;&gt;, ProducesResponseType&lt;PagedResponse&lt;CashboxResponse&gt;&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost (+7 more)

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
Cohesion: 0.14
Nodes (13): ICurrentCompanyContext, SeedCompanyContext, TestCurrentCompanyContext, TestCurrentCompanyContext, TestCurrentCompanyContext, TestCurrentCompanyContext, TestCurrentCompanyContext, TestCurrentCompanyContext (+5 more)

### Community 70 - ".CreateAsync"
Cohesion: 0.21
Nodes (7): BusinessPartnerContainerStoreTestDatabase, Fact, SqliteConnection, Task, ValueTask, BusinessPartnerContainerStoreServiceTests, BusinessPartnerContainerStoreTestDatabase

### Community 71 - "StoresController"
Cohesion: 0.25
Nodes (12): ProducesResponseType&lt;PagedResponse&lt;StoreResponse&gt;&gt;, ProducesResponseType&lt;StoreResponse&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost, HttpPut (+4 more)

### Community 73 - ".GetAll"
Cohesion: 0.26
Nodes (11): ProducesResponseType&lt;CompanyResponse&gt;, ProducesResponseType&lt;PagedResponse&lt;CompanyResponse&gt;&gt;, CancellationToken, HttpDelete, HttpGet, HttpPost, HttpPut, IActionResult (+3 more)

### Community 74 - ".Create"
Cohesion: 0.26
Nodes (12): ProducesResponseType&lt;InventoryCountResponse&gt;, ProducesResponseType&lt;PagedResponse&lt;InventoryCountListResponse&gt;&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost, HttpPut (+4 more)

### Community 75 - ".Create"
Cohesion: 0.25
Nodes (12): ProducesResponseType&lt;ItemResponse&gt;, ProducesResponseType&lt;PagedResponse&lt;ItemResponse&gt;&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost, HttpPut (+4 more)

### Community 76 - "ProducesResponseType&lt;ProblemDetails&gt;"
Cohesion: 0.25
Nodes (13): ProducesResponseType&lt;ItemUnitResponse&gt;, ProducesResponseType&lt;PagedResponse&lt;ItemUnitResponse&gt;&gt;, ProducesResponseType&lt;ProblemDetails&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost (+5 more)

### Community 77 - "http"
Cohesion: 0.12
Nodes (17): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables, launchBrowser, launchUrl, applicationUrl (+9 more)

### Community 78 - "ExchangeRate"
Cohesion: 0.14
Nodes (11): DateOnly, ICollection, Cashbox, DateOnly, DateTime, ExchangeRate, ExchangeRateSource, EntityTypeBuilder (+3 more)

### Community 79 - ".Create"
Cohesion: 0.23
Nodes (13): ProducesResponseType&lt;IReadOnlyList&lt;ItemsCategorySelectResponse&gt;&gt;, ProducesResponseType&lt;ItemsCategoryResponse&gt;, ProducesResponseType&lt;PagedResponse&lt;ItemsCategoryResponse&gt;&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost (+5 more)

### Community 80 - ".Create"
Cohesion: 0.24
Nodes (12): ProducesResponseType&lt;PagedResponse&lt;StockAdjustmentListResponse&gt;&gt;, ProducesResponseType&lt;StockAdjustmentResponse&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost, HttpPut (+4 more)

### Community 81 - "Task"
Cohesion: 0.26
Nodes (7): Fact, InlineData, Task, Theory, TestCurrentUserService, UserServiceTests, UserTestDatabase

### Community 82 - "ProducesResponseType&lt;IReadOnlyList&lt;SelectResponse&gt;&gt;"
Cohesion: 0.23
Nodes (13): ProducesResponseType&lt;ContainerResponse&gt;, ProducesResponseType&lt;IReadOnlyList&lt;SelectResponse&gt;&gt;, ProducesResponseType&lt;PagedResponse&lt;ContainerResponse&gt;&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost (+5 more)

### Community 84 - ".GetCashboxStatement"
Cohesion: 0.17
Nodes (14): ProducesResponseType&lt;CashboxStatementResponse&gt;, ProducesResponseType&lt;DriverStatementResponse&gt;, ProducesResponseType&lt;PartnerStatementResponse&gt;, CancellationToken, HttpGet, IActionResult, Task, StatementsController (+6 more)

### Community 85 - ".Upsert"
Cohesion: 0.25
Nodes (11): ProducesResponseType&lt;IReadOnlyList&lt;StoreContainerResponse&gt;&gt;, ProducesResponseType&lt;PagedResponse&lt;StoreContainerResponse&gt;&gt;, ProducesResponseType&lt;StoreContainerResponse&gt;, ProducesResponseType&lt;StoreContainerWorkspaceResponse&gt;, Authorize, CancellationToken, HttpGet, HttpPut (+3 more)

### Community 86 - "MiniErp.Application.Features.ExchangeRates"
Cohesion: 0.14
Nodes (6): MiniErp.Infrastructure.Services.ExchangeRates, MiniErp.Tests, MiniErp.Application.Features.ExchangeRates, MiniErp.Infrastructure.Services.Cashboxes, ExchangeRateFilterRequest, ExchangeRateFilterRequestValidator

### Community 87 - "EnumRequestOperationDocumentationFilter"
Cohesion: 0.20
Nodes (9): EnumProperty, HashSet, int, IReadOnlyList, OpenApiOperation, OperationFilterContext, Type, EnumProperty (+1 more)

### Community 88 - "PagedResponse"
Cohesion: 0.08
Nodes (18): CancellationToken, IOrderedQueryable, Task, IPaginationService, PagedResponse, DriverTripCostFilterRequest, DriverTripCostFilterRequestValidator, DriverTripBulkCostUpdateResponse (+10 more)

### Community 89 - "StockOpeningBalance"
Cohesion: 0.33
Nodes (5): DateOnly, ICollection, StockOpeningBalance, EntityTypeBuilder, StockOpeningBalanceConfiguration

### Community 90 - "InvoicePaymentTermTests"
Cohesion: 0.27
Nodes (4): Fact, InlineData, Theory, InvoicePaymentTermTests

### Community 91 - "SwaggerExtensions"
Cohesion: 0.29
Nodes (4): IConfiguration, IServiceCollection, WebApplication, SwaggerExtensions

### Community 92 - ".Create"
Cohesion: 0.15
Nodes (7): OpenApiOperation, OperationFilterContext, ItemUnitsSwaggerDocumentation, OpenApiOperation, OperationFilterContext, StoresSwaggerDocumentation, SwaggerOperationDescription

### Community 93 - "MiniErp.Api.Swagger"
Cohesion: 0.17
Nodes (8): MiniErp.Api.Swagger, OpenApiOperation, OperationFilterContext, CashMovementTypesSwaggerDocumentation, OpenApiOperation, OperationFilterContext, string, UnifiedErrorResponseSwaggerFilter

### Community 94 - ".GetAsync"
Cohesion: 0.14
Nodes (11): MovementProjection, InvoiceLineResponse, InventoryCostStatus, CancellationToken, DateOnly, DateTime, int, Task (+3 more)

### Community 95 - ".Create"
Cohesion: 0.18
Nodes (14): ProducesResponseType&lt;DriverResponse&gt;, ProducesResponseType&lt;PagedResponse&lt;DriverResponse&gt;&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost, HttpPut (+6 more)

### Community 97 - "BusinessPartnersController"
Cohesion: 0.23
Nodes (13): ProducesResponseType&lt;BusinessPartnerContainerStoreResponse&gt;, ProducesResponseType&lt;BusinessPartnerResponse&gt;, ProducesResponseType&lt;PagedResponse&lt;BusinessPartnerResponse&gt;&gt;, Authorize, CancellationToken, HttpDelete, HttpGet, HttpPost (+5 more)

### Community 98 - "ApiControllerBase"
Cohesion: 0.11
Nodes (17): ControllerBase, ProducesResponseType&lt;DriverTripBulkCostUpdateResponse&gt;, ProducesResponseType&lt;InventoryCostReportResponse&gt;, ProducesResponseType&lt;PagedResponse&lt;DriverTripCostResponse&gt;&gt;, ApiControllerBase, Authorize, CancellationToken, HttpGet (+9 more)

### Community 99 - "InventoryCountRequest"
Cohesion: 0.18
Nodes (10): int, InventoryCountIncreaseCostRequest, InventoryCountLineUpdateRequest, InventoryCountReconcileRequest, InventoryCountRequest, InventoryCountUpdateRequest, InventoryCountLineUpdateRequestValidator, InventoryCountReconcileRequestValidator (+2 more)

### Community 100 - "IScopedService"
Cohesion: 0.33
Nodes (3): IScopedService, int, DriverTripService

### Community 101 - "ICashboxService"
Cohesion: 0.29
Nodes (7): CashboxUpdateRequest, CashboxUpdateRequestValidator, CashboxSelectResponse, CancellationToken, IReadOnlyList, Task, ICashboxService

### Community 102 - ".ConvertToBase"
Cohesion: 0.16
Nodes (6): int, ExchangeRateRules, Fact, InlineData, Theory, ExchangeRateRulesTests

### Community 103 - ".Apply"
Cohesion: 0.27
Nodes (6): IOpenApiSchema, ISchemaFilter, SchemaFilterContext, Type, EnumDocumentationFormatter, EnumSchemaDocumentationFilter

### Community 104 - "CashVoucherRequest"
Cohesion: 0.26
Nodes (8): int, CashVoucherRequest, CashVoucherRequestValidator, CashDirection, DateOnly, Fact, Task, FinancialStatementServiceTests

### Community 105 - "JwtOptions"
Cohesion: 0.14
Nodes (11): ClaimsPrincipal, CompanyClaimResolver, IConfiguration, IServiceCollection, DependencyInjection, int, CurrentCompanyContext, string (+3 more)

### Community 107 - "InventoryCount"
Cohesion: 0.28
Nodes (6): DateOnly, DateTime, ICollection, InventoryCount, EntityTypeBuilder, InventoryCountConfiguration

### Community 108 - "UserRequestValidatorTests"
Cohesion: 0.60
Nodes (3): Fact, Task, UserRequestValidatorTests

### Community 109 - "MiniErp.Application.Features.StockAdjustments"
Cohesion: 0.10
Nodes (9): MiniErp.Tests.Inventory, MiniErp.Infrastructure.Services.InventoryCounts, MiniErp.Application.Features.InventoryCostReports, MiniErp.Application.Features.InventoryCounts, MiniErp.Application.Features.StockAdjustments, InventoryCostAllocationReportResponse, InventoryCostReportItemResponse, InventoryCostReportResponse (+1 more)

### Community 110 - "ArabicValidationConfiguration"
Cohesion: 0.29
Nodes (5): MiniErp.Application.Common.Validation, LanguageManager, IReadOnlyDictionary, ArabicLanguageManager, ArabicValidationConfiguration

### Community 111 - "ExchangeRateServiceTests"
Cohesion: 0.11
Nodes (19): DbConnection, DbTransaction, DbTransactionInterceptor, ExchangeRateRow, ExchangeRateTestDatabase, IsolationCaptureInterceptor, IsolationLevel, CancellationToken (+11 more)

### Community 112 - "CashMasterServiceTests"
Cohesion: 0.18
Nodes (9): int, CashboxRequest, CashboxRequestValidator, PartnerAccountEffect, Fact, InlineData, Task, Theory (+1 more)

### Community 113 - ".GetAllAsync"
Cohesion: 0.27
Nodes (7): CancellationToken, IReadOnlyList, Task, IStoreContainerService, StoreContainerFilterRequest, StoreContainerResponse, StoreContainerWorkspaceResponse

### Community 114 - "Migration"
Cohesion: 0.50
Nodes (3): Migration, MigrationBuilder, InitialIdentity

### Community 116 - "CashVoucherResponse"
Cohesion: 0.31
Nodes (6): CashVoucherFilterRequest, CashVoucherFilterRequestValidator, CashVoucherResponse, CancellationToken, Task, ICashVoucherService

### Community 117 - ".ApplyPendingMigrationsAsync"
Cohesion: 0.33
Nodes (4): CancellationToken, Task, WebApplication, DatabaseMigrationExtensions

### Community 118 - ".Login"
Cohesion: 0.36
Nodes (9): AllowAnonymous, ProducesResponseType&lt;LoginResponse&gt;, ProducesResponseType&lt;TokenResponse&gt;, CancellationToken, HttpPost, IActionResult, ProducesResponseType, Task (+1 more)

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

### Community 135 - "MiniErp.Application.Features.StockOpeningBalances"
Cohesion: 0.29
Nodes (3): MiniErp.Tests.StockOpeningBalances, MiniErp.Infrastructure.Services.StockOpeningBalances, MiniErp.Application.Features.StockOpeningBalances

### Community 136 - ".Apply"
Cohesion: 0.40
Nodes (3): OpenApiOperation, OperationFilterContext, PartnerOpeningBalancesSwaggerDocumentation

### Community 137 - "StatementsSwaggerDocumentation.cs"
Cohesion: 0.40
Nodes (3): OpenApiOperation, OperationFilterContext, StatementsSwaggerDocumentation

### Community 138 - "MiniErp.Application.Features.Companies"
Cohesion: 0.29
Nodes (3): MiniErp.Application.Features.Companies, CompanyRequest, CompanyRequestValidator

### Community 139 - "IOperationFilter"
Cohesion: 0.18
Nodes (7): IOperationFilter, OpenApiOperation, OperationFilterContext, StockOpeningBalancesSwaggerDocumentation, OpenApiOperation, OperationFilterContext, StoreContainersSwaggerDocumentation

### Community 140 - ".CreateAsync"
Cohesion: 0.58
Nodes (3): Fact, Task, DriverTripCostServiceTests

### Community 141 - ".Apply"
Cohesion: 0.40
Nodes (3): OpenApiOperation, OperationFilterContext, UsersSwaggerDocumentation

### Community 142 - "ApplicationUser"
Cohesion: 0.24
Nodes (7): IdentityUser, Guid, ICollection, ApplicationUser, IdentityResult, IEnumerable, TestUserManager

### Community 143 - "PartnerOpeningBalanceAmountRules"
Cohesion: 0.40
Nodes (3): decimal, int, PartnerOpeningBalanceAmountRules

### Community 159 - "MiniErp.Infrastructure.Persistence.Migrations"
Cohesion: 0.33
Nodes (3): MiniErp.Infrastructure.Persistence.Migrations, MigrationBuilder, AddStockOpening

### Community 178 - ".CreateStandardClaims"
Cohesion: 0.38
Nodes (4): Claim, DateTimeOffset, IEnumerable, List

### Community 179 - "MiniErp.Application.Features.PartnerOpeningBalances"
Cohesion: 0.20
Nodes (3): MiniErp.Infrastructure.Services.PartnerOpeningBalances, MiniErp.Application.Features.PartnerOpeningBalances, MiniErp.Tests.PartnerOpeningBalances

### Community 180 - "DriverTripBulkCostUpdateRequest"
Cohesion: 0.33
Nodes (5): int, DriverTripBulkCostUpdateRequest, DriverTripCostUpdateItem, DriverTripBulkCostUpdateRequestValidator, DriverTripCostUpdateItemValidator

### Community 181 - "ItemsCategory"
Cohesion: 0.40
Nodes (4): ICollection, ItemsCategory, EntityTypeBuilder, ItemsCategoryConfiguration

### Community 183 - "ItemsCategoryRequest"
Cohesion: 0.22
Nodes (7): ItemsCategoryFilterRequest, int, ItemsCategoryRequest, ItemsCategoryUpdateRequest, ItemsCategoryFilterRequestValidator, ItemsCategoryRequestValidator, ItemsCategoryUpdateRequestValidator

### Community 184 - "CompanyAndExchangeRateAuthorizationTests"
Cohesion: 0.25
Nodes (5): MiniErp.Tests.Authorization, Fact, InlineData, Theory, CompanyAndExchangeRateAuthorizationTests

### Community 185 - "StockOpeningBalanceAmountRules"
Cohesion: 0.40
Nodes (3): decimal, int, StockOpeningBalanceAmountRules

### Community 186 - "Country"
Cohesion: 0.40
Nodes (3): Country, EntityTypeBuilder, CountryConfiguration

### Community 187 - ".Apply"
Cohesion: 0.40
Nodes (3): OpenApiOperation, OperationFilterContext, DriversSwaggerDocumentation

### Community 188 - "UserCreateRequest"
Cohesion: 0.33
Nodes (3): UserCreateRequest, UserCreateRequestValidator, UserFieldsValidator

### Community 189 - "StoreContainerUpsertRequest"
Cohesion: 0.40
Nodes (3): int, StoreContainerUpsertRequest, StoreContainerUpsertRequestValidator

### Community 190 - "CashVoucherUpdateRequest"
Cohesion: 0.40
Nodes (4): Expression, CashVoucherUpdateRequest, CashVoucherUpdateRequestValidator, CashVoucherValidationRules

### Community 191 - "Q: Cross-project MiniErp feature flow impact analysis"
Cohesion: 0.40
Nodes (4): Answer, Outcome, Q: Cross-project MiniErp feature flow impact analysis, Source Nodes

### Community 209 - "InventoryCountsSwaggerDocumentation.cs"
Cohesion: 0.40
Nodes (3): OpenApiOperation, OperationFilterContext, InventoryCountsSwaggerDocumentation

### Community 212 - "StockAdjustmentsSwaggerDocumentation.cs"
Cohesion: 0.40
Nodes (3): OpenApiOperation, OperationFilterContext, StockAdjustmentsSwaggerDocumentation

## Knowledge Gaps
- **96 isolated node(s):** `Asp.Versioning.Mvc (10.0.0)`, `Asp.Versioning.Mvc.ApiExplorer (10.0.0)`, `FluentValidation.DependencyInjectionExtensions (12.1.1)`, `Microsoft.EntityFrameworkCore.Design (10.0.10)`, `Microsoft.OpenApi (2.11.0)` (+91 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **58 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Result` connect `Result` to `StockOpeningBalanceService`, `PartnerOpeningBalanceServiceTests`, `StockAdjustmentService`, `BusinessPartnerService`, `AuthenticationService`, `CashMovementTypeService`, `ItemsCategoryService`, `CashboxService`, `.UpdateAsync`, `CountryService`, `ApiErrorResponseFactory`, `DriverService`, `InventoryCountService`, `MiniErp.Application.Features.Authentication`, `.Validation`, `.NotFound`, `Task`, `ItemService`, `UserService`, `IUserService`, `Error`, `CompanyService`, `ExchangeRateService`, `ItemUnitService`, `.AddAsync`, `StoreService`, `PaginationRequest`, `.PrepareAsync`, `Task`, `PagedResponse`, `.GetAsync`, `IScopedService`, `ICashboxService`, `.GetAllAsync`, `CashVoucherResponse`?**
  _High betweenness centrality (0.162) - this node is a cross-community bridge._
- **Why does `ApplicationDbContext` connect `ApplicationDbContext` to `InvoiceServiceTests`, `StockOpeningBalanceService`, `PartnerOpeningBalanceServiceTests`, `StockAdjustmentService`, `BusinessPartnerService`, `.CreateAsync`, `MiniErp.Domain.Entities.Companies`, `ApplicationUser`, `UserTestDatabase`, `Task`, `Company`, `MiniErp.Infrastructure.Persistence.Configurations`, `Task`, `Task`, `AuditableEntity`, `CurrencyCode`, `Task`, `Invoice`, `StockOpeningBalanceServiceTests`, `AccessTokenCompanyTestDatabase`, `.CreateAsync`, `ItemsCategory`, `Task`, `ItemMovement`, `Task`, `Country`, `IAsyncDisposable`, `RefreshToken`, `.CreateAsync`, `ExchangeRate`, `StockOpeningBalance`, `InventoryCount`, `ExchangeRateServiceTests`?**
  _High betweenness centrality (0.141) - this node is a cross-community bridge._
- **Why does `MiniErp.Domain.Enums` connect `MiniErp.Domain.Enums` to `InvoiceServiceTests`, `StockOpeningBalanceService`, `PartnerOpeningBalanceServiceTests`, `StockAdjustmentService`, `MiniErp.Application.Common.Results`, `MiniErp.Application.Features.StockOpeningBalances`, `MiniErp.Application.Features.Companies`, `MiniErp.Domain.Entities.Companies`, `.UpdateAsync`, `MiniErp.Application.Common.Abstractions`, `MiniErp.Application.Common.Models`, `AbstractValidator`, `MiniErp.Application.Features.Stores`, `InventoryStockService`, `CurrencyCode`, `Invoice`, `MiniErp.Application.Features.PartnerOpeningBalances`, `.AddAsync`, `PaginationRequest`, `ExchangeRate`, `MiniErp.Application.Features.ExchangeRates`, `.GetAsync`, `InventoryCostReportFilterRequest`, `.Apply`, `CashVoucherRequest`, `MiniErp.Application.Features.StockAdjustments`, `CashMasterServiceTests`, `CashManagementValidatorTests`?**
  _High betweenness centrality (0.090) - this node is a cross-community bridge._
- **What connects `Asp.Versioning.Mvc (10.0.0)`, `Asp.Versioning.Mvc.ApiExplorer (10.0.0)`, `FluentValidation.DependencyInjectionExtensions (12.1.1)` to the rest of the system?**
  _96 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `InvoiceServiceTests` be split into smaller, more focused modules?**
  _Cohesion score 0.05993793891883064 - nodes in this community are weakly interconnected._
- **Should `StockOpeningBalanceService` be split into smaller, more focused modules?**
  _Cohesion score 0.05025773195876289 - nodes in this community are weakly interconnected._
- **Should `PartnerOpeningBalanceServiceTests` be split into smaller, more focused modules?**
  _Cohesion score 0.054431960049937576 - nodes in this community are weakly interconnected._