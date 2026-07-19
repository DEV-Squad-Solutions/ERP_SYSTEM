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
            "Admin",
            "System",
            "Administrator"),
        new(
            "user",
            "user@minierp.local",
            "User",
            "Application",
            "User")
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
        var roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        await SeedIdentityAsync(
            userManager,
            roleManager,
            password,
            cancellationToken);

        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await SeedCatalogAsync(dbContext, itemCount, cancellationToken);
    }

    private static async Task SeedIdentityAsync(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        string password,
        CancellationToken cancellationToken)
    {
        foreach (var roleName in SeedUsers
                     .Select(seedUser => seedUser.Role)
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

        var allowedUserNames = SeedUsers
            .Select(seedUser => seedUser.UserName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingUsers = await userManager.Users.ToListAsync(cancellationToken);

        foreach (var existingUser in existingUsers.Where(
                     user => !allowedUserNames.Contains(user.UserName ?? string.Empty)))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var deleteResult = await userManager.DeleteAsync(existingUser);
            EnsureSucceeded(
                deleteResult,
                $"deleting user '{existingUser.UserName}'");
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

            await SetOnlyRoleAsync(userManager, user, seedUser.Role);
        }
    }

    private static async Task SetOnlyRoleAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationUser user,
        string roleName)
    {
        var currentRoles = await userManager.GetRolesAsync(user);
        var rolesToRemove = currentRoles
            .Where(currentRole => !string.Equals(
                currentRole,
                roleName,
                StringComparison.OrdinalIgnoreCase))
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

        if (!await userManager.IsInRoleAsync(user, roleName))
        {
            var addResult = await userManager.AddToRoleAsync(user, roleName);
            EnsureSucceeded(
                addResult,
                $"assigning '{roleName}' to '{user.UserName}'");
        }
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

    private sealed record SeedUser(
        string UserName,
        string Email,
        string Role,
        string FirstName,
        string LastName);
}
