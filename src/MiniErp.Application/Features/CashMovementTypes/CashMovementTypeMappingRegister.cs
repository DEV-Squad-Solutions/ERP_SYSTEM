using Mapster;
using MiniErp.Domain.Entities.CashManagement;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.CashMovementTypes;

public sealed class CashMovementTypeMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.ForType<CashMovementTypeRequest, CashMovementType>()
            .Map(
                movementType => movementType.Name,
                request => request.Name.Trim())
            .Map(
                movementType => movementType.PartnerEffect,
                request => request.ForPartner
                    ? request.Direction == CashDirection.Receipt
                        ? PartnerAccountEffect.Credit
                        : PartnerAccountEffect.Debit
                    : PartnerAccountEffect.None)
            .Map(
                movementType => movementType.Notes,
                request => Normalize(request.Notes));

        config.ForType<CashMovementTypeUpdateRequest, CashMovementType>()
            .Ignore(movementType => movementType.RowVersion)
            .Map(
                movementType => movementType.Name,
                request => request.Name.Trim())
            .Map(
                movementType => movementType.PartnerEffect,
                request => request.ForPartner
                    ? request.Direction == CashDirection.Receipt
                        ? PartnerAccountEffect.Credit
                        : PartnerAccountEffect.Debit
                    : PartnerAccountEffect.None)
            .Map(
                movementType => movementType.Notes,
                request => Normalize(request.Notes));

        config.ForType<CashMovementType, CashMovementTypeResponse>()
            .Map(
                response => response.ForPartner,
                movementType =>
                    movementType.PartnerEffect != PartnerAccountEffect.None);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
