using Bogus;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Domain.Entities.BusinessPartners;
using MiniErp.Domain.Entities.CashManagement;
using MiniErp.Domain.Entities.Catalog;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Entities.Containers;
using MiniErp.Domain.Entities.Employees;
using MiniErp.Domain.Entities.Inventory;
using MiniErp.Domain.Entities.Invoicing;
using MiniErp.Domain.Entities.Logistics;
using MiniErp.Domain.Entities.Payroll;
using MiniErp.Domain.Entities.ReferenceData;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Identity;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Services.Inventory;

namespace MiniErp.Infrastructure.Seeding;

public static class DevelopmentDataSeeder
{
    private const string SeedActor = "seed";

    private static readonly SeedUser[] SeedUsers =
    [
        new(
            "admin",
            "admin@minierp.local",
            ["Admin", "User"],
            "System",
            "Administrator"),
        new(
            "user",
            "user@minierp.local",
            ["User"],
            "Application",
            "User")
    ];

    private static readonly SeedCompany[] AdditionalSeedCompanies =
    [
        new(
            "MiniERP Trading Company",
            "Nasr City, Cairo",
            "54322",
            "456789124",
            "Ahmed Hassan",
            new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)),
        new(
            "MiniERP Distribution Company",
            "Smouha, Alexandria",
            "54323",
            "456789125",
            "Mona Ibrahim",
            new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc))
    ];

    private static readonly string[] DefaultItemUnitNames =
    [
        "Piece",
        "Box",
        "Pack",
        "Kilogram",
        "Liter",
        "Meter"
    ];

    private static readonly string[] DefaultItemsCategoryNames =
    [
        "General Items",
        "Local Items",
        "Export Items"
    ];

    private static readonly SeedStore[] DefaultStores =
    [
        new("MAIN", "Main Store"),
        new("SALES", "Sales Store"),
        new("RETURNS", "Returns Store")
    ];

    private static readonly SeedStore ContainerStore =
        new("CONTAINERS", "Container Store");

    private static readonly (int Count, decimal Weight, decimal Price)[]
        StockOpeningLineAmounts =
        [
            (10, 2.50m, 12.00m),
            (6, 4.00m, 15.50m),
            (20, 1.25m, 8.75m)
        ];

    private static readonly SeedCountry[] DefaultCountries =
    [
        new("EG", "\u0645\u0635\u0631", "Egypt"),
        new("SA", "\u0627\u0644\u0633\u0639\u0648\u062f\u064a\u0629", "Saudi Arabia"),
        new("AE", "\u0627\u0644\u0625\u0645\u0627\u0631\u0627\u062a", "United Arab Emirates"),
        new("US", "\u0627\u0644\u0648\u0644\u0627\u064a\u0627\u062a \u0627\u0644\u0645\u062a\u062d\u062f\u0629", "United States")
    ];

    private static readonly SeedContainer[] DefaultContainers =
    [
        new("CTN-001", "Small Crate", "Reusable small crate"),
        new("CTN-002", "Large Crate", "Reusable large crate"),
        new("CTN-003", "Pallet", "Reusable pallet")
    ];

    private static readonly SeedDriver[] DefaultDrivers =
    [
        new(
            "DRV-001",
            "Ahmed Ali",
            "123456",
            new DateOnly(2028, 12, 31)),
        new(
            "DRV-002",
            "Mahmoud Hassan",
            "234567",
            new DateOnly(2029, 6, 30)),
        new(
            "DRV-003",
            "Omar Ibrahim",
            "345678",
            new DateOnly(2029, 12, 31)),
        new(
            "DRV-004",
            "Youssef Adel",
            "456789",
            new DateOnly(2030, 3, 31)),
        new(
            "DRV-005",
            "Hassan Nabil",
            "567890",
            new DateOnly(2030, 6, 30)),
        new(
            "DRV-006",
            "Khaled Samir",
            "678901",
            new DateOnly(2030, 9, 30)),
        new(
            "DRV-007",
            "Mostafa Hany",
            "789012",
            new DateOnly(2030, 12, 31)),
        new(
            "DRV-008",
            "Tarek Fathy",
            "890123",
            new DateOnly(2031, 3, 31)),
        new(
            "DRV-009",
            "Ibrahim Sayed",
            "901234",
            new DateOnly(2031, 6, 30)),
        new(
            "DRV-010",
            "Amr Wael",
            "012345",
            new DateOnly(2031, 12, 31))
    ];

    private static readonly SeedBusinessPartner[] DefaultBusinessPartners =
    [
        new("BP-001", "Ahmed Mohamed Trading", "123456", CurrencyCode.EGP, 50_000m),
        new("BP-002", "Al Salam Supplies", "234567", CurrencyCode.USD, 75_000m),
        new("BP-003", "Nile Distribution", "345678", CurrencyCode.EGP, 100_000m),
        new("BP-004", "Delta Wholesale", "456789", CurrencyCode.EGP, 60_000m),
        new("BP-005", "Cairo Market", "567890", CurrencyCode.USD, 80_000m),
        new("BP-006", "Upper Egypt Supplies", "678901", CurrencyCode.EGP, 90_000m),
        new("BP-007", "Future Trade", "789012", CurrencyCode.EGP, 70_000m),
        new("BP-008", "Nile Valley Stores", "890123", CurrencyCode.USD, 85_000m),
        new("BP-009", "Al Waha Distribution", "901234", CurrencyCode.EGP, 65_000m),
        new("BP-010", "United Merchants", "012345", CurrencyCode.EGP, 110_000m)
    ];

    private static readonly SeedEmployee[] DefaultEmployees =
    [
        new(
            Key: "01",
            Name: "Ahmed Samir",
            JobTitle: "Accountant",
            Type: EmployeeType.Monthly,
            Salary: 8_000m),
        new(
            Key: "02",
            Name: "Mona Hassan",
            JobTitle: "Sales Specialist",
            Type: EmployeeType.Monthly,
            Salary: 9_000m),
        new(
            Key: "03",
            Name: "Karim Adel",
            JobTitle: "Store Keeper",
            Type: EmployeeType.Monthly,
            Salary: 7_500m),
        new(
            Key: "04",
            Name: "Salma Ibrahim",
            JobTitle: "HR Specialist",
            Type: EmployeeType.Monthly,
            Salary: 8_500m),
        new(
            Key: "05",
            Name: "Youssef Nabil",
            JobTitle: "Operations Supervisor",
            Type: EmployeeType.Monthly,
            Salary: 11_000m),
        new(
            Key: "06",
            Name: "Mahmoud Ali",
            JobTitle: "Warehouse Worker",
            Type: EmployeeType.Daily,
            Salary: 300m),
        new(
            Key: "07",
            Name: "Sara Hany",
            JobTitle: "Packing Worker",
            Type: EmployeeType.Daily,
            Salary: 275m),
        new(
            Key: "08",
            Name: "Omar Fathy",
            JobTitle: "Loading Worker",
            Type: EmployeeType.Daily,
            Salary: 325m),
        new(
            Key: "09",
            Name: "Nour Khaled",
            JobTitle: "Delivery Assistant",
            Type: EmployeeType.Daily,
            Salary: 290m),
        new(
            Key: "10",
            Name: "Mostafa Emad",
            JobTitle: "Maintenance Worker",
            Type: EmployeeType.Daily,
            Salary: 350m)
    ];

    public static async Task SeedAsync(
        IServiceProvider services,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var password = configuration["Seed:Password"];
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Seed:Password must be configured when Seed:Enabled is true.");
        }

        var itemCount = Math.Clamp(
            configuration.GetValue("Seed:ItemCount", 25),
            0,
            10_000);

        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        await SeedCountriesAsync(dbContext, cancellationToken);

        var primaryCompany = await SeedCompaniesAsync(
            dbContext,
            cancellationToken);
        var companies = new List<Company> { primaryCompany };
        companies.AddRange(await SeedAdditionalCompaniesAsync(
            dbContext,
            cancellationToken));

        await SeedCompanySettingsAsync(
            dbContext,
            companies,
            cancellationToken);

        await SeedExchangeRatesAsync(
            dbContext,
            companies,
            cancellationToken);

        await SeedIdentityAsync(
            dbContext,
            userManager,
            roleManager,
            password,
            companies,
            cancellationToken);

        foreach (var company in companies)
        {
            await SeedBusinessPartnersAsync(
                dbContext,
                company,
                cancellationToken);

            await SeedPartnerOpeningBalancesAsync(
                dbContext,
                company,
                cancellationToken);

            await SeedStoresAsync(
                dbContext,
                company,
                cancellationToken);

            await SeedContainerStoreAsync(
                dbContext,
                company,
                cancellationToken);

            await SeedContainersAsync(
                dbContext,
                company,
                cancellationToken);

            await SeedStoreContainersAsync(
                dbContext,
                company,
                cancellationToken);

            await SeedDriversAsync(
                dbContext,
                company,
                cancellationToken);

            await SeedCatalogAsync(
                dbContext,
                company.Id,
                itemCount,
                cancellationToken);

            await SeedItemsCategoriesAsync(
                dbContext,
                company.Id,
                cancellationToken);

            await SeedStockOpeningBalancesAsync(
                dbContext,
                company,
                cancellationToken);

            await SeedInvoicesAsync(
                dbContext,
                company,
                cancellationToken);

            await SeedNonSalesInvoicesAsync(
                dbContext,
                company,
                cancellationToken);

            await RecalculateSeedInventoryCostingAsync(
                dbContext,
                company.Id,
                cancellationToken);

            await SeedCashManagementAsync(
                dbContext,
                company,
                cancellationToken);

            await SeedEmployeesAsync(
                dbContext,
                company,
                cancellationToken);

            await SeedAttendanceAsync(
                dbContext,
                company,
                cancellationToken);

            var payrollPeriod = await SeedPayrollPeriodsAsync(
                dbContext,
                company,
                cancellationToken);

            await SeedPayrollEntriesAsync(
                dbContext,
                company,
                payrollPeriod,
                cancellationToken);

            await SeedEmployeeTransactionsAsync(
                dbContext,
                company,
                payrollPeriod,
                cancellationToken);

        }
    }

    private static async Task SeedCountriesAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var countryCodes = DefaultCountries
            .Select(country => country.Code)
            .ToArray();
        var existingCodes = (await dbContext.Countries
                .IgnoreQueryFilters()
                .Where(country => countryCodes.Contains(country.Code))
                .Select(country => country.Code)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var createdOn = DateTime.UtcNow;
        var createdByPc = Environment.MachineName;

        foreach (var seedCountry in DefaultCountries)
        {
            if (existingCodes.Contains(seedCountry.Code))
            {
                continue;
            }

            dbContext.Countries.Add(new Country
            {
                Code = seedCountry.Code,
                Name = seedCountry.Name,
                EnglishName = seedCountry.EnglishName,
                IsActive = true,
                CreatedById = SeedActor,
                CreatedByPc = createdByPc,
                CreatedOn = createdOn
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedIdentityAsync(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        string password,
        IReadOnlyList<Company> companies,
        CancellationToken cancellationToken)
    {
        foreach (var roleName in SeedUsers
                     .SelectMany(seedUser => seedUser.Roles)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var roleResult = await roleManager.CreateAsync(
                new IdentityRole<Guid>(roleName));
            EnsureSucceeded(roleResult, $"creating the '{roleName}' role");
        }

        foreach (var seedUser in SeedUsers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var user = await userManager.FindByNameAsync(seedUser.UserName);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = seedUser.UserName,
                    Email = seedUser.Email,
                    EmailConfirmed = true,
                    FirstName = seedUser.FirstName,
                    LastName = seedUser.LastName,
                    ProfileImage = string.Empty
                };

                var createResult = await userManager.CreateAsync(user, password);
                EnsureSucceeded(
                    createResult,
                    $"creating user '{seedUser.UserName}'");
            }
            else
            {
                user.Email = seedUser.Email;
                user.EmailConfirmed = true;
                user.FirstName = seedUser.FirstName;
                user.LastName = seedUser.LastName;
                user.ProfileImage = string.Empty;

                var updateResult = await userManager.UpdateAsync(user);
                EnsureSucceeded(
                    updateResult,
                    $"updating user '{seedUser.UserName}'");

                if (!await userManager.CheckPasswordAsync(user, password))
                {
                    var resetToken = await userManager
                        .GeneratePasswordResetTokenAsync(user);
                    var resetResult = await userManager.ResetPasswordAsync(
                        user,
                        resetToken,
                        password);
                    EnsureSucceeded(
                        resetResult,
                        $"resetting the password for '{seedUser.UserName}'");
                }
            }

            await SetRolesAsync(userManager, user, seedUser.Roles);

            var companyIds = seedUser.Roles.Contains(
                    "Admin",
                    StringComparer.OrdinalIgnoreCase)
                ? companies.Select(company => company.Id).ToArray()
                : [companies[0].Id];
            var assignedCompanyIds = await dbContext.UserCompanies
                .Where(userCompany =>
                    userCompany.UserId == user.Id &&
                    companyIds.Contains(userCompany.CompanyId))
                .Select(userCompany => userCompany.CompanyId)
                .ToHashSetAsync(cancellationToken);

            foreach (var companyId in companyIds.Except(assignedCompanyIds))
            {
                dbContext.UserCompanies.Add(new UserCompany
                {
                    UserId = user.Id,
                    CompanyId = companyId
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SetRolesAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationUser user,
        IReadOnlyCollection<string> roleNames)
    {
        var currentRoles = await userManager.GetRolesAsync(user);
        var rolesToRemove = currentRoles
            .Where(currentRole => !roleNames.Contains(
                currentRole,
                StringComparer.OrdinalIgnoreCase))
            .ToArray();

        if (rolesToRemove.Length > 0)
        {
            var removeResult = await userManager.RemoveFromRolesAsync(
                user,
                rolesToRemove);
            EnsureSucceeded(
                removeResult,
                $"removing old roles from '{user.UserName}'");
        }

        var rolesToAdd = roleNames
            .Where(roleName => !currentRoles.Contains(
                roleName,
                StringComparer.OrdinalIgnoreCase))
            .ToArray();
        if (rolesToAdd.Length > 0)
        {
            var addResult = await userManager.AddToRolesAsync(user, rolesToAdd);
            EnsureSucceeded(
                addResult,
                $"assigning roles to '{user.UserName}'");
        }
    }

    private static async Task SeedStoresAsync(
        ApplicationDbContext dbContext,
        Company company,
        CancellationToken cancellationToken)
    {
        var seedStoreCodes = DefaultStores
            .Select(seedStore => seedStore.Code)
            .ToArray();
        var existingCodes = await dbContext.Stores
            .IgnoreQueryFilters()
            .Where(store =>
                store.CompanyId == company.Id &&
                seedStoreCodes.Contains(store.Code))
            .Select(store => store.Code)
            .ToHashSetAsync(cancellationToken);
        var createdOn = DateTime.UtcNow;
        var createdByPc = Environment.MachineName;

        foreach (var seedStore in DefaultStores)
        {
            if (existingCodes.Contains(seedStore.Code))
            {
                continue;
            }

            dbContext.Stores.Add(new Store
            {
                CompanyId = company.Id,
                Code = seedStore.Code,
                Name = $"{seedStore.Name} - Company {company.Id}",
                Address = company.Address,
                IsActive = true,
                CreatedById = SeedActor,
                CreatedByPc = createdByPc,
                CreatedOn = createdOn
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedContainerStoreAsync(
        ApplicationDbContext dbContext,
        Company company,
        CancellationToken cancellationToken)
    {
        var existingStore = await dbContext.Stores
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                store =>
                    store.CompanyId == company.Id &&
                    store.Code == ContainerStore.Code,
                cancellationToken);
        if (existingStore is not null)
        {
            return;
        }

        var businessPartnerId = await dbContext.BusinessPartners
            .Where(partner =>
                partner.CompanyId == company.Id &&
                partner.IsActive &&
                !dbContext.Stores.Any(store =>
                    store.CompanyId == company.Id &&
                    store.BusinessPartnerId == partner.Id &&
                    store.IsContainerStore &&
                    store.IsActive))
            .OrderBy(partner => partner.Id)
            .Select(partner => (int?)partner.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (!businessPartnerId.HasValue)
        {
            return;
        }

        dbContext.Stores.Add(new Store
        {
            CompanyId = company.Id,
            Code = ContainerStore.Code,
            Name = $"{ContainerStore.Name} - Company {company.Id}",
            Address = company.Address,
            IsContainerStore = true,
            BusinessPartnerId = businessPartnerId.Value,
            IsActive = true,
            CreatedById = SeedActor,
            CreatedByPc = Environment.MachineName,
            CreatedOn = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedContainersAsync(
        ApplicationDbContext dbContext,
        Company company,
        CancellationToken cancellationToken)
    {
        var containerCodes = DefaultContainers
            .Select(container => container.Code)
            .ToArray();
        var existingCodes = (await dbContext.Containers
                .IgnoreQueryFilters()
                .Where(container =>
                    container.CompanyId == company.Id &&
                    containerCodes.Contains(container.Code))
                .Select(container => container.Code)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var createdOn = DateTime.UtcNow;
        var createdByPc = Environment.MachineName;

        foreach (var seedContainer in DefaultContainers)
        {
            if (existingCodes.Contains(seedContainer.Code))
            {
                continue;
            }

            dbContext.Containers.Add(new Container
            {
                CompanyId = company.Id,
                Code = seedContainer.Code,
                Name = $"{seedContainer.Name} - Company {company.Id}",
                Description = seedContainer.Description,
                IsActive = true,
                CreatedById = SeedActor,
                CreatedByPc = createdByPc,
                CreatedOn = createdOn
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedStoreContainersAsync(
        ApplicationDbContext dbContext,
        Company company,
        CancellationToken cancellationToken)
    {
        var containerStore = await dbContext.Stores
            .Where(store =>
                store.CompanyId == company.Id &&
                store.Code == ContainerStore.Code &&
                store.IsActive &&
                store.IsContainerStore)
            .Select(store => new { store.Id })
            .FirstOrDefaultAsync(cancellationToken);
        if (containerStore is null)
        {
            return;
        }

        var containerCodes = DefaultContainers
            .Select(container => container.Code)
            .ToArray();
        var containers = await dbContext.Containers
            .Where(container =>
                container.CompanyId == company.Id &&
                container.IsActive &&
                containerCodes.Contains(container.Code))
            .Select(container => new { container.Id })
            .ToListAsync(cancellationToken);
        if (containers.Count == 0)
        {
            return;
        }

        var containerIds = containers
            .Select(container => container.Id)
            .ToArray();
        var existingContainerIds = (await dbContext.StoreContainers
                .IgnoreQueryFilters()
                .Where(assignment =>
                    assignment.CompanyId == company.Id &&
                    assignment.StoreId == containerStore.Id &&
                    containerIds.Contains(assignment.ContainerId))
                .Select(assignment => assignment.ContainerId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        foreach (var containerId in containerIds.Where(
                     containerId => !existingContainerIds.Contains(containerId)))
        {
            dbContext.StoreContainers.Add(new StoreContainer
            {
                CompanyId = company.Id,
                StoreId = containerStore.Id,
                ContainerId = containerId,
                IsActive = true,
                CreatedById = SeedActor,
                CreatedByPc = Environment.MachineName,
                CreatedOn = DateTime.UtcNow
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedDriversAsync(
        ApplicationDbContext dbContext,
        Company company,
        CancellationToken cancellationToken)
    {
        var seedDriverCodes = DefaultDrivers
            .Select(seedDriver => seedDriver.Code)
            .ToArray();
        var existingCodes = (await dbContext.Drivers
                .IgnoreQueryFilters()
                .Where(driver =>
                    driver.CompanyId == company.Id &&
                    seedDriverCodes.Contains(driver.Code))
                .Select(driver => driver.Code)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var createdOn = DateTime.UtcNow;
        var createdByPc = Environment.MachineName;

        foreach (var seedDriver in DefaultDrivers)
        {
            if (existingCodes.Contains(seedDriver.Code))
            {
                continue;
            }

            dbContext.Drivers.Add(new Driver
            {
                CompanyId = company.Id,
                Code = seedDriver.Code,
                Name = $"{seedDriver.Name} - Company {company.Id}",
                PhoneNumber = $"010{company.Id % 100:00}{seedDriver.PhoneSuffix}",
                NationalId = $"NID-{company.Id:0000}-{seedDriver.Code}",
                LicenseNumber = $"LIC-{company.Id:0000}-{seedDriver.Code}",
                LicenseExpiryDate = seedDriver.LicenseExpiryDate,
                IsActive = true,
                CreatedById = SeedActor,
                CreatedByPc = createdByPc,
                CreatedOn = createdOn
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedBusinessPartnersAsync(
        ApplicationDbContext dbContext,
        Company company,
        CancellationToken cancellationToken)
    {
        var seedPartnerCodes = DefaultBusinessPartners
            .Select(seedPartner => seedPartner.Code)
            .ToArray();
        var existingCodes = (await dbContext.BusinessPartners
                .IgnoreQueryFilters()
                .Where(partner =>
                    partner.CompanyId == company.Id &&
                    seedPartnerCodes.Contains(partner.Code))
                .Select(partner => partner.Code)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var createdOn = DateTime.UtcNow;
        var createdByPc = Environment.MachineName;

        foreach (var seedPartner in DefaultBusinessPartners)
        {
            if (existingCodes.Contains(seedPartner.Code))
            {
                continue;
            }

            dbContext.BusinessPartners.Add(new BusinessPartner
            {
                CompanyId = company.Id,
                Code = seedPartner.Code,
                Name = $"{seedPartner.Name} - Company {company.Id}",
                PhoneNumber = $"010{company.Id % 100:00}{seedPartner.PhoneSuffix}",
                Email = $"{seedPartner.Code.ToLowerInvariant()}.company{company.Id}@minierp.local",
                Address = company.Address,
                TaxNumber = $"TAX-{company.Id:0000}-{seedPartner.Code}",
                Currency = seedPartner.Currency,
                CreditLimit = seedPartner.CreditLimit,
                IsActive = true,
                CreatedById = SeedActor,
                CreatedByPc = createdByPc,
                CreatedOn = createdOn
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedCatalogAsync(
        ApplicationDbContext dbContext,
        int companyId,
        int itemCount,
        CancellationToken cancellationToken)
    {
        var itemUnits = await dbContext.ItemUnits
            .Where(itemUnit =>
                itemUnit.CompanyId == companyId &&
                DefaultItemUnitNames.Contains(itemUnit.Name))
            .ToListAsync(cancellationToken);

        var itemUnitsByName = itemUnits.ToDictionary(
            itemUnit => itemUnit.Name,
            StringComparer.OrdinalIgnoreCase);
        var createdOn = DateTime.UtcNow;
        var createdByPc = Environment.MachineName;

        foreach (var unitName in DefaultItemUnitNames)
        {
            if (itemUnitsByName.ContainsKey(unitName))
            {
                continue;
            }

            var itemUnit = new ItemUnit
            {
                CompanyId = companyId,
                Name = unitName,
                IsActive = true,
                CreatedById = SeedActor,
                CreatedByPc = createdByPc,
                CreatedOn = createdOn
            };

            dbContext.ItemUnits.Add(itemUnit);
            itemUnitsByName.Add(unitName, itemUnit);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (itemCount == 0)
        {
            return;
        }

        var existingItemCodes = await dbContext.Items
            .IgnoreQueryFilters()
            .Where(item =>
                item.CompanyId == companyId &&
                item.Code.StartsWith("ITEM-"))
            .Select(item => item.Code)
            .ToHashSetAsync(cancellationToken);

        var availableItemUnits = DefaultItemUnitNames
            .Select(unitName => itemUnitsByName[unitName])
            .Where(itemUnit => itemUnit.IsActive)
            .ToArray();

        if (availableItemUnits.Length == 0)
        {
            throw new InvalidOperationException(
                "At least one active item unit is required to seed items.");
        }

        var itemFaker = new Faker<Item>("en")
            .RuleFor(item => item.CompanyId, _ => companyId)
            .RuleFor(item => item.Code, faker => $"ITEM-{faker.IndexFaker + 1:0000}")
            .RuleFor(
                item => item.Name,
                faker => $"Company {companyId} - {faker.Commerce.ProductName()}")
            .RuleFor(
                item => item.Description,
                faker => $"Company {companyId}: {faker.Commerce.ProductDescription()}")
            .RuleFor(item => item.IsActive, _ => true)
            .RuleFor(item => item.CreatedById, _ => SeedActor)
            .RuleFor(item => item.CreatedByPc, _ => createdByPc)
            .RuleFor(item => item.CreatedOn, _ => createdOn)
            .UseSeed(20260719 + companyId);

        var generatedItems = itemFaker.Generate(itemCount);

        for (var index = 0; index < generatedItems.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var item = generatedItems[index];

            if (existingItemCodes.Contains(item.Code))
            {
                continue;
            }

            var itemUnit = availableItemUnits[index % availableItemUnits.Length];
            item.ItemUnitId = itemUnit.Id;
            item.ItemUnit = itemUnit;

            dbContext.Items.Add(item);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedPartnerOpeningBalancesAsync(
        ApplicationDbContext dbContext,
        Company company,
        CancellationToken cancellationToken)
    {
        var seedBalances = new[]
        {
            new SeedPartnerOpeningBalance(
                PartnerCode: "BP-001",
                DocumentNumber: "PARTNER-OPEN-001",
                BalanceType: PartnerBalanceType.Receivable,
                Amount: 2_500m),
            new SeedPartnerOpeningBalance(
                PartnerCode: "BP-002",
                DocumentNumber: "PARTNER-OPEN-002",
                BalanceType: PartnerBalanceType.Payable,
                Amount: 1_750m),
            new SeedPartnerOpeningBalance(
                PartnerCode: "BP-003",
                DocumentNumber: "PARTNER-OPEN-003",
                BalanceType: PartnerBalanceType.Receivable,
                Amount: 3_250m),
            new SeedPartnerOpeningBalance(
                PartnerCode: "BP-004",
                DocumentNumber: "PARTNER-OPEN-004",
                BalanceType: PartnerBalanceType.Payable,
                Amount: 2_250m),
            new SeedPartnerOpeningBalance(
                PartnerCode: "BP-005",
                DocumentNumber: "PARTNER-OPEN-005",
                BalanceType: PartnerBalanceType.Receivable,
                Amount: 4_000m),
            new SeedPartnerOpeningBalance(
                PartnerCode: "BP-006",
                DocumentNumber: "PARTNER-OPEN-006",
                BalanceType: PartnerBalanceType.Payable,
                Amount: 2_750m),
            new SeedPartnerOpeningBalance(
                PartnerCode: "BP-007",
                DocumentNumber: "PARTNER-OPEN-007",
                BalanceType: PartnerBalanceType.Receivable,
                Amount: 4_750m),
            new SeedPartnerOpeningBalance(
                PartnerCode: "BP-008",
                DocumentNumber: "PARTNER-OPEN-008",
                BalanceType: PartnerBalanceType.Payable,
                Amount: 3_250m),
            new SeedPartnerOpeningBalance(
                PartnerCode: "BP-009",
                DocumentNumber: "PARTNER-OPEN-009",
                BalanceType: PartnerBalanceType.Receivable,
                Amount: 5_500m),
            new SeedPartnerOpeningBalance(
                PartnerCode: "BP-010",
                DocumentNumber: "PARTNER-OPEN-010",
                BalanceType: PartnerBalanceType.Payable,
                Amount: 3_750m)
        };

        var partners = await dbContext.BusinessPartners
            .Where(partner =>
                partner.CompanyId == company.Id &&
                DefaultBusinessPartners
                    .Select(seed => seed.Code)
                    .Contains(partner.Code))
            .Select(partner => new
            {
                partner.Id,
                partner.Code,
                partner.Currency
            })
            .ToListAsync(cancellationToken);
        var partnersByCode = partners.ToDictionary(partner => partner.Code);
        var documentNumbers = seedBalances
            .Select(seed => seed.DocumentNumber)
            .ToArray();
        var existingBalances = await dbContext.PartnerOpeningBalances
            .IgnoreQueryFilters()
            .Where(balance =>
                balance.CompanyId == company.Id &&
                documentNumbers.Contains(balance.DocumentNumber))
            .OrderBy(balance => balance.IsDeleted)
            .ThenBy(balance => balance.Id)
            .ToListAsync(cancellationToken);
        var existingByDocumentNumber = existingBalances
            .GroupBy(balance => balance.DocumentNumber)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (var seed in seedBalances)
        {
            if (!partnersByCode.TryGetValue(seed.PartnerCode, out var partner))
            {
                continue;
            }

            var notes = seed.BalanceType == PartnerBalanceType.Receivable
                ? "رصيد افتتاحي تجريبي مطلوب من العميل أو المورد"
                : "رصيد افتتاحي تجريبي مطلوب دفعه للعميل أو المورد";
            if (existingByDocumentNumber.TryGetValue(
                    seed.DocumentNumber,
                    out var existingBalance))
            {
                existingBalance.IsDeleted = false;
                existingBalance.DeletedById = null;
                existingBalance.DeletedOn = null;
                existingBalance.DeletedByPc = null;
                existingBalance.BusinessPartnerId = partner.Id;
                existingBalance.DocumentDate = new DateOnly(2026, 1, 1);
                existingBalance.Currency = partner.Currency;
                existingBalance.BalanceType = seed.BalanceType;
                existingBalance.Amount = seed.Amount;
                existingBalance.Notes = notes;
                await ApplyPartnerOpeningBalanceExchangeRateAsync(
                    dbContext,
                    company.Id,
                    existingBalance,
                    cancellationToken);
                continue;
            }

            var openingBalance = new PartnerOpeningBalance
            {
                CompanyId = company.Id,
                BusinessPartnerId = partner.Id,
                DocumentNumber = seed.DocumentNumber,
                DocumentDate = new DateOnly(2026, 1, 1),
                Currency = partner.Currency,
                BalanceType = seed.BalanceType,
                Amount = seed.Amount,
                Notes = notes
            };
            await ApplyPartnerOpeningBalanceExchangeRateAsync(
                dbContext,
                company.Id,
                openingBalance,
                cancellationToken);

            dbContext.PartnerOpeningBalances.Add(openingBalance);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedStockOpeningBalancesAsync(
        ApplicationDbContext dbContext,
        Company company,
        CancellationToken cancellationToken)
    {
        const string documentNumber = "OPEN-001";
        var seedNotes = $"Seed draft for Company {company.Id}";

        var existingBalance = await dbContext.StockOpeningBalances
            .IgnoreQueryFilters()
            .Include(balance => balance.Lines)
            .FirstOrDefaultAsync(
                balance =>
                    balance.CompanyId == company.Id &&
                    balance.DocumentNumber == documentNumber,
                cancellationToken);
        if (existingBalance is not null)
        {
            if (existingBalance.IsDeleted ||
                existingBalance.Notes != seedNotes)
            {
                return;
            }

            var existingSeedLines = existingBalance.Lines
                .Where(line =>
                    !line.IsDeleted &&
                    line.Notes == "Seed draft line")
                .OrderBy(line => line.Id)
                .Take(StockOpeningLineAmounts.Length)
                .ToList();

            for (var index = 0; index < existingSeedLines.Count; index++)
            {
                ApplyStockOpeningSeedAmounts(
                    existingSeedLines[index],
                    index);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await EnsureOpeningBalanceMovementsAsync(
                dbContext,
                existingBalance,
                cancellationToken);
            return;
        }

        var store = await dbContext.Stores
            .Where(store =>
                store.CompanyId == company.Id &&
                store.Code == "MAIN" &&
                store.IsActive &&
                !store.IsContainerStore)
            .Select(store => new { store.Id })
            .FirstOrDefaultAsync(cancellationToken);
        if (store is null)
        {
            return;
        }

        var items = await dbContext.Items
            .Where(item =>
                item.CompanyId == company.Id &&
                item.IsActive &&
                item.ItemUnit.IsActive)
            .OrderBy(item => item.Id)
            .Take(3)
            .Select(item => new
            {
                item.Id,
                item.ItemUnitId
            })
            .ToListAsync(cancellationToken);
        if (items.Count == 0)
        {
            return;
        }

        var balance = new StockOpeningBalance
        {
            CompanyId = company.Id,
            StoreId = store.Id,
            DocumentNumber = documentNumber,
            DocumentDate = new DateOnly(2026, 1, 1),
            Notes = seedNotes
        };

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var line = new StockOpeningBalanceLine
            {
                CompanyId = company.Id,
                ItemId = item.Id,
                ItemUnitId = item.ItemUnitId,
                Notes = "Seed draft line"
            };
            ApplyStockOpeningSeedAmounts(line, index);
            balance.Lines.Add(line);
        }

        dbContext.StockOpeningBalances.Add(balance);
        await dbContext.SaveChangesAsync(cancellationToken);
        await EnsureOpeningBalanceMovementsAsync(
            dbContext,
            balance,
            cancellationToken);
    }

    private static async Task SeedItemsCategoriesAsync(
        ApplicationDbContext dbContext,
        int companyId,
        CancellationToken cancellationToken)
    {
        var existingNames = (await dbContext.ItemsCategories
            .IgnoreQueryFilters()
            .Where(category => category.CompanyId == companyId)
            .Select(category => category.Name)
            .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var name in DefaultItemsCategoryNames)
        {
            if (existingNames.Contains(name))
            {
                continue;
            }

            dbContext.ItemsCategories.Add(
                new ItemsCategory
                {
                    CompanyId = companyId,
                    Name = name,
                    IsActive = true,
                    Notes = $"Development seed category for Company {companyId}"
                });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureOpeningBalanceMovementsAsync(
        ApplicationDbContext dbContext,
        StockOpeningBalance balance,
        CancellationToken cancellationToken)
    {
        var existingMovements = await dbContext.ItemMovements
            .Where(movement =>
                movement.CompanyId == balance.CompanyId &&
                movement.MovementType == ItemMovementType.OpeningBalance &&
                movement.ReferenceId == balance.Id)
            .ToListAsync(cancellationToken);
        var existing = existingMovements.ToDictionary(
            movement => movement.ItemId);

        foreach (var line in balance.Lines.Where(line => !line.IsDeleted))
        {
            if (existing.TryGetValue(line.ItemId, out var movement))
            {
                movement.StoreId = balance.StoreId;
                movement.ItemUnitId = line.ItemUnitId;
                movement.ReferenceNumber = balance.DocumentNumber;
                movement.MovementDate = balance.DocumentDate;
                movement.QuantityIn = line.Quantity;
                movement.QuantityOut = 0m;
                continue;
            }

            dbContext.ItemMovements.Add(
                new ItemMovement
                {
                    CompanyId = balance.CompanyId,
                    StoreId = balance.StoreId,
                    ItemId = line.ItemId,
                    ItemUnitId = line.ItemUnitId,
                    MovementType = ItemMovementType.OpeningBalance,
                    ReferenceId = balance.Id,
                    ReferenceNumber = balance.DocumentNumber,
                    MovementDate = balance.DocumentDate,
                    QuantityIn = line.Quantity,
                    QuantityOut = 0m,
                    Description =
                        $"Opening balance {balance.DocumentNumber}"
                });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task RecalculateSeedInventoryCostingAsync(
        ApplicationDbContext dbContext,
        int companyId,
        CancellationToken cancellationToken)
    {
        var openingBalanceKeys = await dbContext.StockOpeningBalanceLines
            .AsNoTracking()
            .Where(line =>
                line.CompanyId == companyId &&
                line.StockOpeningBalance.DocumentNumber == "OPEN-001" &&
                line.StockOpeningBalance.Notes ==
                    $"Seed draft for Company {companyId}")
            .Select(line => new InventoryCostingKey(
                line.StockOpeningBalance.StoreId,
                line.ItemId))
            .ToListAsync(cancellationToken);
        var invoiceKeys = await dbContext.InvoiceLines
            .AsNoTracking()
            .Where(line =>
                line.CompanyId == companyId &&
                line.ItemId.HasValue &&
                line.Invoice.ExportInvoiceCode != null &&
                line.Invoice.ExportInvoiceCode.StartsWith("SEED-"))
            .Select(line => new InventoryCostingKey(
                line.Invoice.StoreId,
                line.ItemId!.Value))
            .ToListAsync(cancellationToken);
        var keys = openingBalanceKeys
            .Concat(invoiceKeys)
            .Distinct()
            .ToArray();
        if (keys.Length == 0)
        {
            return;
        }

        var service = new InventoryCostingService(
            dbContext,
            new SeedCompanyContext(companyId),
            TimeProvider.System);
        var error = await service.RecalculateAsync(
            keys,
            cancellationToken);
        if (error is not null)
        {
            throw new InvalidOperationException(
                $"Development inventory costing seed failed: {error.Code} - {error.Description}");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedInvoicesAsync(
        ApplicationDbContext dbContext,
        Company company,
        CancellationToken cancellationToken)
    {
        var seedDefinitions = new List<(
            string ExportCode,
            string InvoiceNumber,
            PaymentTerm PaymentTerm,
            DateOnly InvoiceDate,
            DateOnly? DueDate,
            int Sequence)>
        {
            (
                ExportCode: "SEED-CASH",
                InvoiceNumber: $"SEED-{company.Id}-CASH",
                PaymentTerm: PaymentTerm.Cash,
                InvoiceDate: new DateOnly(2026, 7, 25),
                DueDate: null,
                Sequence: 0),
            (
                ExportCode: "SEED-CREDIT",
                InvoiceNumber: $"SEED-{company.Id}-CREDIT",
                PaymentTerm: PaymentTerm.Credit,
                InvoiceDate: new DateOnly(2026, 7, 25),
                DueDate: new DateOnly(2026, 8, 24),
                Sequence: 0)
        };
        seedDefinitions.AddRange(
            Enumerable.Range(1, 48)
                .Select(index =>
                    (
                        ExportCode: $"SEED-{index:000}",
                        InvoiceNumber: $"SEED-{company.Id}-{index:000}",
                        PaymentTerm: index <= 24
                            ? PaymentTerm.Cash
                            : PaymentTerm.Credit,
                        InvoiceDate: new DateOnly(2026, 7, 25)
                            .AddDays(index),
                        DueDate: index <= 24
                            ? (DateOnly?)null
                            : new DateOnly(2026, 7, 25)
                                .AddDays(index + 30),
                        Sequence: index)));

        var seedByExportCode = seedDefinitions
            .ToDictionary(seed => seed.ExportCode);
        var seedExportCodes = seedDefinitions
            .Select(seed => seed.ExportCode)
            .ToArray();

        var store = await dbContext.Stores
            .Where(candidate =>
                candidate.CompanyId == company.Id &&
                candidate.Code == "MAIN" &&
                candidate.IsActive &&
                !candidate.IsContainerStore)
            .Select(candidate => new { candidate.Id })
            .FirstOrDefaultAsync(cancellationToken);
        var containerStore = await dbContext.Stores
            .Where(candidate =>
                candidate.CompanyId == company.Id &&
                candidate.Code == ContainerStore.Code &&
                candidate.IsActive &&
                candidate.IsContainerStore &&
                candidate.BusinessPartnerId.HasValue)
            .Select(candidate => new
            {
                candidate.Id,
                BusinessPartnerId = candidate.BusinessPartnerId!.Value
            })
            .FirstOrDefaultAsync(cancellationToken);
        var partner = containerStore is null
            ? null
            : await dbContext.BusinessPartners
                .Where(candidate =>
                    candidate.CompanyId == company.Id &&
                    candidate.Id == containerStore.BusinessPartnerId &&
                    candidate.IsActive)
                .Select(candidate => new
                {
                    candidate.Id,
                    candidate.Currency
                })
                .FirstOrDefaultAsync(cancellationToken);
        var drivers = await dbContext.Drivers
            .Where(driver =>
                driver.CompanyId == company.Id &&
                driver.IsActive)
            .OrderBy(driver => driver.Id)
            .Select(driver => new
            {
                driver.Id,
                driver.Name
            })
            .ToListAsync(cancellationToken);
        var containers = await dbContext.Containers
            .Where(container =>
                container.CompanyId == company.Id &&
                container.IsActive &&
                DefaultContainers
                    .Select(seedContainer => seedContainer.Code)
                    .Contains(container.Code))
            .OrderBy(container => container.Id)
            .Select(container => new { container.Id })
            .ToListAsync(cancellationToken);
        var items = await dbContext.Items
            .Where(candidate =>
                candidate.CompanyId == company.Id &&
                candidate.IsActive &&
                candidate.ItemUnit.IsActive)
            .OrderBy(candidate => candidate.Id)
            .Take(3)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.ItemUnitId
            })
            .ToListAsync(cancellationToken);
        var itemsCategoryId = await dbContext.ItemsCategories
            .Where(category =>
                category.CompanyId == company.Id &&
                category.Name == DefaultItemsCategoryNames[0] &&
                category.IsActive)
            .Select(category => (int?)category.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (partner is null ||
            store is null ||
            containerStore is null ||
            drivers.Count == 0 ||
            containers.Count == 0 ||
            items.Count == 0)
        {
            return;
        }

        var existingInvoices = await dbContext.Invoices
            .IgnoreQueryFilters()
            .Where(invoice =>
                invoice.CompanyId == company.Id &&
                seedExportCodes.Contains(invoice.ExportInvoiceCode!))
            .Include(invoice => invoice.Lines)
            .Include(invoice => invoice.ContainerLines)
            .OrderBy(invoice => invoice.IsDeleted)
            .ThenBy(invoice => invoice.Id)
            .ToListAsync(cancellationToken);
        var existingByExportCode = existingInvoices
            .GroupBy(invoice => invoice.ExportInvoiceCode!)
            .ToDictionary(group => group.Key, group => group.First());
        var createdOn = DateTime.UtcNow;
        var createdByPc = Environment.MachineName;

        foreach (var seed in seedDefinitions)
        {
            if (existingByExportCode.TryGetValue(
                    seed.ExportCode,
                    out var existingInvoice))
            {
                existingInvoice.IsDeleted = false;
                existingInvoice.DeletedById = null;
                existingInvoice.DeletedOn = null;
                existingInvoice.DeletedByPc = null;
                continue;
            }

            var driver = drivers[seed.Sequence % drivers.Count];
            var item = items[seed.Sequence % items.Count];
            var container = containers[seed.Sequence % containers.Count];
            var invoice = new Invoice
            {
                CompanyId = company.Id,
                InvoiceNumber = seed.InvoiceNumber,
                ExportInvoiceCode = seed.ExportCode,
                InvoiceType = InvoiceType.Sales,
                PaymentTerm = seed.PaymentTerm,
                InvoiceDate = seed.InvoiceDate,
                DueDate = seed.DueDate,
                BusinessPartnerId = partner.Id,
                StoreId = store.Id,
                ContainerStoreId = containerStore.Id,
                ItemsCategoryId = itemsCategoryId,
                Currency = partner.Currency,
                DriverId = driver.Id,
                ActualDriverName = driver.Name,
                Notes = $"Seed {seed.PaymentTerm} invoice for Company {company.Id}",
                CreatedById = SeedActor,
                CreatedByPc = createdByPc,
                CreatedOn = createdOn
            };
            var line = new InvoiceLine
            {
                CompanyId = company.Id,
                ItemId = item.Id,
                ItemUnitId = item.ItemUnitId,
                Count = 1,
                Weight = 1m,
                Price = 100m + (seed.Sequence % 5 * 25m),
                Notes = "Development seed invoice line",
                CreatedById = SeedActor,
                CreatedByPc = createdByPc,
                CreatedOn = createdOn
            };
            line.CalculateAmounts();
            invoice.Lines.Add(line);

            invoice.ContainerLines.Add(
                new InvoiceContainerLine
                {
                    CompanyId = company.Id,
                    ContainerId = container.Id,
                    OutgoingUnits = 1,
                    IncomingUnits = 0,
                    CreatedById = SeedActor,
                    CreatedByPc = createdByPc,
                    CreatedOn = createdOn
                });

            invoice.CalculateTotal();
            if (invoice.PaymentTerm == PaymentTerm.Cash)
            {
                invoice.PaidAmount = invoice.Total;
            }

            if (invoice.Currency == CurrencyCode.EGP)
            {
                invoice.ApplyExchangeRate(null, 1m);
            }
            else
            {
                var invoiceRate = await dbContext.ExchangeRates
                    .Where(candidate =>
                        candidate.CompanyId == company.Id &&
                        candidate.Currency == invoice.Currency &&
                        candidate.RateDate <= invoice.InvoiceDate)
                    .OrderByDescending(candidate => candidate.RateDate)
                    .ThenByDescending(candidate => candidate.Id)
                    .FirstAsync(cancellationToken);
                invoice.ApplyExchangeRate(
                    invoiceRate.Id,
                    invoiceRate.Rate);
            }

            invoice.Touch(createdOn);
            dbContext.Invoices.Add(invoice);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var seededInvoices = await dbContext.Invoices
            .IgnoreQueryFilters()
            .Include(invoice => invoice.Lines)
            .Include(invoice => invoice.ContainerLines)
            .Where(invoice =>
                invoice.CompanyId == company.Id &&
                seedExportCodes.Contains(invoice.ExportInvoiceCode!))
            .ToListAsync(cancellationToken);

        foreach (var invoice in seededInvoices)
        {
            if (!seedByExportCode.TryGetValue(
                    invoice.ExportInvoiceCode ?? string.Empty,
                    out var seed))
            {
                continue;
            }

            var driver = drivers[seed.Sequence % drivers.Count];
            var item = items[seed.Sequence % items.Count];
            var container = containers[seed.Sequence % containers.Count];
            var invoiceChanged = false;

            invoice.IsDeleted = false;
            invoice.DeletedById = null;
            invoice.DeletedOn = null;
            invoice.DeletedByPc = null;
            invoice.InvoiceNumber = seed.InvoiceNumber;
            invoice.InvoiceType = InvoiceType.Sales;
            invoice.PaymentTerm = seed.PaymentTerm;
            invoice.InvoiceDate = seed.InvoiceDate;
            invoice.DueDate = seed.DueDate;
            invoice.BusinessPartnerId = partner.Id;
            invoice.StoreId = store.Id;
            invoice.Currency = partner.Currency;

            var invoiceLine = invoice.Lines
                .OrderBy(line => line.IsDeleted)
                .ThenBy(line => line.Id)
                .FirstOrDefault();
            if (invoiceLine is null)
            {
                invoiceLine = new InvoiceLine
                {
                    CompanyId = company.Id,
                    CreatedById = SeedActor,
                    CreatedByPc = createdByPc,
                    CreatedOn = createdOn
                };
                invoice.Lines.Add(invoiceLine);
            }

            invoiceLine.IsDeleted = false;
            invoiceLine.DeletedById = null;
            invoiceLine.DeletedOn = null;
            invoiceLine.DeletedByPc = null;
            invoiceLine.ItemId = item.Id;
            invoiceLine.ItemUnitId = item.ItemUnitId;
            invoiceLine.SourceInvoiceLineId = null;
            invoiceLine.Count = 1;
            invoiceLine.Weight = 1m;
            invoiceLine.Price = 100m + (seed.Sequence % 5 * 25m);
            invoiceLine.Notes = "Development seed invoice line";
            invoiceLine.CalculateAmounts();

            if (!invoice.ItemsCategoryId.HasValue)
            {
                invoice.ItemsCategoryId = itemsCategoryId;
                invoiceChanged = true;
            }

            if (invoice.ContainerStoreId != containerStore.Id)
            {
                invoice.ContainerStoreId = containerStore.Id;
                invoiceChanged = true;
            }

            if (invoice.DriverId != driver.Id)
            {
                invoice.DriverId = driver.Id;
                invoiceChanged = true;
            }

            if (invoice.ActualDriverName != driver.Name ||
                invoice.UsesExternalDriver ||
                invoice.ExternalDriverName is not null)
            {
                invoice.ActualDriverName = driver.Name;
                invoice.UsesExternalDriver = false;
                invoice.ExternalDriverName = null;
                invoiceChanged = true;
            }

            var invoiceContainerLine = invoice.ContainerLines
                .OrderBy(line => line.IsDeleted)
                .ThenBy(line => line.Id)
                .FirstOrDefault();
            if (invoiceContainerLine is null)
            {
                invoiceContainerLine = new InvoiceContainerLine
                {
                    CompanyId = company.Id,
                    CreatedById = SeedActor,
                    CreatedByPc = createdByPc,
                    CreatedOn = createdOn
                };
                invoice.ContainerLines.Add(invoiceContainerLine);
            }

            invoiceContainerLine.IsDeleted = false;
            invoiceContainerLine.DeletedById = null;
            invoiceContainerLine.DeletedOn = null;
            invoiceContainerLine.DeletedByPc = null;
            invoiceContainerLine.ContainerId = container.Id;
            invoiceContainerLine.OutgoingUnits = 1;
            invoiceContainerLine.IncomingUnits = 0;
            invoiceChanged = true;

            invoice.CalculateTotal();
            invoice.PaidAmount = invoice.PaymentTerm == PaymentTerm.Cash
                ? invoice.Total
                : 0m;
            await ApplySeedInvoiceExchangeRateAsync(
                dbContext,
                company.Id,
                invoice,
                cancellationToken);

            if (invoiceChanged)
            {
                invoice.Touch(createdOn);
            }

            foreach (var containerLine in invoice.ContainerLines.Where(
                         line => !line.IsDeleted))
            {
                var containerMovement = await dbContext.ContainerMovements
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(movement =>
                        movement.CompanyId == company.Id &&
                        movement.InvoiceId == invoice.Id &&
                        movement.ContainerId == containerLine.ContainerId,
                        cancellationToken);
                if (containerMovement is null)
                {
                    dbContext.ContainerMovements.Add(
                        new ContainerMovement
                        {
                            CompanyId = company.Id,
                            BusinessPartnerId = invoice.BusinessPartnerId,
                            ContainerStoreId = invoice.ContainerStoreId!.Value,
                            ContainerId = containerLine.ContainerId,
                            InvoiceId = invoice.Id,
                            InvoiceNumber = invoice.InvoiceNumber,
                            MovementDate = invoice.InvoiceDate,
                            OutgoingUnits = containerLine.OutgoingUnits,
                            IncomingUnits = containerLine.IncomingUnits,
                            Description = $"Invoice {invoice.InvoiceNumber}",
                            CreatedById = SeedActor,
                            CreatedByPc = createdByPc,
                            CreatedOn = createdOn
                        });
                }
                else
                {
                    containerMovement.IsDeleted = false;
                    containerMovement.DeletedById = null;
                    containerMovement.DeletedOn = null;
                    containerMovement.DeletedByPc = null;
                    containerMovement.BusinessPartnerId =
                        invoice.BusinessPartnerId;
                    containerMovement.ContainerStoreId =
                        invoice.ContainerStoreId!.Value;
                    containerMovement.InvoiceNumber = invoice.InvoiceNumber;
                    containerMovement.MovementDate = invoice.InvoiceDate;
                    containerMovement.OutgoingUnits =
                        containerLine.OutgoingUnits;
                    containerMovement.IncomingUnits =
                        containerLine.IncomingUnits;
                    containerMovement.Description =
                        $"Invoice {invoice.InvoiceNumber}";
                }
            }

            var itemMovements = await dbContext.ItemMovements
                .IgnoreQueryFilters()
                .Where(movement =>
                    movement.CompanyId == company.Id &&
                    movement.ReferenceId == invoice.Id)
                .OrderBy(movement => movement.IsDeleted)
                .ThenBy(movement => movement.Id)
                .ToListAsync(cancellationToken);
            var itemMovementType =
                InvoiceMovementRules.GetItemMovementType(
                    invoice.InvoiceType);
            var inbound = InvoiceMovementRules.IsInbound(
                invoice.InvoiceType);

            foreach (var line in invoice.Lines.Where(line =>
                         !line.IsDeleted && line.ItemId.HasValue))
            {
                if (line.ItemId is not int lineItemId)
                {
                    continue;
                }

                var itemMovement = itemMovements.FirstOrDefault(movement =>
                    movement.ItemId == lineItemId);
                if (itemMovement is null)
                {
                    itemMovement = new ItemMovement
                    {
                        CompanyId = company.Id,
                        ReferenceId = invoice.Id,
                        ItemId = lineItemId,
                        CreatedById = SeedActor,
                        CreatedByPc = createdByPc,
                        CreatedOn = createdOn
                    };
                    dbContext.ItemMovements.Add(itemMovement);
                }

                itemMovement.IsDeleted = false;
                itemMovement.DeletedById = null;
                itemMovement.DeletedOn = null;
                itemMovement.DeletedByPc = null;
                itemMovement.StoreId = invoice.StoreId;
                itemMovement.ItemUnitId = line.ItemUnitId;
                itemMovement.MovementType = itemMovementType;
                itemMovement.ReferenceNumber = invoice.InvoiceNumber;
                itemMovement.MovementDate = invoice.InvoiceDate;
                itemMovement.QuantityIn = inbound ? line.Quantity : 0m;
                itemMovement.QuantityOut = inbound ? 0m : line.Quantity;
                itemMovement.Description =
                    $"Invoice {invoice.InvoiceNumber}";
            }

            var partnerMovement = await dbContext.BusinessPartnerMovements
                .IgnoreQueryFilters()
                .Where(movement =>
                    movement.CompanyId == company.Id &&
                    movement.InvoiceId == invoice.Id)
                .OrderBy(movement => movement.IsDeleted)
                .ThenBy(movement => movement.Id)
                .FirstOrDefaultAsync(cancellationToken);
            var salesMovementType =
                InvoiceMovementRules.GetPartnerMovementType(
                    invoice.InvoiceType);
            var (salesDebit, salesCredit) =
                InvoiceMovementRules.GetPartnerAmounts(
                    invoice.InvoiceType,
                    invoice.Total);
            if (partnerMovement is null)
            {
                partnerMovement = new BusinessPartnerMovement
                {
                    CompanyId = company.Id,
                    InvoiceId = invoice.Id,
                    CreatedById = SeedActor,
                    CreatedByPc = createdByPc,
                    CreatedOn = createdOn
                };
                dbContext.BusinessPartnerMovements.Add(partnerMovement);
            }

            partnerMovement.IsDeleted = false;
            partnerMovement.DeletedById = null;
            partnerMovement.DeletedOn = null;
            partnerMovement.DeletedByPc = null;
            partnerMovement.BusinessPartnerId = invoice.BusinessPartnerId;
            partnerMovement.MovementType = salesMovementType;
            partnerMovement.MovementDate = invoice.InvoiceDate;
            partnerMovement.Currency = invoice.Currency;
            partnerMovement.Debit = salesDebit;
            partnerMovement.Credit = salesCredit;
            partnerMovement.Description =
                $"فاتورة {invoice.InvoiceNumber}";
            partnerMovement.ApplyExchangeRate(invoice.ExchangeRate);

            var driverTrip = await dbContext.DriverTrips
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    trip =>
                        trip.CompanyId == company.Id &&
                        trip.InvoiceId == invoice.Id,
                    cancellationToken);
            if (driverTrip is null)
            {
                dbContext.DriverTrips.Add(
                    new DriverTrip
                    {
                        CompanyId = company.Id,
                        DriverId = invoice.DriverId!.Value,
                        ActualDriverName = invoice.ActualDriverName,
                        InvoiceId = invoice.Id,
                        BusinessPartnerId = invoice.BusinessPartnerId,
                        InvoiceNumber = invoice.InvoiceNumber,
                        ExportInvoiceCode = invoice.ExportInvoiceCode,
                        TripDate = invoice.InvoiceDate,
                        CreatedById = SeedActor,
                        CreatedByPc = createdByPc,
                        CreatedOn = createdOn
                    });
            }
            else
            {
                driverTrip.IsDeleted = false;
                driverTrip.DeletedById = null;
                driverTrip.DeletedOn = null;
                driverTrip.DeletedByPc = null;
                driverTrip.DriverId = invoice.DriverId!.Value;
                driverTrip.ActualDriverName = invoice.ActualDriverName;
                driverTrip.BusinessPartnerId = invoice.BusinessPartnerId;
                driverTrip.InvoiceNumber = invoice.InvoiceNumber;
                driverTrip.ExportInvoiceCode = invoice.ExportInvoiceCode;
                driverTrip.TripDate = invoice.InvoiceDate;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedNonSalesInvoicesAsync(
        ApplicationDbContext dbContext,
        Company company,
        CancellationToken cancellationToken)
    {
        var seedDefinitions = BuildNonSalesInvoiceSeedDefinitions(
            company.Id);
        var seedExportCodes = seedDefinitions
            .Select(seed => seed.ExportCode)
            .ToArray();

        var store = await dbContext.Stores
            .Where(candidate =>
                candidate.CompanyId == company.Id &&
                candidate.Code == "MAIN" &&
                candidate.IsActive &&
                !candidate.IsContainerStore)
            .Select(candidate => new { candidate.Id })
            .FirstOrDefaultAsync(cancellationToken);
        var containerStore = await dbContext.Stores
            .Where(candidate =>
                candidate.CompanyId == company.Id &&
                candidate.Code == ContainerStore.Code &&
                candidate.IsActive &&
                candidate.IsContainerStore &&
                candidate.BusinessPartnerId.HasValue)
            .Select(candidate => new
            {
                candidate.Id,
                BusinessPartnerId = candidate.BusinessPartnerId!.Value
            })
            .FirstOrDefaultAsync(cancellationToken);
        var partner = containerStore is null
            ? null
            : await dbContext.BusinessPartners
                .Where(candidate =>
                    candidate.CompanyId == company.Id &&
                    candidate.Id == containerStore.BusinessPartnerId &&
                    candidate.IsActive)
                .Select(candidate => new
                {
                    candidate.Id,
                    candidate.Currency
                })
                .FirstOrDefaultAsync(cancellationToken);
        var drivers = await dbContext.Drivers
            .Where(driver =>
                driver.CompanyId == company.Id &&
                driver.IsActive)
            .OrderBy(driver => driver.Id)
            .Select(driver => new
            {
                driver.Id,
                driver.Name
            })
            .ToListAsync(cancellationToken);
        var containers = await dbContext.Containers
            .Where(container =>
                container.CompanyId == company.Id &&
                container.IsActive &&
                DefaultContainers
                    .Select(seedContainer => seedContainer.Code)
                    .Contains(container.Code))
            .OrderBy(container => container.Id)
            .Select(container => new { container.Id })
            .ToListAsync(cancellationToken);
        var items = await dbContext.Items
            .Where(candidate =>
                candidate.CompanyId == company.Id &&
                candidate.IsActive &&
                candidate.ItemUnit.IsActive)
            .OrderBy(candidate => candidate.Id)
            .Take(3)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.ItemUnitId
            })
            .ToListAsync(cancellationToken);
        var itemsCategoryId = await dbContext.ItemsCategories
            .Where(category =>
                category.CompanyId == company.Id &&
                category.Name == DefaultItemsCategoryNames[0] &&
                category.IsActive)
            .Select(category => (int?)category.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (partner is null ||
            store is null ||
            containerStore is null ||
            drivers.Count == 0 ||
            containers.Count == 0 ||
            items.Count == 0)
        {
            return;
        }

        var existingInvoices = await dbContext.Invoices
            .IgnoreQueryFilters()
            .Where(invoice =>
                invoice.CompanyId == company.Id &&
                seedExportCodes.Contains(invoice.ExportInvoiceCode!))
            .Include(invoice => invoice.Lines)
            .Include(invoice => invoice.ContainerLines)
            .OrderBy(invoice => invoice.IsDeleted)
            .ThenBy(invoice => invoice.Id)
            .ToListAsync(cancellationToken);
        var invoicesByExportCode = existingInvoices
            .GroupBy(invoice => invoice.ExportInvoiceCode!)
            .ToDictionary(group => group.Key, group => group.First());
        var createdByPc = Environment.MachineName;

        foreach (var seed in seedDefinitions.Where(seed =>
                     seed.InvoiceType == InvoiceType.Purchase))
        {
            await EnsureInvoiceAsync(seed, sourceLine: null);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var sourceExportCodes = seedDefinitions
            .Where(seed => seed.SourceExportCode is not null)
            .Select(seed => seed.SourceExportCode!)
            .Distinct()
            .ToArray();
        var sourceLines = await dbContext.InvoiceLines
            .IgnoreQueryFilters()
            .Include(line => line.Invoice)
            .Where(line =>
                line.CompanyId == company.Id &&
                !line.IsDeleted &&
                !line.Invoice.IsDeleted &&
                sourceExportCodes.Contains(
                    line.Invoice.ExportInvoiceCode!))
            .OrderBy(line => line.Id)
            .ToListAsync(cancellationToken);
        var sourceLinesByExportCode = sourceLines
            .GroupBy(line => line.Invoice.ExportInvoiceCode!)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (var seed in seedDefinitions.Where(seed =>
                     seed.InvoiceType is InvoiceType.SalesReturn or
                         InvoiceType.PurchaseReturn))
        {
            if (seed.SourceExportCode is null ||
                !sourceLinesByExportCode.TryGetValue(
                    seed.SourceExportCode,
                    out var sourceLine))
            {
                throw new InvalidOperationException(
                    $"Development invoice seed source {seed.SourceExportCode} was not found for company {company.Id}.");
            }

            await EnsureInvoiceAsync(seed, sourceLine);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await EnsureNonSalesInvoiceSideEffectsAsync(
            dbContext,
            company.Id,
            seedExportCodes,
            cancellationToken);

        async Task EnsureInvoiceAsync(
            SeedInvoiceDefinition seed,
            InvoiceLine? sourceLine)
        {
            var driver = drivers[seed.Sequence % drivers.Count];
            var container = containers[seed.Sequence % containers.Count];
            var seededItem = items[seed.Sequence % items.Count];
            if (sourceLine is not null &&
                (!sourceLine.ItemId.HasValue ||
                 !sourceLine.ItemUnitId.HasValue))
            {
                throw new InvalidOperationException(
                    $"Development invoice seed item source is invalid for {seed.ExportCode}.");
            }

            if (sourceLine is not null)
            {
                var expectedSourceType = seed.InvoiceType ==
                    InvoiceType.SalesReturn
                        ? InvoiceType.Sales
                        : InvoiceType.Purchase;
                if (sourceLine.Invoice.InvoiceType != expectedSourceType ||
                    sourceLine.Invoice.InvoiceDate > seed.InvoiceDate)
                {
                    throw new InvalidOperationException(
                        $"Development invoice seed source {seed.SourceExportCode} is invalid for {seed.ExportCode}.");
                }

                var alreadyReturnedQuantity = await dbContext.InvoiceLines
                    .AsNoTracking()
                    .Where(returnLine =>
                        returnLine.CompanyId == company.Id &&
                        !returnLine.IsDeleted &&
                        returnLine.SourceInvoiceLineId == sourceLine.Id &&
                        !returnLine.Invoice.IsDeleted &&
                        returnLine.Invoice.InvoiceType == seed.InvoiceType &&
                        returnLine.Invoice.ExportInvoiceCode !=
                            seed.ExportCode)
                    .SumAsync(
                        returnLine => (decimal?)returnLine.Quantity,
                        cancellationToken) ?? 0m;
                const decimal requestedReturnQuantity = 1m;
                if (alreadyReturnedQuantity + requestedReturnQuantity >
                    sourceLine.Quantity)
                {
                    throw new InvalidOperationException(
                        $"Development invoice seed return quantity exceeds source {seed.SourceExportCode} for company {company.Id}.");
                }
            }

            var itemId = sourceLine?.ItemId ?? seededItem.Id;
            var itemUnitId = sourceLine?.ItemUnitId ??
                seededItem.ItemUnitId;

            var invoicePartnerId = sourceLine?.Invoice.BusinessPartnerId ??
                partner.Id;
            var invoiceStoreId = sourceLine?.Invoice.StoreId ?? store.Id;
            var invoiceCurrency = sourceLine?.Invoice.Currency ??
                partner.Currency;
            var createdOn = seed.InvoiceDate.ToDateTime(
                new TimeOnly(12, 0),
                DateTimeKind.Utc);
            var invoiceIsNew = !invoicesByExportCode.TryGetValue(
                seed.ExportCode,
                out var invoice);
            if (invoiceIsNew)
            {
                invoice = new Invoice
                {
                    CompanyId = company.Id,
                    ExportInvoiceCode = seed.ExportCode,
                    CreatedById = SeedActor,
                    CreatedByPc = createdByPc,
                    CreatedOn = createdOn
                };
                dbContext.Invoices.Add(invoice);
                invoicesByExportCode.Add(seed.ExportCode, invoice);
            }

            invoice!.IsDeleted = false;
            invoice.DeletedById = null;
            invoice.DeletedOn = null;
            invoice.DeletedByPc = null;
            invoice.InvoiceNumber = seed.InvoiceNumber;
            invoice.ExportInvoiceCode = seed.ExportCode;
            invoice.InvoiceType = seed.InvoiceType;
            invoice.PaymentTerm = seed.PaymentTerm;
            invoice.InvoiceDate = seed.InvoiceDate;
            invoice.DueDate = seed.DueDate;
            invoice.BusinessPartnerId = invoicePartnerId;
            invoice.StoreId = invoiceStoreId;
            invoice.ContainerStoreId = containerStore.Id;
            invoice.ItemsCategoryId = itemsCategoryId;
            invoice.Currency = invoiceCurrency;
            invoice.DriverId = driver.Id;
            invoice.ActualDriverName = driver.Name;
            invoice.UsesExternalDriver = false;
            invoice.ExternalDriverName = null;
            invoice.Notes =
                $"Development seed {seed.InvoiceType} {seed.PaymentTerm} invoice";

            var line = invoice.Lines
                .OrderBy(candidate => candidate.IsDeleted)
                .ThenBy(candidate => candidate.Id)
                .FirstOrDefault();
            if (line is null)
            {
                line = new InvoiceLine
                {
                    CompanyId = company.Id,
                    CreatedById = SeedActor,
                    CreatedByPc = createdByPc,
                    CreatedOn = createdOn
                };
                invoice.Lines.Add(line);
            }

            line.IsDeleted = false;
            line.DeletedById = null;
            line.DeletedOn = null;
            line.DeletedByPc = null;
            line.ItemId = itemId;
            line.ItemUnitId = itemUnitId;
            line.SourceInvoiceLineId = sourceLine?.Id;
            line.Count = 1;
            line.Weight = seed.InvoiceType == InvoiceType.Purchase
                ? 5m
                : 1m;
            line.Price = sourceLine?.Price ??
                80m + (seed.Sequence % 5 * 10m);
            line.Notes = "Development seed invoice line";
            line.CalculateAmounts();

            var containerInbound = InvoiceMovementRules.IsInbound(
                seed.InvoiceType);
            var containerLine = invoice.ContainerLines
                .OrderBy(candidate => candidate.IsDeleted)
                .ThenBy(candidate => candidate.Id)
                .FirstOrDefault();
            if (containerLine is null)
            {
                containerLine = new InvoiceContainerLine
                {
                    CompanyId = company.Id,
                    CreatedById = SeedActor,
                    CreatedByPc = createdByPc,
                    CreatedOn = createdOn
                };
                invoice.ContainerLines.Add(containerLine);
            }

            containerLine.IsDeleted = false;
            containerLine.DeletedById = null;
            containerLine.DeletedOn = null;
            containerLine.DeletedByPc = null;
            containerLine.ContainerId = container.Id;
            containerLine.OutgoingUnits = containerInbound ? 0 : 1;
            containerLine.IncomingUnits = containerInbound ? 1 : 0;

            invoice.CalculateTotal();
            invoice.PaidAmount = seed.PaymentTerm == PaymentTerm.Cash
                ? invoice.Total
                : 0m;
            await ApplySeedInvoiceExchangeRateAsync(
                dbContext,
                company.Id,
                invoice,
                cancellationToken);
            invoice.Touch(createdOn);
        }
    }

    private static IReadOnlyList<SeedInvoiceDefinition>
        BuildNonSalesInvoiceSeedDefinitions(int companyId)
    {
        var definitions = new List<SeedInvoiceDefinition>();

        definitions.AddRange(Enumerable.Range(1, 9).Select(index =>
        {
            var invoiceDate = new DateOnly(2026, 7, 1)
                .AddDays(index - 1);
            var paymentTerm = index % 2 == 1
                ? PaymentTerm.Cash
                : PaymentTerm.Credit;
            return new SeedInvoiceDefinition(
                ExportCode: $"SEED-PURCHASE-{index:000}",
                InvoiceNumber: $"SEED-{companyId}-PUR-{index:000}",
                InvoiceType: InvoiceType.Purchase,
                PaymentTerm: paymentTerm,
                InvoiceDate: invoiceDate,
                DueDate: paymentTerm == PaymentTerm.Credit
                    ? invoiceDate.AddDays(30)
                    : null,
                Sequence: index - 1,
                SourceExportCode: null);
        }));

        var salesSourceCodes = new[]
        {
            "SEED-CASH",
            "SEED-CREDIT",
            "SEED-001",
            "SEED-002",
            "SEED-003",
            "SEED-004",
            "SEED-005",
            "SEED-006"
        };
        definitions.AddRange(Enumerable.Range(1, 8).Select(index =>
        {
            var invoiceDate = new DateOnly(2026, 10, 1)
                .AddDays(index - 1);
            var paymentTerm = index % 2 == 1
                ? PaymentTerm.Cash
                : PaymentTerm.Credit;
            return new SeedInvoiceDefinition(
                ExportCode: $"SEED-SALES-RETURN-{index:000}",
                InvoiceNumber: $"SEED-{companyId}-SRET-{index:000}",
                InvoiceType: InvoiceType.SalesReturn,
                PaymentTerm: paymentTerm,
                InvoiceDate: invoiceDate,
                DueDate: paymentTerm == PaymentTerm.Credit
                    ? invoiceDate.AddDays(30)
                    : null,
                Sequence: 9 + index - 1,
                SourceExportCode: salesSourceCodes[index - 1]);
        }));

        definitions.AddRange(Enumerable.Range(1, 8).Select(index =>
        {
            var invoiceDate = new DateOnly(2026, 7, 12)
                .AddDays(index - 1);
            var paymentTerm = index % 2 == 1
                ? PaymentTerm.Cash
                : PaymentTerm.Credit;
            return new SeedInvoiceDefinition(
                ExportCode: $"SEED-PURCHASE-RETURN-{index:000}",
                InvoiceNumber: $"SEED-{companyId}-PRET-{index:000}",
                InvoiceType: InvoiceType.PurchaseReturn,
                PaymentTerm: paymentTerm,
                InvoiceDate: invoiceDate,
                DueDate: paymentTerm == PaymentTerm.Credit
                    ? invoiceDate.AddDays(30)
                    : null,
                Sequence: 17 + index - 1,
                SourceExportCode: $"SEED-PURCHASE-{index:000}");
        }));

        var typeCounts = definitions
            .GroupBy(definition => definition.InvoiceType)
            .ToDictionary(group => group.Key, group => group.Count());
        if (definitions.Count != 25 ||
            typeCounts.GetValueOrDefault(InvoiceType.Purchase) != 9 ||
            typeCounts.GetValueOrDefault(InvoiceType.SalesReturn) != 8 ||
            typeCounts.GetValueOrDefault(InvoiceType.PurchaseReturn) != 8 ||
            typeCounts.ContainsKey(InvoiceType.Sales))
        {
            throw new InvalidOperationException(
                "Development non-sales invoice seed distribution must be 9 purchases, 8 sales returns, and 8 purchase returns.");
        }

        return definitions;
    }

    private static async Task ApplySeedInvoiceExchangeRateAsync(
        ApplicationDbContext dbContext,
        int companyId,
        Invoice invoice,
        CancellationToken cancellationToken)
    {
        if (invoice.Currency == CurrencyCode.EGP)
        {
            invoice.ApplyExchangeRate(null, 1m);
            return;
        }

        var invoiceRate = await dbContext.ExchangeRates
            .Where(candidate =>
                candidate.CompanyId == companyId &&
                candidate.Currency == invoice.Currency &&
                candidate.RateDate <= invoice.InvoiceDate)
            .OrderByDescending(candidate => candidate.RateDate)
            .ThenByDescending(candidate => candidate.Id)
            .FirstAsync(cancellationToken);
        invoice.ApplyExchangeRate(invoiceRate.Id, invoiceRate.Rate);
    }

    private static async Task EnsureNonSalesInvoiceSideEffectsAsync(
        ApplicationDbContext dbContext,
        int companyId,
        IReadOnlyCollection<string> seedExportCodes,
        CancellationToken cancellationToken)
    {
        var invoices = await dbContext.Invoices
            .IgnoreQueryFilters()
            .Include(invoice => invoice.Lines)
            .Include(invoice => invoice.ContainerLines)
            .Where(invoice =>
                invoice.CompanyId == companyId &&
                !invoice.IsDeleted &&
                seedExportCodes.Contains(invoice.ExportInvoiceCode!))
            .ToListAsync(cancellationToken);

        foreach (var invoice in invoices)
        {
            foreach (var containerLine in invoice.ContainerLines.Where(
                         line => !line.IsDeleted))
            {
                var containerMovement = await dbContext.ContainerMovements
                    .IgnoreQueryFilters()
                    .Where(movement =>
                        movement.CompanyId == companyId &&
                        movement.InvoiceId == invoice.Id &&
                        movement.ContainerId == containerLine.ContainerId)
                    .OrderBy(movement => movement.IsDeleted)
                    .ThenBy(movement => movement.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (containerMovement is null)
                {
                    containerMovement = new ContainerMovement
                    {
                        CompanyId = companyId,
                        InvoiceId = invoice.Id,
                        ContainerId = containerLine.ContainerId,
                        CreatedById = SeedActor,
                        CreatedByPc = Environment.MachineName,
                        CreatedOn = invoice.CreatedOn
                    };
                    dbContext.ContainerMovements.Add(containerMovement);
                }

                containerMovement.IsDeleted = false;
                containerMovement.DeletedById = null;
                containerMovement.DeletedOn = null;
                containerMovement.DeletedByPc = null;
                containerMovement.BusinessPartnerId =
                    invoice.BusinessPartnerId;
                containerMovement.ContainerStoreId =
                    invoice.ContainerStoreId!.Value;
                containerMovement.InvoiceNumber = invoice.InvoiceNumber;
                containerMovement.MovementDate = invoice.InvoiceDate;
                containerMovement.OutgoingUnits =
                    containerLine.OutgoingUnits;
                containerMovement.IncomingUnits =
                    containerLine.IncomingUnits;
                containerMovement.Description =
                    $"Invoice {invoice.InvoiceNumber}";
            }

            var movementType = InvoiceMovementRules.GetItemMovementType(
                invoice.InvoiceType);
            var inbound = InvoiceMovementRules.IsInbound(
                invoice.InvoiceType);
            foreach (var line in invoice.Lines.Where(line =>
                         !line.IsDeleted && line.ItemId.HasValue))
            {
                if (line.ItemId is not int lineItemId)
                {
                    continue;
                }

                var itemMovement = await dbContext.ItemMovements
                    .IgnoreQueryFilters()
                    .Where(movement =>
                        movement.CompanyId == companyId &&
                        movement.ReferenceId == invoice.Id &&
                        movement.ItemId == lineItemId)
                    .OrderBy(movement => movement.IsDeleted)
                    .ThenBy(movement => movement.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (itemMovement is null)
                {
                    itemMovement = new ItemMovement
                    {
                        CompanyId = companyId,
                        ReferenceId = invoice.Id,
                        ItemId = lineItemId,
                        CreatedById = SeedActor,
                        CreatedByPc = Environment.MachineName,
                        CreatedOn = invoice.CreatedOn
                    };
                    dbContext.ItemMovements.Add(itemMovement);
                }

                itemMovement.IsDeleted = false;
                itemMovement.DeletedById = null;
                itemMovement.DeletedOn = null;
                itemMovement.DeletedByPc = null;
                itemMovement.StoreId = invoice.StoreId;
                itemMovement.ItemUnitId = line.ItemUnitId;
                itemMovement.MovementType = movementType;
                itemMovement.ReferenceNumber = invoice.InvoiceNumber;
                itemMovement.MovementDate = invoice.InvoiceDate;
                itemMovement.QuantityIn = inbound ? line.Quantity : 0m;
                itemMovement.QuantityOut = inbound ? 0m : line.Quantity;
                itemMovement.Description =
                    $"Invoice {invoice.InvoiceNumber}";
            }

            var partnerMovement = await dbContext.BusinessPartnerMovements
                .IgnoreQueryFilters()
                .Where(movement =>
                    movement.CompanyId == companyId &&
                    movement.InvoiceId == invoice.Id)
                .OrderBy(movement => movement.IsDeleted)
                .ThenBy(movement => movement.Id)
                .FirstOrDefaultAsync(cancellationToken);
            var partnerMovementType =
                InvoiceMovementRules.GetPartnerMovementType(
                    invoice.InvoiceType);
            var (debit, credit) = InvoiceMovementRules.GetPartnerAmounts(
                invoice.InvoiceType,
                invoice.Total);
            if (partnerMovement is null)
            {
                partnerMovement = new BusinessPartnerMovement
                {
                    CompanyId = companyId,
                    InvoiceId = invoice.Id,
                    CreatedById = SeedActor,
                    CreatedByPc = Environment.MachineName,
                    CreatedOn = invoice.CreatedOn
                };
                dbContext.BusinessPartnerMovements.Add(partnerMovement);
            }

            partnerMovement.IsDeleted = false;
            partnerMovement.DeletedById = null;
            partnerMovement.DeletedOn = null;
            partnerMovement.DeletedByPc = null;
            partnerMovement.BusinessPartnerId = invoice.BusinessPartnerId;
            partnerMovement.MovementType = partnerMovementType;
            partnerMovement.MovementDate = invoice.InvoiceDate;
            partnerMovement.Currency = invoice.Currency;
            partnerMovement.Debit = debit;
            partnerMovement.Credit = credit;
            partnerMovement.Description =
                $"فاتورة {invoice.InvoiceNumber}";
            partnerMovement.ApplyExchangeRate(invoice.ExchangeRate);

            var driverTrip = await dbContext.DriverTrips
                .IgnoreQueryFilters()
                .Where(trip =>
                    trip.CompanyId == companyId &&
                    trip.InvoiceId == invoice.Id)
                .OrderBy(trip => trip.IsDeleted)
                .ThenBy(trip => trip.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (driverTrip is null)
            {
                driverTrip = new DriverTrip
                {
                    CompanyId = companyId,
                    InvoiceId = invoice.Id,
                    CreatedById = SeedActor,
                    CreatedByPc = Environment.MachineName,
                    CreatedOn = invoice.CreatedOn
                };
                dbContext.DriverTrips.Add(driverTrip);
            }

            driverTrip.IsDeleted = false;
            driverTrip.DeletedById = null;
            driverTrip.DeletedOn = null;
            driverTrip.DeletedByPc = null;
            driverTrip.DriverId = invoice.DriverId!.Value;
            driverTrip.ActualDriverName = invoice.ActualDriverName;
            driverTrip.BusinessPartnerId = invoice.BusinessPartnerId;
            driverTrip.InvoiceNumber = invoice.InvoiceNumber;
            driverTrip.ExportInvoiceCode = invoice.ExportInvoiceCode;
            driverTrip.TripDate = invoice.InvoiceDate;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedCashManagementAsync(
        ApplicationDbContext dbContext,
        Company company,
        CancellationToken cancellationToken)
    {
        var cashbox = await dbContext.Cashboxes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                entity =>
                    entity.CompanyId == company.Id &&
                    entity.Code == "CASH-MAIN",
                cancellationToken);
        if (cashbox is null)
        {
            cashbox = new Cashbox
            {
                CompanyId = company.Id,
                Code = "CASH-MAIN",
                Name = "Main Cashbox",
                Currency = CurrencyCode.EGP,
                OpeningBalance = 100_000m,
                IsActive = true,
                Notes = "Development seed cashbox"
            };
            cashbox.ApplyOpeningExchangeRate(
                new DateOnly(2026, 1, 1),
                null,
                1m);
            dbContext.Cashboxes.Add(cashbox);
        }
        else if (cashbox.OpeningBalance != 0m &&
                 cashbox.BaseOpeningBalance == 0m)
        {
            cashbox.ApplyOpeningExchangeRate(
                cashbox.OpeningBalanceDate == default
                    ? new DateOnly(2026, 1, 1)
                    : cashbox.OpeningBalanceDate,
                null,
                1m);
        }

        var movementTypeSeeds = new[]
        {
            new
            {
                Name = "Customer Collection",
                Direction = CashDirection.Receipt,
                Classification = CashMovementClassification.PartnerSettlement,
                PartnerEffect = PartnerAccountEffect.Credit,
                DefaultInvoiceType = (InvoiceType?)InvoiceType.Sales
            },
            new
            {
                Name = "Supplier Refund",
                Direction = CashDirection.Receipt,
                Classification = CashMovementClassification.PartnerSettlement,
                PartnerEffect = PartnerAccountEffect.Credit,
                DefaultInvoiceType = (InvoiceType?)InvoiceType.PurchaseReturn
            },
            new
            {
                Name = "Other Receipt",
                Direction = CashDirection.Receipt,
                Classification = CashMovementClassification.Other,
                PartnerEffect = PartnerAccountEffect.None,
                DefaultInvoiceType = (InvoiceType?)null
            },
            new
            {
                Name = "Other Revenue",
                Direction = CashDirection.Receipt,
                Classification = CashMovementClassification.Revenue,
                PartnerEffect = PartnerAccountEffect.None,
                DefaultInvoiceType = (InvoiceType?)null
            },
            new
            {
                Name = "Supplier Payment",
                Direction = CashDirection.Payment,
                Classification = CashMovementClassification.PartnerSettlement,
                PartnerEffect = PartnerAccountEffect.Debit,
                DefaultInvoiceType = (InvoiceType?)InvoiceType.Purchase
            },
            new
            {
                Name = "Customer Refund",
                Direction = CashDirection.Payment,
                Classification = CashMovementClassification.PartnerSettlement,
                PartnerEffect = PartnerAccountEffect.Debit,
                DefaultInvoiceType = (InvoiceType?)InvoiceType.SalesReturn
            },
            new
            {
                Name = "Driver Advance",
                Direction = CashDirection.Payment,
                Classification = CashMovementClassification.Other,
                PartnerEffect = PartnerAccountEffect.None,
                DefaultInvoiceType = (InvoiceType?)null
            },
            new
            {
                Name = "Other Payment",
                Direction = CashDirection.Payment,
                Classification = CashMovementClassification.Other,
                PartnerEffect = PartnerAccountEffect.None,
                DefaultInvoiceType = (InvoiceType?)null
            },
            new
            {
                Name = "Administrative Expense",
                Direction = CashDirection.Payment,
                Classification = CashMovementClassification.Expense,
                PartnerEffect = PartnerAccountEffect.None,
                DefaultInvoiceType = (InvoiceType?)null
            }
        };

        var existingMovementTypes = await dbContext.CashMovementTypes
            .IgnoreQueryFilters()
            .Where(entity => entity.CompanyId == company.Id)
            .ToListAsync(cancellationToken);

        foreach (var seed in movementTypeSeeds)
        {
            var existingMovementType = existingMovementTypes
                .FirstOrDefault(entity =>
                    entity.Direction == seed.Direction &&
                    entity.Name == seed.Name);
            if (existingMovementType is not null)
            {
                existingMovementType.IsDeleted = false;
                existingMovementType.DeletedById = null;
                existingMovementType.DeletedOn = null;
                existingMovementType.DeletedByPc = null;
                existingMovementType.IsActive = true;
                existingMovementType.Classification = seed.Classification;
                existingMovementType.PartnerEffect = seed.PartnerEffect;
                if (seed.DefaultInvoiceType is InvoiceType invoiceType &&
                    !existingMovementTypes.Any(entity =>
                        !entity.IsDeleted &&
                        IsDefaultForInvoiceType(entity, invoiceType)))
                {
                    SetDefaultForInvoiceType(
                        existingMovementType,
                        invoiceType);
                }

                continue;
            }

            var movementType = new CashMovementType
            {
                CompanyId = company.Id,
                Name = seed.Name,
                Direction = seed.Direction,
                Classification = seed.Classification,
                PartnerEffect = seed.PartnerEffect,
                IsActive = true,
                Notes = "Development seed movement type"
            };

            if (seed.DefaultInvoiceType is InvoiceType defaultInvoiceType &&
                !existingMovementTypes.Any(entity =>
                    !entity.IsDeleted &&
                    IsDefaultForInvoiceType(entity, defaultInvoiceType)))
            {
                SetDefaultForInvoiceType(
                    movementType,
                    defaultInvoiceType);
            }

            dbContext.CashMovementTypes.Add(movementType);
            existingMovementTypes.Add(movementType);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var customerCollectionType = existingMovementTypes.Single(entity =>
            entity.Direction == CashDirection.Receipt &&
            entity.Name == "Customer Collection");
        var supplierPaymentType = existingMovementTypes.Single(entity =>
            entity.Direction == CashDirection.Payment &&
            entity.Name == "Supplier Payment");
        var customerRefundType = existingMovementTypes.Single(entity =>
            entity.Direction == CashDirection.Payment &&
            entity.Name == "Customer Refund");
        var supplierRefundType = existingMovementTypes.Single(entity =>
            entity.Direction == CashDirection.Receipt &&
            entity.Name == "Supplier Refund");
        var invoiceMovementTypes = new Dictionary<
            InvoiceType,
            CashMovementType>
        {
            [InvoiceType.Sales] = customerCollectionType,
            [InvoiceType.Purchase] = supplierPaymentType,
            [InvoiceType.SalesReturn] = customerRefundType,
            [InvoiceType.PurchaseReturn] = supplierRefundType
        };
        await SeedCashInvoicePaymentsAsync(
            dbContext,
            company,
            cashbox,
            invoiceMovementTypes,
            cancellationToken);

        var revenueType = existingMovementTypes.Single(entity =>
            entity.Direction == CashDirection.Receipt &&
            entity.Name == "Other Revenue");
        var expenseType = existingMovementTypes.Single(entity =>
            entity.Direction == CashDirection.Payment &&
            entity.Name == "Administrative Expense");
        await SeedStandaloneCashVoucherAsync(
            dbContext,
            company,
            cashbox,
            revenueType,
            voucherNumber: $"SEED-REVENUE-{company.Id}",
            voucherDate: new DateOnly(2026, 7, 27),
            amount: 1_250m,
            description: "Development seed miscellaneous revenue",
            cancellationToken);
        await SeedStandaloneCashVoucherAsync(
            dbContext,
            company,
            cashbox,
            expenseType,
            voucherNumber: $"SEED-EXPENSE-{company.Id}",
            voucherDate: new DateOnly(2026, 7, 28),
            amount: 750m,
            description: "Development seed administrative expense",
            cancellationToken);

        static bool IsDefaultForInvoiceType(
            CashMovementType movementType,
            InvoiceType invoiceType) =>
            invoiceType switch
            {
                InvoiceType.Sales => movementType.IsDefaultForSales,
                InvoiceType.Purchase => movementType.IsDefaultForPurchase,
                InvoiceType.SalesReturn =>
                    movementType.IsDefaultForSalesReturn,
                InvoiceType.PurchaseReturn =>
                    movementType.IsDefaultForPurchaseReturn,
                _ => false
            };

        static void SetDefaultForInvoiceType(
            CashMovementType movementType,
            InvoiceType invoiceType)
        {
            switch (invoiceType)
            {
                case InvoiceType.Sales:
                    movementType.IsDefaultForSales = true;
                    break;
                case InvoiceType.Purchase:
                    movementType.IsDefaultForPurchase = true;
                    break;
                case InvoiceType.SalesReturn:
                    movementType.IsDefaultForSalesReturn = true;
                    break;
                case InvoiceType.PurchaseReturn:
                    movementType.IsDefaultForPurchaseReturn = true;
                    break;
            }
        }

        const string voucherNumberPrefix = "SEED-CASH-RECEIPT";
        var voucherNumber = $"{voucherNumberPrefix}-{company.Id}";
        var voucher = await dbContext.CashVouchers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                entity =>
                    entity.CompanyId == company.Id &&
                    entity.VoucherNumber == voucherNumber,
                cancellationToken);
        var partner = await dbContext.BusinessPartners
            .Where(entity =>
                entity.CompanyId == company.Id &&
                entity.IsActive &&
                entity.Currency == cashbox.Currency)
            .OrderBy(entity => entity.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var collectionType = customerCollectionType;

        if (voucher is null && partner is not null)
        {
            voucher = new CashVoucher
            {
                CompanyId = company.Id,
                VoucherNumber = voucherNumber,
                VoucherDate = new DateOnly(2026, 7, 26),
                Direction = CashDirection.Receipt,
                CashboxId = cashbox.Id,
                CashMovementTypeId = collectionType.Id,
                PartyType = CashPartyType.Partner,
                BusinessPartnerId = partner.Id,
                Amount = 1_000m,
                Currency = cashbox.Currency,
                IsPosted = true,
                ReferenceNumber = $"SEED-REF-{company.Id}",
                Description = "تحصيل تجريبي من العميل",
                Notes = "سند نقدية تجريبي"
            };
            voucher.Touch(DateTime.UtcNow);
            voucher.ApplyExchangeRate(null, 1m);
            dbContext.CashVouchers.Add(voucher);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (voucher is not null)
        {
            voucher.IsPosted = true;
            if (voucher.Amount != 0m && voucher.BaseAmount == 0m)
            {
                voucher.ApplyExchangeRate(null, 1m);
            }

            if (voucher.Description == "Seed customer collection")
            {
                voucher.Description = "تحصيل تجريبي من العميل";
            }

            if (voucher.Notes == "Development seed voucher")
            {
                voucher.Notes = "سند نقدية تجريبي";
            }
        }

        if (voucher is null ||
            voucher.IsDeleted ||
            voucher.BusinessPartnerId is null)
        {
            return;
        }

        var existingMovement = await dbContext.BusinessPartnerMovements
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                movement =>
                    movement.CompanyId == company.Id &&
                    movement.CashVoucherId == voucher.Id &&
                    !movement.IsDeleted,
                cancellationToken);
        if (existingMovement is not null)
        {
            if ((existingMovement.Debit != 0m ||
                 existingMovement.Credit != 0m) &&
                existingMovement.BaseDebit == 0m &&
                existingMovement.BaseCredit == 0m)
            {
                existingMovement.ApplyExchangeRate(
                    voucher.ExchangeRate);
            }

            if (existingMovement.Description == "Seed customer collection")
            {
                existingMovement.Description =
                    "تحصيل تجريبي من العميل";
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var partnerMovement = new BusinessPartnerMovement
        {
            CompanyId = company.Id,
            BusinessPartnerId = voucher.BusinessPartnerId.Value,
            CashVoucherId = voucher.Id,
            MovementType = BusinessPartnerMovementType.CashReceipt,
            MovementDate = voucher.VoucherDate,
            Currency = voucher.Currency,
            Debit = 0m,
            Credit = voucher.Amount,
            Description = voucher.Description
        };
        partnerMovement.ApplyExchangeRate(voucher.ExchangeRate);
        dbContext.BusinessPartnerMovements.Add(partnerMovement);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task ApplyPartnerOpeningBalanceExchangeRateAsync(
        ApplicationDbContext dbContext,
        int companyId,
        PartnerOpeningBalance openingBalance,
        CancellationToken cancellationToken)
    {
        if (openingBalance.Currency == CurrencyCode.EGP)
        {
            openingBalance.ApplyExchangeRate(null, 1m);
            return;
        }

        var rate = await dbContext.ExchangeRates
            .Where(candidate =>
                candidate.CompanyId == companyId &&
                candidate.Currency == openingBalance.Currency &&
                candidate.RateDate <= openingBalance.DocumentDate)
            .OrderByDescending(candidate => candidate.RateDate)
            .ThenByDescending(candidate => candidate.Id)
            .FirstAsync(cancellationToken);
        openingBalance.ApplyExchangeRate(rate.Id, rate.Rate);
    }

    private static async Task SeedCashInvoicePaymentsAsync(
        ApplicationDbContext dbContext,
        Company company,
        Cashbox cashbox,
        IReadOnlyDictionary<InvoiceType, CashMovementType> movementTypes,
        CancellationToken cancellationToken)
    {
        var cashInvoices = await dbContext.Invoices
            .IgnoreQueryFilters()
            .Where(invoice =>
                invoice.CompanyId == company.Id &&
                !invoice.IsDeleted &&
                invoice.PaymentTerm == PaymentTerm.Cash &&
                invoice.PaidAmount > 0m &&
                invoice.ExportInvoiceCode != null &&
                invoice.ExportInvoiceCode.StartsWith("SEED-"))
            .ToListAsync(cancellationToken);
        var createdOn = DateTime.UtcNow;

        foreach (var invoice in cashInvoices)
        {
            if (!movementTypes.TryGetValue(
                    invoice.InvoiceType,
                    out var movementType))
            {
                continue;
            }

            var direction = InvoiceMovementRules.GetPaymentDirection(
                invoice.InvoiceType);
            var cashboxExchangeRateId = (int?)null;
            var cashboxExchangeRate = 1m;
            if (cashbox.Currency != CurrencyCode.EGP)
            {
                var rate = await dbContext.ExchangeRates
                    .Where(candidate =>
                        candidate.CompanyId == company.Id &&
                        candidate.Currency == cashbox.Currency &&
                        candidate.RateDate <= invoice.InvoiceDate)
                    .OrderByDescending(candidate => candidate.RateDate)
                    .ThenByDescending(candidate => candidate.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (rate is null)
                {
                    continue;
                }

                cashboxExchangeRateId = rate.Id;
                cashboxExchangeRate = rate.Rate;
            }

            var cashboxAmount = ExchangeRateRules.ConvertFromBase(
                invoice.BasePaidAmountAtInvoiceRate,
                cashboxExchangeRate);
            if (cashboxAmount <= 0m)
            {
                continue;
            }

            var voucherNumber =
                $"SEED-INVOICE-CASH-{company.Id}-{invoice.Id}";
            var voucher = await dbContext.CashVouchers
                .IgnoreQueryFilters()
                .Where(candidate =>
                    candidate.CompanyId == company.Id &&
                    candidate.InvoiceId == invoice.Id)
                .OrderBy(candidate => candidate.IsDeleted)
                .ThenBy(candidate => candidate.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (voucher is not null &&
                voucher.VoucherNumber != voucherNumber)
            {
                continue;
            }

            if (voucher is null)
            {
                voucher = new CashVoucher
                {
                    CompanyId = company.Id,
                    InvoiceId = invoice.Id,
                    VoucherNumber = voucherNumber,
                    CreatedById = SeedActor,
                    CreatedByPc = Environment.MachineName,
                    CreatedOn = createdOn
                };
                dbContext.CashVouchers.Add(voucher);
            }

            voucher.IsDeleted = false;
            voucher.DeletedById = null;
            voucher.DeletedOn = null;
            voucher.DeletedByPc = null;
            voucher.InvoiceId = invoice.Id;
            voucher.VoucherDate = invoice.InvoiceDate;
            voucher.Direction = direction;
            voucher.CashboxId = cashbox.Id;
            voucher.CashMovementTypeId = movementType.Id;
            voucher.PartyType = CashPartyType.Partner;
            voucher.BusinessPartnerId = invoice.BusinessPartnerId;
            voucher.EmployeeId = null;
            voucher.DriverId = null;
            voucher.DriverTripId = null;
            voucher.ExternalPartyName = null;
            voucher.CashboxTransferId = null;
            voucher.Amount = cashboxAmount;
            voucher.Currency = cashbox.Currency;
            voucher.IsPosted = true;
            voucher.ReferenceNumber = invoice.InvoiceNumber;
            voucher.Description = $"دفعة الفاتورة {invoice.InvoiceNumber}";
            voucher.Notes = direction == CashDirection.Receipt
                ? "سند قبض فاتورة تجريبي"
                : "سند صرف فاتورة تجريبي";
            voucher.ApplyExchangeRate(
                cashboxExchangeRateId,
                cashboxExchangeRate);

            if (voucher.Id == 0)
            {
                voucher.Touch(createdOn);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var invoice in cashInvoices)
        {
            var direction = InvoiceMovementRules.GetPaymentDirection(
                invoice.InvoiceType);
            var voucherNumber =
                $"SEED-INVOICE-CASH-{company.Id}-{invoice.Id}";
            var voucher = await dbContext.CashVouchers
                .FirstOrDefaultAsync(
                    candidate =>
                        candidate.CompanyId == company.Id &&
                        candidate.VoucherNumber == voucherNumber,
                    cancellationToken);
            if (voucher is null)
            {
                continue;
            }

            var invoicePayment = await dbContext.InvoicePayments
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    payment =>
                        payment.CompanyId == company.Id &&
                        payment.CashVoucherId == voucher.Id,
                    cancellationToken);
            if (invoicePayment is null)
            {
                invoicePayment = new InvoicePayment
                {
                    CompanyId = company.Id,
                    InvoiceId = invoice.Id,
                    CashVoucherId = voucher.Id,
                    CreatedById = SeedActor,
                    CreatedByPc = Environment.MachineName,
                    CreatedOn = createdOn
                };
                dbContext.InvoicePayments.Add(invoicePayment);
            }
            else
            {
                invoicePayment.IsDeleted = false;
                invoicePayment.DeletedById = null;
                invoicePayment.DeletedOn = null;
                invoicePayment.DeletedByPc = null;
                invoicePayment.InvoiceId = invoice.Id;
            }

            var cashboxExchangeRate = voucher.ExchangeRate;
            invoicePayment.Apply(
                invoice.Currency,
                invoice.PaidAmount,
                voucher.Currency,
                voucher.Amount,
                invoice.ExchangeRate,
                cashboxExchangeRate);

            var partnerMovement = await dbContext.BusinessPartnerMovements
                .IgnoreQueryFilters()
                .Where(movement =>
                    movement.CompanyId == company.Id &&
                    movement.CashVoucherId == voucher.Id)
                .OrderBy(movement => movement.IsDeleted)
                .ThenBy(movement => movement.Id)
                .FirstOrDefaultAsync(cancellationToken);
            var debit = direction == CashDirection.Payment
                ? invoice.PaidAmount
                : 0m;
            var credit = direction == CashDirection.Receipt
                ? invoice.PaidAmount
                : 0m;
            if (partnerMovement is null)
            {
                partnerMovement = new BusinessPartnerMovement
                {
                    CompanyId = company.Id,
                    BusinessPartnerId = invoice.BusinessPartnerId,
                    CashVoucherId = voucher.Id,
                    MovementType = direction == CashDirection.Receipt
                        ? BusinessPartnerMovementType.CashReceipt
                        : BusinessPartnerMovementType.CashPayment,
                    MovementDate = invoice.InvoiceDate,
                    Currency = invoice.Currency,
                    Debit = debit,
                    Credit = credit,
                    Description = voucher.Description,
                    CreatedById = SeedActor,
                    CreatedByPc = Environment.MachineName,
                    CreatedOn = createdOn
                };
                dbContext.BusinessPartnerMovements.Add(partnerMovement);
            }
            else
            {
                partnerMovement.IsDeleted = false;
                partnerMovement.DeletedById = null;
                partnerMovement.DeletedOn = null;
                partnerMovement.DeletedByPc = null;
                partnerMovement.BusinessPartnerId = invoice.BusinessPartnerId;
                partnerMovement.InvoiceId = null;
                partnerMovement.MovementType =
                    direction == CashDirection.Receipt
                        ? BusinessPartnerMovementType.CashReceipt
                        : BusinessPartnerMovementType.CashPayment;
                partnerMovement.MovementDate = invoice.InvoiceDate;
                partnerMovement.Currency = invoice.Currency;
                partnerMovement.Debit = debit;
                partnerMovement.Credit = credit;
                partnerMovement.Description = voucher.Description;
            }

            partnerMovement.ApplyExchangeRate(invoice.ExchangeRate);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedStandaloneCashVoucherAsync(
        ApplicationDbContext dbContext,
        Company company,
        Cashbox cashbox,
        CashMovementType movementType,
        string voucherNumber,
        DateOnly voucherDate,
        decimal amount,
        string description,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.CashVouchers
            .IgnoreQueryFilters()
            .AnyAsync(
                voucher =>
                    voucher.CompanyId == company.Id &&
                    voucher.VoucherNumber == voucherNumber &&
                    !voucher.IsDeleted,
                cancellationToken);
        if (exists)
        {
            return;
        }

        var createdOn = voucherDate.ToDateTime(
            new TimeOnly(12, 0),
            DateTimeKind.Utc);
        var voucher = new CashVoucher
        {
            CompanyId = company.Id,
            VoucherNumber = voucherNumber,
            VoucherDate = voucherDate,
            Direction = movementType.Direction,
            CashboxId = cashbox.Id,
            CashMovementTypeId = movementType.Id,
            PartyType = CashPartyType.None,
            Amount = amount,
            Currency = cashbox.Currency,
            IsPosted = true,
            ReferenceNumber = voucherNumber,
            Description = description,
            Notes = "Development seed voucher",
            CreatedById = SeedActor,
            CreatedByPc = Environment.MachineName,
            CreatedOn = createdOn
        };
        voucher.Touch(createdOn);
        voucher.ApplyExchangeRate(null, 1m);
        dbContext.CashVouchers.Add(voucher);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void ApplyStockOpeningSeedAmounts(
        StockOpeningBalanceLine line,
        int index)
    {
        var amount = StockOpeningLineAmounts[index];
        line.Count = amount.Count;
        line.Weight = amount.Weight;
        line.Price = amount.Price;
        line.CalculateAmounts();
    }

    private static async Task<IReadOnlyList<Company>> SeedAdditionalCompaniesAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var companies = new List<Company>(AdditionalSeedCompanies.Length);

        foreach (var seedCompany in AdditionalSeedCompanies)
        {
            var company = await dbContext.Companies.FirstOrDefaultAsync(
                entity =>
                    entity.CommercialRegister == seedCompany.CommercialRegister ||
                    entity.TaxNumber == seedCompany.TaxNumber,
                cancellationToken);

            if (company is null)
            {
                company = new Company
                {
                    Name = seedCompany.Name,
                    Address = seedCompany.Address,
                    CommercialRegister = seedCompany.CommercialRegister,
                    TaxNumber = seedCompany.TaxNumber,
                    ManagerName = seedCompany.ManagerName,
                    CreatedById = SeedActor,
                    CreatedByPc = Environment.MachineName,
                    CreatedOn = seedCompany.CreatedOn
                };

                dbContext.Companies.Add(company);
            }

            companies.Add(company);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return companies;
    }

    private static async Task<Company> SeedCompaniesAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        const string commercialRegister = "54321";
        const string taxNumber = "456789123";

        var existingCompany = await dbContext.Companies
            .FirstOrDefaultAsync(
                company =>
                    company.CommercialRegister == commercialRegister ||
                    company.TaxNumber == taxNumber,
                cancellationToken);

        if (existingCompany is not null)
        {
            return existingCompany;
        }

        var company = new Company
        {
            Name = "مجموعة السلام القابضة",
            Address = "شارع النصر، مدينة نصر، القاهرة",
            CommercialRegister = commercialRegister,
            TaxNumber = taxNumber,
            ManagerName = "خالد السلام",
            CreatedById = SeedActor,
            CreatedByPc = Environment.MachineName,
            CreatedOn = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        dbContext.Companies.Add(company);
        await dbContext.SaveChangesAsync(cancellationToken);
        return company;
    }

    private static async Task SeedCompanySettingsAsync(
        ApplicationDbContext dbContext,
        IReadOnlyCollection<Company> companies,
        CancellationToken cancellationToken)
    {
        var companyIds = companies
            .Select(company => company.Id)
            .Distinct()
            .ToArray();
        var existingCompanyIds = await dbContext.CompanySettings
            .IgnoreQueryFilters()
            .Where(settings => companyIds.Contains(settings.CompanyId))
            .Select(settings => settings.CompanyId)
            .ToListAsync(cancellationToken);

        foreach (var companyId in companyIds.Except(existingCompanyIds))
        {
            dbContext.CompanySettings.Add(new CompanySettings
            {
                CompanyId = companyId,
                BaseCurrency = CurrencyCode.EGP,
                StockBalanceCheckMode = StockBalanceCheckMode.None
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedExchangeRatesAsync(
        ApplicationDbContext dbContext,
        IReadOnlyCollection<Company> companies,
        CancellationToken cancellationToken)
    {
        var seeds = new (CurrencyCode Currency, decimal Rate)[]
        {
            (CurrencyCode.USD, 50m),
            (CurrencyCode.EUR, 55m),
            (CurrencyCode.GBP, 64m),
            (CurrencyCode.SAR, 13.333333333333m),
            (CurrencyCode.AED, 13.617m),
            (CurrencyCode.KWD, 162m)
        };
        var rateDate = new DateOnly(2026, 1, 1);

        foreach (var company in companies)
        {
            foreach (var seed in seeds)
            {
                var exists = await dbContext.ExchangeRates
                    .IgnoreQueryFilters()
                    .AnyAsync(
                        rate =>
                            rate.CompanyId == company.Id &&
                            rate.Currency == seed.Currency &&
                            rate.RateDate == rateDate,
                        cancellationToken);
                if (exists)
                {
                    continue;
                }

                var rate = new ExchangeRate
                {
                    CompanyId = company.Id,
                    Currency = seed.Currency,
                    RateDate = rateDate,
                    Rate = seed.Rate,
                    Source = ExchangeRateSource.Manual,
                    Notes = "Development seed exchange rate"
                };
                rate.Touch(DateTime.UtcNow);
                dbContext.ExchangeRates.Add(rate);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void EnsureSucceeded(
        IdentityResult result,
        string operation)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = string.Join(
            "; ",
            result.Errors.Select(error => $"{error.Code}: {error.Description}"));

        throw new InvalidOperationException(
            $"Identity seed failed while {operation}: {errors}");
    }
    private static async Task SeedEmployeesAsync(
        ApplicationDbContext dbContext,
        Company company,
        CancellationToken cancellationToken)
    {
        var seedEmails = DefaultEmployees
            .Select(employee => GetSeedEmployeeEmail(employee.Key, company.Id))
            .ToArray();
        var existingEmployeeEmails = (await dbContext.Employees
            .IgnoreQueryFilters()
            .Where(employee =>
                employee.CompanyId == company.Id &&
                employee.Email != null &&
                seedEmails.Contains(employee.Email))
            .Select(employee => employee.Email!)
            .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var createdOn = new DateTime(
            2026,
            7,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc);
        var createdByPc = Environment.MachineName;

        foreach (var seed in DefaultEmployees)
        {
            var email = GetSeedEmployeeEmail(seed.Key, company.Id);
            if (existingEmployeeEmails.Contains(email))
            {
                continue;
            }

            dbContext.Employees.Add(new Employee
            {
                CompanyId = company.Id,
                Name = seed.Name,
                JobTitle = seed.JobTitle,
                PhoneNumber = $"010{company.Id:D3}{seed.Key.PadLeft(5, '0')}",
                Email = email,
                Address = "Development seed employee address",
                Type = seed.Type,
                DailySalary = seed.Type == EmployeeType.Daily
                    ? seed.Salary
                    : null,
                MonthlySalary = seed.Type == EmployeeType.Monthly
                    ? seed.Salary
                    : null,
                RequiredWorkingDaysPerMonth = seed.Type == EmployeeType.Monthly
                    ? 26
                    : null,
                IsActive = true,
                CreatedById = SeedActor,
                CreatedByPc = createdByPc,
                CreatedOn = createdOn
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedAttendanceAsync(
        ApplicationDbContext dbContext,
        Company company,
        CancellationToken cancellationToken)
    {
        var seedEmails = DefaultEmployees
            .Select(employee => GetSeedEmployeeEmail(employee.Key, company.Id))
            .ToArray();
        var employees = await dbContext.Employees
            .Where(employee =>
                employee.CompanyId == company.Id &&
                employee.IsActive &&
                employee.Email != null &&
                seedEmails.Contains(employee.Email))
            .OrderBy(employee => employee.Id)
            .Select(employee => employee.Id)
            .ToListAsync(cancellationToken);

        if (employees.Count == 0)
        {
            return;
        }

        var targetDate = new DateOnly(2026, 7, 25);
        var existingEmployeeIds = (await dbContext.EmployeeAttendances
            .IgnoreQueryFilters()
            .Where(attendance =>
                attendance.CompanyId == company.Id &&
                attendance.WorkDate == targetDate &&
                !attendance.IsDeleted &&
                employees.Contains(attendance.EmployeeId))
            .Select(attendance => attendance.EmployeeId)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var createdOn = new DateTime(
            2026,
            7,
            25,
            16,
            30,
            0,
            DateTimeKind.Utc);
        var createdByPc = Environment.MachineName;

        foreach (var employeeId in employees)
        {
            if (existingEmployeeIds.Contains(employeeId))
            {
                continue;
            }

            dbContext.EmployeeAttendances.Add(new EmployeeAttendance
            {
                CompanyId = company.Id,
                EmployeeId = employeeId,
                WorkDate = targetDate,
                Status = AttendanceStatus.Present,
                CheckIn = new TimeOnly(8, 0, 0),
                CheckOut = new TimeOnly(16, 30, 0),
                WorkHours = new TimeOnly(8, 30, 0),
                WorkDayRatio = WorkDayRatio.FullDay,
                WorkLocation = "Main Office",
                Notes = "Development seed attendance",
                CreatedById = SeedActor,
                CreatedByPc = createdByPc,
                CreatedOn = createdOn
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedEmployeeTransactionsAsync(
        ApplicationDbContext dbContext,
        Company company,
        PayrollPeriod payrollPeriod,
        CancellationToken cancellationToken)
    {
        var seedEmails = DefaultEmployees
            .Select(employee => GetSeedEmployeeEmail(employee.Key, company.Id))
            .ToArray();
        var payrollEntries = await dbContext.PayrollEntries
            .Include(entry => entry.Employee)
            .Where(entry =>
                entry.CompanyId == company.Id &&
                entry.StartDate == payrollPeriod.StartDate &&
                entry.EndDate == payrollPeriod.EndDate &&
                entry.Employee.Email != null &&
                seedEmails.Contains(entry.Employee.Email))
            .OrderBy(entry => entry.EmployeeId)
            .ToListAsync(cancellationToken);

        if (payrollEntries.Count == 0)
        {
            return;
        }

        var cashbox = await dbContext.Cashboxes
            .FirstOrDefaultAsync(
                entity =>
                    entity.CompanyId == company.Id &&
                    entity.Code == "CASH-MAIN" &&
                    entity.IsActive,
                cancellationToken);
        var movementType = await dbContext.CashMovementTypes
            .FirstOrDefaultAsync(
                entity =>
                    entity.CompanyId == company.Id &&
                    entity.Name == "Other Receipt" &&
                    entity.Direction == CashDirection.Receipt &&
                    entity.IsActive,
                cancellationToken);

        if (cashbox is null || movementType is null)
        {
            throw new InvalidOperationException(
                $"Cash management seed dependencies are missing for company {company.Id}.");
        }

        var createdOn = new DateTime(
            2026,
            7,
            31,
            12,
            0,
            0,
            DateTimeKind.Utc);
        var createdByPc = Environment.MachineName;

        foreach (var payrollEntry in payrollEntries)
        {
            var existingTransaction = await dbContext.EmployeeTransactions
                .FirstOrDefaultAsync(
                    transaction =>
                        transaction.CompanyId == company.Id &&
                        transaction.SourceType == EmployeeTransactionSource.Payroll &&
                        transaction.SourceId == payrollEntry.Id,
                    cancellationToken);

            if (existingTransaction is not null)
            {
                payrollEntry.EmployeeTransactionId = existingTransaction.Id;
                payrollEntry.IsSalaryMoveToEmployeeAccount = true;
                payrollEntry.Employee.LastDayOfReceivingSalary = payrollPeriod.EndDate;
                continue;
            }

            var voucherNumber =
                $"SEED-PAYROLL-{company.Id}-{payrollEntry.EmployeeId}";
            var voucher = await dbContext.CashVouchers
                .FirstOrDefaultAsync(
                    entity =>
                        entity.CompanyId == company.Id &&
                        entity.VoucherNumber == voucherNumber,
                    cancellationToken);

            if (voucher is null)
            {
                voucher = new CashVoucher
                {
                    CompanyId = company.Id,
                    VoucherNumber = voucherNumber,
                    VoucherDate = payrollPeriod.EndDate,
                    Direction = CashDirection.Receipt,
                    CashboxId = cashbox.Id,
                    CashMovementTypeId = movementType.Id,
                    PartyType = CashPartyType.Employee,
                    EmployeeId = payrollEntry.EmployeeId,
                    Amount = payrollEntry.NetSalary,
                    Currency = cashbox.Currency,
                    IsPosted = true,
                    ReferenceNumber = $"PAYROLL-{payrollEntry.Id}",
                    Description = $"Payroll credit for {payrollEntry.EmployeeName}",
                    Notes = "Development seed payroll credit",
                    CreatedById = SeedActor,
                    CreatedByPc = createdByPc,
                    CreatedOn = createdOn
                };
                voucher.Touch(createdOn);
                voucher.ApplyExchangeRate(null, 1m);
                dbContext.CashVouchers.Add(voucher);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            var existingEntries = await dbContext.EmployeeTransactions
                .AsNoTracking()
                .Where(transaction =>
                    transaction.CompanyId == company.Id &&
                    transaction.EmployeeId == payrollEntry.EmployeeId)
                .Select(transaction => new
                {
                    transaction.Type,
                    transaction.Amount
                })
                .ToListAsync(cancellationToken);
            var runningBalance = existingEntries.Sum(transaction =>
                IsEmployeeCredit(transaction.Type)
                    ? transaction.Amount
                    : -transaction.Amount) + payrollEntry.NetSalary;

            var employeeTransaction = new EmployeeTransaction
            {
                CompanyId = company.Id,
                EmployeeId = payrollEntry.EmployeeId,
                Type = EmployeeTransactionType.Credit,
                Amount = payrollEntry.NetSalary,
                TransactionDate = payrollPeriod.EndDate,
                Notes = $"Development seed payroll credit for {payrollEntry.EmployeeName}",
                RunningBalance = runningBalance,
                SourceType = EmployeeTransactionSource.Payroll,
                SourceId = payrollEntry.Id,
                CashVoucherId = voucher.Id,
                CashBoxId = cashbox.Id,
                CreatedById = SeedActor,
                CreatedByPc = createdByPc,
                CreatedOn = createdOn
            };
            dbContext.EmployeeTransactions.Add(employeeTransaction);
            await dbContext.SaveChangesAsync(cancellationToken);

            payrollEntry.EmployeeTransactionId = employeeTransaction.Id;
            payrollEntry.IsSalaryMoveToEmployeeAccount = true;
            payrollEntry.Employee.LastDayOfReceivingSalary = payrollPeriod.EndDate;
        }

        payrollPeriod.TotalCredits = payrollEntries.Sum(entry => entry.NetSalary);
        payrollPeriod.TotalDebits = 0m;
        payrollPeriod.Status = PayrollPeriodStatus.Paid;
        payrollPeriod.PaidAt = createdOn;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task<PayrollPeriod> SeedPayrollPeriodsAsync(
        ApplicationDbContext dbContext,
        Company company,
        CancellationToken cancellationToken)
    {
        var startDate = new DateOnly(2026, 7, 1);
        var endDate = new DateOnly(2026, 7, 31);
        var existingPeriod = await dbContext.PayrollPeriods
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                period =>
                    period.CompanyId == company.Id &&
                    period.StartDate == startDate &&
                    period.EndDate == endDate &&
                    !period.IsDeleted,
                cancellationToken);

        if (existingPeriod is not null)
        {
            return existingPeriod;
        }

        var payrollPeriod = new PayrollPeriod
        {
            CompanyId = company.Id,
            Name = "July 2026",
            StartDate = startDate,
            EndDate = endDate,
            WorkingDaysInPeriod = 26,
            Status = PayrollPeriodStatus.Draft,
            CreatedById = SeedActor,
            CreatedByPc = Environment.MachineName,
            CreatedOn = new DateTime(
                2026,
                7,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc)
        };
        dbContext.PayrollPeriods.Add(payrollPeriod);

        await dbContext.SaveChangesAsync(cancellationToken);
        return payrollPeriod;
    }

    private static async Task SeedPayrollEntriesAsync(
        ApplicationDbContext dbContext,
        Company company,
        PayrollPeriod payrollPeriod,
        CancellationToken cancellationToken)
    {
        var seedEmails = DefaultEmployees
            .Select(employee => GetSeedEmployeeEmail(employee.Key, company.Id))
            .ToArray();
        var employees = await dbContext.Employees
            .Where(employee =>
                employee.CompanyId == company.Id &&
                employee.IsActive &&
                employee.Email != null &&
                seedEmails.Contains(employee.Email))
            .OrderBy(employee => employee.Id)
            .ToListAsync(cancellationToken);

        var existingEmployeeIds = (await dbContext.PayrollEntries
            .IgnoreQueryFilters()
            .Where(entry =>
                entry.CompanyId == company.Id &&
                entry.StartDate == payrollPeriod.StartDate &&
                entry.EndDate == payrollPeriod.EndDate &&
                !entry.IsDeleted)
            .Select(entry => entry.EmployeeId)
            .ToListAsync(cancellationToken))
            .ToHashSet();
        var employeeIds = employees
            .Select(employee => employee.Id)
            .ToArray();
        var attendances = await dbContext.EmployeeAttendances
            .AsNoTracking()
            .Where(attendance =>
                attendance.CompanyId == company.Id &&
                attendance.WorkDate >= payrollPeriod.StartDate &&
                attendance.WorkDate <= payrollPeriod.EndDate &&
                employeeIds.Contains(attendance.EmployeeId))
            .ToListAsync(cancellationToken);
        var createdOn = new DateTime(
            2026,
            7,
            31,
            10,
            0,
            0,
            DateTimeKind.Utc);

        foreach (var employee in employees)
        {
            if (existingEmployeeIds.Contains(employee.Id))
            {
                continue;
            }

            var employeeAttendances = attendances
                .Where(attendance => attendance.EmployeeId == employee.Id)
                .ToArray();
            var presentDays = employeeAttendances.Count(attendance =>
                attendance.Status == AttendanceStatus.Present);
            var absentDays = employeeAttendances.Count(attendance =>
                attendance.Status == AttendanceStatus.Absent);
            var workedDays = employeeAttendances
                .Where(attendance =>
                    attendance.Status == AttendanceStatus.Present)
                .Sum(attendance => GetWorkDayRatioValue(
                    attendance.WorkDayRatio));
            var overtimeDays = employeeAttendances
                .Where(attendance =>
                    attendance.Status == AttendanceStatus.Present)
                .Sum(attendance => GetWorkDayRatioValue(
                    attendance.WorkOverTimeRatio));
            var deductionDays = employeeAttendances
                .Where(attendance =>
                    attendance.Status == AttendanceStatus.Present)
                .Sum(attendance => GetWorkDayRatioValue(
                    attendance.WorkDaysDeductionRatio));
            var salaryPerDay = employee.Type == EmployeeType.Monthly
                ? employee.MonthlySalary!.Value /
                    employee.RequiredWorkingDaysPerMonth!.Value
                : employee.DailySalary!.Value;
            var grossSalary = employee.Type == EmployeeType.Monthly
                ? employee.MonthlySalary!.Value
                : employee.DailySalary!.Value;
            var calculatedSalary = salaryPerDay *
                (workedDays + overtimeDays - deductionDays);
            const decimal bonus = 100m;
            const decimal deduction = 25m;

            dbContext.PayrollEntries.Add(new PayrollEntry
            {
                StartDate = payrollPeriod.StartDate,
                EndDate = payrollPeriod.EndDate,
                CompanyId = company.Id,
                EmployeeId = employee.Id,
                EmployeeCode = employee.Code,
                EmployeeName = employee.Name,
                EmployeeType = employee.Type,
                PresentDays = presentDays,
                AbsentDays = absentDays,
                WorkedDaysbydayunit = workedDays,
                Overtimebydayunit = overtimeDays,
                Deductionbydayunit = deductionDays,
                RequiredWorkingDays = employee.RequiredWorkingDaysPerMonth,
                Bonus = bonus,
                Deduction = deduction,
                SalaryPerDay = salaryPerDay,
                CalculatedSalary = calculatedSalary,
                GrossSalary = grossSalary,
                NetSalary = calculatedSalary + bonus - deduction,
                IsSalaryMoveToEmployeeAccount = false,
                CreatedById = SeedActor,
                CreatedByPc = Environment.MachineName,
                CreatedOn = createdOn
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var periodEntries = await dbContext.PayrollEntries
            .AsNoTracking()
            .Where(entry =>
                entry.CompanyId == company.Id &&
                entry.StartDate == payrollPeriod.StartDate &&
                entry.EndDate == payrollPeriod.EndDate)
            .ToListAsync(cancellationToken);
        payrollPeriod.TotalEmployees = periodEntries
            .Select(entry => entry.EmployeeId)
            .Distinct()
            .Count();
        payrollPeriod.TotalMonthlyEmployees = periodEntries.Count(entry =>
            entry.EmployeeType == EmployeeType.Monthly);
        payrollPeriod.TotalDailyEmployees = periodEntries.Count(entry =>
            entry.EmployeeType == EmployeeType.Daily);
        payrollPeriod.TotalGrossSalary = periodEntries.Sum(entry =>
            entry.GrossSalary);
        payrollPeriod.TotalNetSalary = periodEntries.Sum(entry =>
            entry.NetSalary);
        payrollPeriod.TotalWorkedDays = periodEntries.Sum(entry =>
            entry.WorkedDaysbydayunit);
        payrollPeriod.TotalOvertimeDays = periodEntries.Sum(entry =>
            entry.Overtimebydayunit ?? 0m);
        payrollPeriod.TotalAbsentDays = periodEntries.Sum(entry =>
            entry.AbsentDays);
        payrollPeriod.CalculatedAt = createdOn;
        payrollPeriod.Status = PayrollPeriodStatus.Calculated;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string GetSeedEmployeeEmail(
        string key,
        int companyId) =>
        $"seed.employee.{key}.company-{companyId}@minierp.local";

    private static decimal GetWorkDayRatioValue(WorkDayRatio? ratio) =>
        ratio switch
        {
            WorkDayRatio.FullDay => 1m,
            WorkDayRatio.ThreeQuarterDay => 0.75m,
            WorkDayRatio.HalfDay => 0.5m,
            WorkDayRatio.ThirdDay => 1m / 3m,
            WorkDayRatio.QuarterDay => 0.25m,
            _ => 0m
        };

    private static bool IsEmployeeCredit(EmployeeTransactionType type) =>
        type is EmployeeTransactionType.Credit or EmployeeTransactionType.Bonus;

    private sealed record SeedUser(
        string UserName,
        string Email,
        string[] Roles,
        string FirstName,
        string LastName);

    private sealed record SeedCompany(
        string Name,
        string Address,
        string CommercialRegister,
        string TaxNumber,
        string ManagerName,
        DateTime CreatedOn);

    private sealed record SeedStore(
        string Code,
        string Name);

    private sealed record SeedCountry(
        string Code,
        string Name,
        string EnglishName);

    private sealed record SeedContainer(
        string Code,
        string Name,
        string Description);

    private sealed record SeedDriver(
        string Code,
        string Name,
        string PhoneSuffix,
        DateOnly LicenseExpiryDate);

    private sealed record SeedBusinessPartner(
        string Code,
        string Name,
        string PhoneSuffix,
        CurrencyCode Currency,
        decimal CreditLimit);

    private sealed record SeedPartnerOpeningBalance(
        string PartnerCode,
        string DocumentNumber,
        PartnerBalanceType BalanceType,
        decimal Amount);

    private sealed record SeedInvoiceDefinition(
        string ExportCode,
        string InvoiceNumber,
        InvoiceType InvoiceType,
        PaymentTerm PaymentTerm,
        DateOnly InvoiceDate,
        DateOnly? DueDate,
        int Sequence,
        string? SourceExportCode);

    private sealed record SeedEmployee(
        string Key,
        string Name,
        string JobTitle,
        EmployeeType Type,
        decimal Salary);

    private sealed record SeedCompanyContext(int CompanyId)
        : ICurrentCompanyContext;
}
