using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.ContainerMovements;

public sealed record ContainerMovementResponse(
    int Id,
    int BusinessPartnerId,
    int ContainerStoreId,
    int ContainerId,
    string ContainerCode,
    string ContainerName,
    DateOnly MovementDate,
    int OutgoingUnits,
    int IncomingUnits,
    int NetUnits,
    string? Description,
    ContainerMovementSourceType SourceType,
    int? InvoiceId,
    string? InvoiceNumber,
    byte[] RowVersion);
