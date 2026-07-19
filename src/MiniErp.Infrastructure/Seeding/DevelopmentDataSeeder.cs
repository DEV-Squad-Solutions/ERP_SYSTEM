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
    private const string DefaultRole = "User";
    private const string SeedActor = "seed";

    private static readonly string[] DefaultItemUnitNames =
    [
        "Piece",
        "Box",
        "Pack",
        "Kilogram",
        "Liter",
        "Meter"
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

        var userCount = Math.Clamp(
            configuration.GetValue("Seed:UserCount", 10),
            0,
            1_000);
        var itemCount = Math.Clamp(
            configuration.GetValue("Seed:ItemCount", 25),
            0,
            10_000);
        var roleName = configuration["Seed:Role"] ?? DefaultRole;

        await using var scope = services.CreateAsyncScope();
        var roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        if (!await roleManager.RoleExistsAsync(roleName))
        {
            var roleResult = await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
            EnsureSucceeded(roleResult, $"creating the '{roleName}' role");
        }

        var faker = new Faker("en");

        for (var index = 1; index <= userCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var email = $"demo{index}@minierp.local";
            if (await userManager.FindByEmailAsync(email) is not null)
            {
                continue;
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = faker.Name.FirstName(),
                LastName = faker.Name.LastName(),
                ProfileImage = $"https://i.pravatar.cc/150?u={Uri.EscapeDataString(email)}"
            };

            var userResult = await userManager.CreateAsync(user, password);
            EnsureSucceeded(userResult, $"creating user '{email}'");

            var roleResult = await userManager.AddToRoleAsync(user, roleName);
            EnsureSucceeded(roleResult, $"assigning '{roleName}' to '{email}'");
        }

        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await SeedCatalogAsync(dbContext, itemCount, cancellationToken);
    }

    private static async Task SeedCatalogAsync(
        ApplicationDbContext dbContext,
        int itemCount,
        CancellationToken cancellationToken)
    {
        var itemUnits = await dbContext.ItemUnits
            .Where(itemUnit => DefaultItemUnitNames.Contains(itemUnit.Name))
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
            .Where(item => item.Code.StartsWith("ITEM-"))
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
            .RuleFor(item => item.Code, faker => $"ITEM-{faker.IndexFaker + 1:0000}")
            .RuleFor(item => item.Name, faker => faker.Commerce.ProductName())
            .RuleFor(item => item.Description, faker => faker.Commerce.ProductDescription())
            .RuleFor(item => item.IsActive, _ => true)
            .RuleFor(item => item.CreatedById, _ => SeedActor)
            .RuleFor(item => item.CreatedByPc, _ => createdByPc)
            .RuleFor(item => item.CreatedOn, _ => createdOn)
            .UseSeed(20260719);

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
}
