using Mapster;
using MiniErp.Domain.Entities.CashManagement;

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
                movementType => movementType.Notes,
                request => Normalize(request.Notes));

        config.ForType<CashMovementTypeUpdateRequest, CashMovementType>()
            .Ignore(movementType => movementType.RowVersion)
            .Map(
                movementType => movementType.Name,
                request => request.Name.Trim())
            .Map(
                movementType => movementType.Notes,
                request => Normalize(request.Notes));
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
