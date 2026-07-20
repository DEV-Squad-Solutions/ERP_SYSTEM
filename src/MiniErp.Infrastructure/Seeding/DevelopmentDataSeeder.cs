using Bogus;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MiniErp.Domain.Entities;
using MiniErp.Infrastructure.Identity;
using MiniErp.Infrastructure.Persistence;

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

    private static readonly SeedStore[] DefaultStores =
    [
        new("MAIN", "Main Store"),
        new("SALES", "Sales Store"),
        new("RETURNS", "Returns Store")
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

        var primaryCompany = await SeedCompaniesAsync(
            dbContext,
            cancellationToken);
        var companies = new List<Company> { primaryCompany };
        companies.AddRange(await SeedAdditionalCompaniesAsync(
            dbContext,
            cancellationToken));

        await SeedIdentityAsync(
            dbContext,
            userManager,
            roleManager,
            password,
            companies,
            cancellationToken);

        foreach (var company in companies)
        {
            await SeedStoresAsync(
                dbContext,
                company,
                cancellationToken);

            await SeedCatalogAsync(
                dbContext,
                company.Id,
                itemCount,
                cancellationToken);
        }
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
}
