using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Realtime;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Persistence.Realtime;

namespace MiniErp.Api.Realtime;

public sealed class RealtimeOutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    IHubContext<UpdatesHub> hubContext,
    TimeProvider timeProvider,
    ILogger<RealtimeOutboxDispatcher> logger) : BackgroundService
{
    private const int BatchSize = 100;
    private const int RetentionDays = 7;
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(1);
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);
    private DateTime nextCleanupAtUtc = DateTime.MinValue;

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var dispatched = await DispatchBatchAsync(stoppingToken);
                if (dispatched > 0)
                {
                    continue;
                }
            }
            catch (OperationCanceledException) when (
                stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Realtime outbox dispatch failed.");
            }

            await Task.Delay(IdleDelay, stoppingToken);
        }
    }

    private async Task<int> DispatchBatchAsync(
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var messages = await dbContext.RealtimeOutboxMessages
            .Where(message =>
                message.DispatchedAtUtc == null &&
                (message.NextAttemptAtUtc == null ||
                    message.NextAttemptAtUtc <= now))
            .OrderBy(message => message.OccurredAtUtc)
            .ThenBy(message => message.Id)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            await DispatchAsync(message, now, cancellationToken);
        }

        if (messages.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (now >= nextCleanupAtUtc)
        {
            await dbContext.RealtimeOutboxMessages
                .Where(message =>
                    message.DispatchedAtUtc != null &&
                    message.DispatchedAtUtc < now.AddDays(-RetentionDays))
                .ExecuteDeleteAsync(cancellationToken);
            nextCleanupAtUtc = now.AddHours(1);
        }

        return messages.Count;
    }

    private async Task DispatchAsync(
        RealtimeOutboxMessage message,
        DateTime now,
        CancellationToken cancellationToken)
    {
        try
        {
            var notification = JsonSerializer.Deserialize<
                RealtimeChangeNotification>(
                message.Payload,
                SerializerOptions)
                ?? throw new JsonException(
                    "Realtime outbox payload was empty.");

            await hubContext.Clients
                .Group(RealtimeHubGroups.Company(message.CompanyId))
                .SendAsync(
                    "entityChanged",
                    notification,
                    cancellationToken);

            message.DispatchedAtUtc = now;
            message.NextAttemptAtUtc = null;
            message.LastError = null;
        }
        catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            message.AttemptCount++;
            message.LastError = Truncate(exception.Message, 2_000);
            message.NextAttemptAtUtc = now.AddSeconds(
                Math.Min(
                    300,
                    Math.Pow(2, Math.Min(message.AttemptCount, 8))));

            logger.LogWarning(
                exception,
                "Realtime event {EventId} could not be dispatched on attempt {AttemptCount}.",
                message.Id,
                message.AttemptCount);
        }
    }

    private static string Truncate(string value, int maximumLength) =>
        value.Length <= maximumLength
            ? value
            : value[..maximumLength];
}
