using FluentValidation;

namespace MiniErp.Application.Features.StoreContainers;

public sealed class StoreContainerUpsertRequestValidator
    : AbstractValidator<StoreContainerUpsertRequest>
{
    public StoreContainerUpsertRequestValidator()
    {
        RuleFor(request => request.StoreId)
            .GreaterThan(0);

        RuleFor(request => request.ContainerIds)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(containerIds =>
                containerIds.Count <=
                StoreContainerUpsertRequest.MaximumContainerCount)
            .WithMessage(
                $"يجب ألا يزيد عدد العبوات عن " +
                $"{StoreContainerUpsertRequest.MaximumContainerCount}.")
            .Must(containerIds => containerIds.All(id => id > 0))
            .WithMessage("يجب أن تكون جميع أرقام العبوات أكبر من صفر.")
            .Must(containerIds =>
                containerIds.Count == containerIds.Distinct().Count())
            .WithMessage("يجب عدم تكرار رقم العبوة في القائمة.");
    }
}
