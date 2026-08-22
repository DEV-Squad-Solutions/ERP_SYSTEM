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
            new DateOnly(2029, 12, 31))
    ];

    private static readonly SeedBusinessPartner[] DefaultBusinessPartners =
    [
        new("BP-001", "Ahmed Mohamed Trading", "123456", CurrencyCode.EGP, 50_000m),
        new("BP-002", "Al Salam Supplies", "234567", CurrencyCode.USD, 75_000m),
        new("BP-003", "Nile Distribution", "345678", CurrencyCode.EGP, 100_000m)
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

            await RecalculateSeedInventoryCostingAsync(
                dbContext,
                company.Id,
                cancellationToken);

            await SeedCashManagementAsync(
                dbContext,
                company,
                cancellationToken);

            //await SeedEmployeesAsync(
                //dbContext, 
                //company, 
                //cancellationToken);

            //await SeedAttendanceAsync(
            //    dbContext, 
            //    company, 
            //    cancellationToken);

            //await SeedEmployeeTransactionsAsync(
            //    dbContext, 
            //    company, 
            //    cancellationToken);

            //await SeedPayrollPeriodsAsync(
            //    dbContext, 
            //    company, 
            //    cancellationToken);

            //await SeedPayrollEntriesAsync(
            //    dbContext, 
            //    company, 
            //    cancellationToken);

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
        var seedBalances = new (string DocumentNumber, PartnerBalanceType BalanceType, decimal Amount)[]
        {
            ("PARTNER-OPEN-001", PartnerBalanceType.Receivable, 2_500m),
            ("PARTNER-OPEN-002", PartnerBalanceType.Payable, 1_750m)
        };

        var partners = await dbContext.BusinessPartners
            .Where(partner =>
                partner.CompanyId == company.Id &&
                partner.IsActive)
            .OrderBy(partner => partner.Id)
            .Take(seedBalances.Length)
            .Select(partner => new
            {
                partner.Id,
                partner.Currency
            })
            .ToListAsync(cancellationToken);

        for (var index = 0; index < partners.Count; index++)
        {
            var seed = seedBalances[index];
            var partner = partners[index];
            var notes = seed.BalanceType == PartnerBalanceType.Receivable
                ? "رصيد افتتاحي تجريبي مطلوب من العميل أو المورد"
                : "رصيد افتتاحي تجريبي مطلوب دفعه للعميل أو المورد";
            var existingBalance = await dbContext.PartnerOpeningBalances
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(balance =>
                    balance.CompanyId == company.Id &&
                    balance.DocumentNumber == seed.DocumentNumber,
                    cancellationToken);
            if (existingBalance is not null)
            {
                var oldNotes =
                    $"Seed {seed.BalanceType} for Company {company.Id}";
                if (existingBalance.Notes == oldNotes)
                {
                    existingBalance.Notes = notes;
                }

                if (existingBalance.Amount != 0m &&
                    existingBalance.BaseAmount == 0m)
                {
                    if (partner.Currency == CurrencyCode.EGP)
                    {
                        existingBalance.ApplyExchangeRate(null, 1m);
                    }
                    else
                    {
                        var existingRate = await dbContext.ExchangeRates
                            .Where(candidate =>
                                candidate.CompanyId == company.Id &&
                                candidate.Currency == partner.Currency &&
                                candidate.RateDate <=
                                    existingBalance.DocumentDate)
                            .OrderByDescending(candidate =>
                                candidate.RateDate)
                            .ThenByDescending(candidate => candidate.Id)
                            .FirstAsync(cancellationToken);
                        existingBalance.ApplyExchangeRate(
                            existingRate.Id,
                            existingRate.Rate);
                    }
                }

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
            if (partner.Currency == CurrencyCode.EGP)
            {
                openingBalance.ApplyExchangeRate(null, 1m);
            }
            else
            {
                var rate = await dbContext.ExchangeRates
                    .Where(candidate =>
                        candidate.CompanyId == company.Id &&
                        candidate.Currency == partner.Currency &&
                        candidate.RateDate <= openingBalance.DocumentDate)
                    .OrderByDescending(candidate => candidate.RateDate)
                    .ThenByDescending(candidate => candidate.Id)
                    .FirstAsync(cancellationToken);
                openingBalance.ApplyExchangeRate(rate.Id, rate.Rate);
            }

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
                (line.Invoice.ExportInvoiceCode == "SEED-CASH" ||
                 line.Invoice.ExportInvoiceCode == "SEED-CREDIT"))
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
        var existingExportCodes = await dbContext.Invoices
            .IgnoreQueryFilters()
            .Where(invoice =>
                invoice.CompanyId == company.Id &&
                (invoice.ExportInvoiceCode == "SEED-CASH" ||
                 invoice.ExportInvoiceCode == "SEED-CREDIT"))
            .Select(invoice => invoice.ExportInvoiceCode)
            .ToHashSetAsync(cancellationToken);

        var partner = await dbContext.BusinessPartners
            .Where(candidate =>
                candidate.CompanyId == company.Id &&
                candidate.IsActive)
            .OrderBy(candidate => candidate.Id)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.Currency
            })
            .FirstOrDefaultAsync(cancellationToken);
        var store = await dbContext.Stores
            .Where(candidate =>
                candidate.CompanyId == company.Id &&
                candidate.Code == "MAIN" &&
                candidate.IsActive &&
                !candidate.IsContainerStore)
            .Select(candidate => new { candidate.Id })
            .FirstOrDefaultAsync(cancellationToken);
        var item = await dbContext.Items
            .Where(candidate =>
                candidate.CompanyId == company.Id &&
                candidate.IsActive &&
                candidate.ItemUnit.IsActive)
            .OrderBy(candidate => candidate.Id)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.ItemUnitId
            })
            .FirstOrDefaultAsync(cancellationToken);
        var itemsCategoryId = await dbContext.ItemsCategories
            .Where(category =>
                category.CompanyId == company.Id &&
                category.Name == DefaultItemsCategoryNames[0] &&
                category.IsActive)
            .Select(category => (int?)category.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (partner is null || store is null || item is null)
        {
            return;
        }

        var seeds = new[]
        {
            (
                ExportCode: "SEED-CASH",
                InvoiceNumber: $"SEED-{company.Id}-CASH",
                PaymentTerm: PaymentTerm.Cash,
                DueDate: (DateOnly?)null),
            (
                ExportCode: "SEED-CREDIT",
                InvoiceNumber: $"SEED-{company.Id}-CREDIT",
                PaymentTerm: PaymentTerm.Credit,
                DueDate: (DateOnly?)new DateOnly(2026, 8, 24))
        };

        foreach (var seed in seeds)
        {
            if (existingExportCodes.Contains(seed.ExportCode))
            {
                continue;
            }

            var invoice = new Invoice
            {
                CompanyId = company.Id,
                InvoiceNumber = seed.InvoiceNumber,
                ExportInvoiceCode = seed.ExportCode,
                InvoiceType = InvoiceType.Sales,
                PaymentTerm = seed.PaymentTerm,
                InvoiceDate = new DateOnly(2026, 7, 25),
                DueDate = seed.DueDate,
                BusinessPartnerId = partner.Id,
                StoreId = store.Id,
                ItemsCategoryId = itemsCategoryId,
                Currency = partner.Currency,
                Notes = $"Seed {seed.PaymentTerm} invoice for Company {company.Id}",
                CreatedById = SeedActor,
                CreatedByPc = Environment.MachineName,
                CreatedOn = DateTime.UtcNow
            };
            var line = new InvoiceLine
            {
                CompanyId = company.Id,
                ItemId = item.Id,
                ItemUnitId = item.ItemUnitId,
                Count = 1,
                Weight = 1m,
                Price = 100m,
                Notes = "Seed invoice line"
            };
            line.CalculateAmounts();
            invoice.Lines.Add(line);
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

            invoice.Touch(DateTime.UtcNow);
            dbContext.Invoices.Add(invoice);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var seededInvoices = await dbContext.Invoices
            .Include(invoice => invoice.Lines)
            .Where(invoice =>
                invoice.CompanyId == company.Id &&
                (invoice.ExportInvoiceCode == "SEED-CASH" ||
                 invoice.ExportInvoiceCode == "SEED-CREDIT"))
            .ToListAsync(cancellationToken);

        foreach (var invoice in seededInvoices)
        {
            if (!invoice.ItemsCategoryId.HasValue)
            {
                invoice.ItemsCategoryId = itemsCategoryId;
                invoice.Touch(DateTime.UtcNow);
            }

            if (invoice.Total != 0m && invoice.BaseTotal == 0m)
            {
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
                        .OrderByDescending(candidate =>
                            candidate.RateDate)
                        .ThenByDescending(candidate => candidate.Id)
                        .FirstAsync(cancellationToken);
                    invoice.ApplyExchangeRate(
                        invoiceRate.Id,
                        invoiceRate.Rate);
                }
            }

            var hasItemMovements = await dbContext.ItemMovements.AnyAsync(
                movement =>
                    movement.CompanyId == company.Id &&
                    movement.ReferenceId == invoice.Id &&
                    movement.ReferenceNumber == invoice.InvoiceNumber,
                cancellationToken);
            if (!hasItemMovements)
            {
                var itemMovementType =
                    InvoiceMovementRules.GetItemMovementType(
                        invoice.InvoiceType);
                var inbound = InvoiceMovementRules.IsInbound(
                    invoice.InvoiceType);

                foreach (var line in invoice.Lines.Where(l => l.ItemId.HasValue))
                {
                    dbContext.ItemMovements.Add(
                        new ItemMovement
                        {
                            CompanyId = company.Id,
                            StoreId = invoice.StoreId,
                            ItemId = line.ItemId!.Value,
                            ItemUnitId = line.ItemUnitId,
                            MovementType = itemMovementType,
                            ReferenceId = invoice.Id,
                            ReferenceNumber = invoice.InvoiceNumber,
                            MovementDate = invoice.InvoiceDate,
                            QuantityIn = inbound ? line.Quantity : 0m,
                            QuantityOut = inbound ? 0m : line.Quantity,
                            Description = $"Invoice {invoice.InvoiceNumber}"
                        });
                }
            }

            var partnerMovement =
                await dbContext.BusinessPartnerMovements.FirstOrDefaultAsync(
                    movement =>
                        movement.CompanyId == company.Id &&
                        movement.InvoiceId == invoice.Id,
                    cancellationToken);
            if (partnerMovement is not null &&
                partnerMovement.Description ==
                $"Invoice {invoice.InvoiceNumber}")
            {
                partnerMovement.Description =
                    $"فاتورة {invoice.InvoiceNumber}";
            }
            else if (partnerMovement is null &&
                InvoiceMovementRules.ShouldCreatePartnerMovement(
                    invoice.RemainingAmount))
            {
                var movementType =
                    InvoiceMovementRules.GetPartnerMovementType(
                        invoice.InvoiceType);
                var (debit, credit) =
                    InvoiceMovementRules.GetPartnerAmounts(
                        invoice.InvoiceType,
                        invoice.RemainingAmount);
                var newPartnerMovement = new BusinessPartnerMovement
                {
                    CompanyId = company.Id,
                    BusinessPartnerId = invoice.BusinessPartnerId,
                    InvoiceId = invoice.Id,
                    MovementType = movementType,
                    MovementDate = invoice.InvoiceDate,
                    Currency = invoice.Currency,
                    Debit = debit,
                    Credit = credit,
                    Description = $"فاتورة {invoice.InvoiceNumber}"
                };
                newPartnerMovement.ApplyExchangeRate(invoice.ExchangeRate);
                dbContext.BusinessPartnerMovements.Add(newPartnerMovement);
            }
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
        var collectionType = existingMovementTypes.Single(entity =>
            entity.Direction == CashDirection.Receipt &&
            entity.Name == "Customer Collection");

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
        var existingEmployeeCodes = await dbContext.Employees
            .IgnoreQueryFilters()
            .Where(emp => emp.CompanyId == company.Id && emp.Code.StartsWith("EMP-"))
            .Select(emp => emp.Code)
            .ToHashSetAsync(cancellationToken);

        var createdOn = DateTime.UtcNow;
        var createdByPc = Environment.MachineName;

        // Using Bogus to generate diverse employee types based on your HR needs
        var employeeMonthlyFaker = new Faker<Employee>("en")
            .RuleFor(e => e.Name, f => f.Name.FirstName())
            .RuleFor(e => e.Email, f => f.Internet.Email())
            .RuleFor(e => e.PhoneNumber, f => f.Phone.PhoneNumber("010########"))
            .RuleFor(e => e.MonthlySalary, f => f.Finance.Amount(3000, 15000))
            .RuleFor(e => e.Type, EmployeeType.Monthly)// Assuming an enum or string for employee type (e.g., Daily/Monthly wage)
            .RuleFor(e => e.IsActive, _ => true)
            .RuleFor(e => e.CreatedById, _ => SeedActor)
            .RuleFor(e => e.CreatedByPc, _ => createdByPc)
            .RuleFor(e => e.CreatedOn, _ => createdOn)
            .UseSeed(20260801 + company.Id);
        var employeeDailyFaker = new Faker<Employee>("en")
            .RuleFor(e => e.Name, f => f.Name.FirstName())
            .RuleFor(e => e.Email, f => f.Internet.Email())
            .RuleFor(e => e.PhoneNumber, f => f.Phone.PhoneNumber("010########"))
            .RuleFor(e => e.DailySalary, f => f.Finance.Amount(100, 500))
            .RuleFor(e => e.Type, EmployeeType.Daily)// Assuming an enum or string for employee type (e.g., Daily/Monthly wage)
            .RuleFor(e => e.IsActive, _ => true)
            .RuleFor(e => e.CreatedById, _ => SeedActor)
            .RuleFor(e => e.CreatedByPc, _ => createdByPc)
            .RuleFor(e => e.CreatedOn, _ => createdOn)
            .UseSeed(20260801 + company.Id);

        var generatedEmployees = employeeMonthlyFaker.Generate(5).Concat(employeeDailyFaker.Generate(5)); // Seed 10 employees per company

        foreach (var employee in generatedEmployees)
        {
            if (existingEmployeeCodes.Contains(employee.Code))
            {
                continue;
            }

            dbContext.Employees.Add(employee);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedAttendanceAsync(
        ApplicationDbContext dbContext,
        Company company,
        CancellationToken cancellationToken)
    {
        var employees = await dbContext.Employees
            .Where(e => e.CompanyId == company.Id && e.IsActive)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);

        if (employees.Count == 0) return;

        var targetDate = new DateOnly(2026, 7, 25); // Example specific date
        var existingAttendance = await dbContext.EmployeeAttendances
            .IgnoreQueryFilters()
            .Where(a => a.CompanyId == company.Id && a.WorkDate == targetDate)
            .AnyAsync(cancellationToken);

        if (existingAttendance) return;

        var createdOn = DateTime.UtcNow;
        var createdByPc = Environment.MachineName;

        foreach (var employeeId in employees)
        {
            dbContext.EmployeeAttendances.Add(new EmployeeAttendance
            {
                CompanyId = company.Id,
                EmployeeId = employeeId,
                WorkDate = targetDate,
                CheckIn = new TimeOnly(8, 0, 0), // 8:00 AM
                CheckOut = new TimeOnly(16, 30, 0), // 4:30 PM
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
        CancellationToken cancellationToken)
    {
        var employees = await dbContext.Employees
            .Where(e => e.CompanyId == company.Id && e.IsActive)
            .Take(5) // Just seed transactions for the first 5 employees
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);

        if (employees.Count == 0) return;

        var existingTransactions = await dbContext.EmployeeTransactions
            .IgnoreQueryFilters()
            .Where(t => t.CompanyId == company.Id && t.Notes != null && t.Notes.Contains("Seed transaction"))
            .AnyAsync(cancellationToken);

        if (existingTransactions) return;

        var createdOn = DateTime.UtcNow;
        var createdByPc = Environment.MachineName;
        var transactionDate = new DateOnly(2026, 7, 20);

        foreach (var employeeId in employees)
        {
            dbContext.EmployeeTransactions.Add(new EmployeeTransaction
            {
                CompanyId = company.Id,
                EmployeeId = employeeId,
                TransactionDate = transactionDate,
                Amount = 500m,
                Type = EmployeeTransactionType.Credit,
                Notes = "Seed transaction - Performance Bonus",
                CreatedById = SeedActor,
                CreatedByPc = createdByPc,
                CreatedOn = createdOn
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedPayrollPeriodsAsync(
        ApplicationDbContext dbContext,
        Company company,
        CancellationToken cancellationToken)
    {
        var periodCode = "PR-2026-07";
        var existingPeriod = await dbContext.PayrollPeriods
            .IgnoreQueryFilters()
            .Where(p => p.CompanyId == company.Id && p.Code == periodCode)
            .AnyAsync(cancellationToken);

        if (existingPeriod) return;

        dbContext.PayrollPeriods.Add(new PayrollPeriod
        {
            CompanyId = company.Id,
            Code = periodCode,
            Name = "July 2026",
            StartDate = new DateOnly(2026, 7, 1),
            EndDate = new DateOnly(2026, 7, 31),
            Status=PayrollPeriodStatus.Draft,
            CreatedById = SeedActor,
            CreatedByPc = Environment.MachineName,
            CreatedOn = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    

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

    private sealed record SeedCompanyContext(int CompanyId)
        : ICurrentCompanyContext;
}
