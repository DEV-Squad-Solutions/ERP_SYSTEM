using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using MiniErp.Application.Common.Authentication;
using MiniErp.Application.Common.Realtime;
using MiniErp.Domain.Common.Entities;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Infrastructure.Identity;
using MiniErp.Infrastructure.Persistence.Realtime;

namespace MiniErp.Infrastructure.Persistence.Interceptors;

public sealed class RealtimeChangeInterceptor(
    IHttpContextAccessor httpContextAccessor,
    TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public void EnqueueNotifications(DbContext? dbContext)
    {
        if (dbContext is null || dbContext.ChangeTracker
                .Entries<RealtimeOutboxMessage>()
                .Any(entry => entry.State == EntityState.Added))
        {
            return;
        }

        dbContext.ChangeTracker.DetectChanges();

        var entries = dbContext.ChangeTracker
            .Entries()
            .Where(IsBusinessChange)
            .ToArray();

        if (entries.Length == 0)
        {
            return;
        }

        var fallbackCompanyIds = ResolveFallbackCompanyIds(entries);
        var changesByCompany = new Dictionary<
            int,
            Dictionary<string, RealtimeEntityChange>>();

        foreach (var entry in entries)
        {
            var companyIds = ResolveCompanyIds(entry, fallbackCompanyIds);
            if (companyIds.Count == 0)
            {
                continue;
            }

            var change = BuildChange(entry);
            var key = BuildChangeKey(change);

            foreach (var companyId in companyIds)
            {
                if (!changesByCompany.TryGetValue(companyId, out var changes))
                {
                    changes = new Dictionary<string, RealtimeEntityChange>(
                        StringComparer.Ordinal);
                    changesByCompany.Add(companyId, changes);
                }

                changes.TryAdd(key, change);
            }
        }

        var occurredAtUtc = timeProvider.GetUtcNow().UtcDateTime;

        foreach (var (companyId, changes) in changesByCompany)
        {
            if (changes.Count == 0)
            {
                continue;
            }

            var eventId = Guid.NewGuid();
            var notification = new RealtimeChangeNotification(
                EventId: eventId,
                OccurredAtUtc: occurredAtUtc,
                Changes: changes.Values.ToArray());

            dbContext.Add(new RealtimeOutboxMessage
            {
                Id = eventId,
                CompanyId = companyId,
                OccurredAtUtc = occurredAtUtc,
                Payload = JsonSerializer.Serialize(
                    notification,
                    SerializerOptions)
            });
        }
    }

    private static bool IsBusinessChange(EntityEntry entry)
    {
        if (entry.State is not (
                EntityState.Added or
                EntityState.Modified or
                EntityState.Deleted))
        {
            return false;
        }

        var entityNamespace = entry.Metadata.ClrType.Namespace;
        return entityNamespace?.StartsWith(
                "MiniErp.Domain.Entities.",
                StringComparison.Ordinal) == true ||
            entry.Entity is UserCompany or ApplicationUser;
    }

    private IReadOnlySet<int> ResolveFallbackCompanyIds(
        IReadOnlyCollection<EntityEntry> entries)
    {
        var companyIds = entries
            .Select(TryGetCompanyId)
            .Where(companyId => companyId.HasValue)
            .Select(companyId => companyId!.Value)
            .ToHashSet();

        if (CompanyClaimResolver.TryGetCompanyId(
                httpContextAccessor.HttpContext?.User,
                out var currentCompanyId))
        {
            companyIds.Add(currentCompanyId);
        }

        return companyIds;
    }

    private static IReadOnlySet<int> ResolveCompanyIds(
        EntityEntry entry,
        IReadOnlySet<int> fallbackCompanyIds)
    {
        var companyId = TryGetCompanyId(entry);
        if (companyId.HasValue)
        {
            return new HashSet<int> { companyId.Value };
        }

        return fallbackCompanyIds;
    }

    private static int? TryGetCompanyId(EntityEntry entry)
    {
        var companyProperty = entry.Metadata.FindProperty("CompanyId");
        if (companyProperty is not null)
        {
            var value = entry.Property(companyProperty.Name).CurrentValue;
            if (value is int companyId && companyId > 0)
            {
                return companyId;
            }
        }

        if (entry.Entity is Company)
        {
            var value = entry.Property(nameof(Company.Id)).CurrentValue;
            if (value is int companyId && companyId > 0)
            {
                return companyId;
            }
        }

        return null;
    }

    private static RealtimeEntityChange BuildChange(EntityEntry entry) =>
        new(
            Resource: entry.Metadata.ClrType.Name,
            Action: ResolveAction(entry),
            EntityId: ResolveEntityId(entry),
            StoreIds: ResolveStoreIds(entry));

    private static string ResolveAction(EntityEntry entry)
    {
        if (entry.State == EntityState.Added)
        {
            return "Added";
        }

        if (entry.State == EntityState.Deleted || BecameSoftDeleted(entry))
        {
            return "Deleted";
        }

        return "Updated";
    }

    private static bool BecameSoftDeleted(EntityEntry entry)
    {
        if (entry.Entity is not AuditableEntity entity || !entity.IsDeleted)
        {
            return false;
        }

        var property = entry.Property(nameof(AuditableEntity.IsDeleted));
        return property.IsModified && property.OriginalValue is false;
    }

    private static string? ResolveEntityId(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();
        if (key is null)
        {
            return null;
        }

        var values = new List<string>(key.Properties.Count);
        foreach (var property in key.Properties)
        {
            var trackedProperty = entry.Property(property.Name);
            var value = trackedProperty.CurrentValue;
            if (value is null || trackedProperty.IsTemporary ||
                entry.State == EntityState.Added && IsDefaultValue(value))
            {
                return null;
            }

            values.Add(Convert.ToString(value, CultureInfo.InvariantCulture)!);
        }

        return string.Join(':', values);
    }

    private static bool IsDefaultValue(object value) => value switch
    {
        int intValue => intValue == 0,
        long longValue => longValue == 0,
        Guid guidValue => guidValue == Guid.Empty,
        _ => false
    };

    private static IReadOnlyList<int> ResolveStoreIds(EntityEntry entry) =>
        entry.Properties
            .Where(property =>
                property.Metadata.Name.EndsWith(
                    "StoreId",
                    StringComparison.Ordinal))
            .Select(property => property.CurrentValue)
            .OfType<int>()
            .Where(storeId => storeId > 0)
            .Distinct()
            .Order()
            .ToArray();

    private static string BuildChangeKey(RealtimeEntityChange change) =>
        string.Join(
            '|',
            change.Resource,
            change.Action,
            change.EntityId ?? string.Empty,
            string.Join(',', change.StoreIds));
}
