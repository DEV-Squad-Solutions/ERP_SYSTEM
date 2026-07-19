using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MiniErp.Domain.Common.Entities;

namespace MiniErp.Infrastructure.Persistence.Interceptors;

public sealed class AuditableEntityInterceptor(
    IHttpContextAccessor httpContextAccessor,
    TimeProvider timeProvider) : SaveChangesInterceptor
{
    private const string SystemActor = "system";

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyAuditInformation(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditInformation(eventData.Context);
        return base.SavingChangesAsync(
            eventData,
            result,
            cancellationToken);
    }

    private void ApplyAuditInformation(DbContext? dbContext)
    {
        if (dbContext is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var actorId = GetActorId();
        var client = GetClient();

        var entries = dbContext.ChangeTracker
            .Entries<AuditableEntity>()
            .Where(entry => entry.State is
                EntityState.Added or
                EntityState.Modified or
                EntityState.Deleted)
            .ToList();

        foreach (var entry in entries)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    SetCreatedInformation(entry.Entity, now, actorId, client);
                    break;

                case EntityState.Modified:
                    SetUpdatedInformation(entry.Entity, now, actorId, client);

                    if (BecameSoftDeleted(entry))
                    {
                        SetDeletedInformation(entry.Entity, now, actorId, client);
                    }

                    break;

                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    SetUpdatedInformation(entry.Entity, now, actorId, client);
                    SetDeletedInformation(entry.Entity, now, actorId, client);
                    break;
            }
        }
    }

    private static void SetCreatedInformation(
        AuditableEntity entity,
        DateTime now,
        string actorId,
        string client)
    {
        entity.CreatedOn = entity.CreatedOn == default
            ? now
            : entity.CreatedOn;
        entity.CreatedById = string.IsNullOrWhiteSpace(entity.CreatedById)
            ? actorId
            : entity.CreatedById;
        entity.CreatedByPc = string.IsNullOrWhiteSpace(entity.CreatedByPc)
            ? client
            : entity.CreatedByPc;
    }

    private static void SetUpdatedInformation(
        AuditableEntity entity,
        DateTime now,
        string actorId,
        string client)
    {
        entity.UpdatedOn = now;
        entity.UpdatedById = actorId;
        entity.UpdatedByPc = client;
    }

    private static void SetDeletedInformation(
        AuditableEntity entity,
        DateTime now,
        string actorId,
        string client)
    {
        entity.DeletedOn = now;
        entity.DeletedById = actorId;
        entity.DeletedByPc = client;
    }

    private static bool BecameSoftDeleted(EntityEntry<AuditableEntity> entry) =>
        entry.Entity.IsDeleted &&
        !entry.Property(entity => entity.IsDeleted).OriginalValue;

    private string GetActorId()
    {
        var user = httpContextAccessor.HttpContext?.User;

        return user?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user?.FindFirst("sub")?.Value
            ?? SystemActor;
    }

    private string GetClient() =>
        httpContextAccessor.HttpContext?
            .Connection
            .RemoteIpAddress?
            .ToString()
        ?? Environment.MachineName;
}
