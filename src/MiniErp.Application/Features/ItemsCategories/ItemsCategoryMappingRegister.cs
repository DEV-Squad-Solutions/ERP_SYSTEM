using Mapster;
using MiniErp.Domain.Entities.Catalog;

namespace MiniErp.Application.Features.ItemsCategories;

public sealed class ItemsCategoryMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.ForType<ItemsCategoryRequest, ItemsCategory>()
            .Ignore(category => category.Id)
            .Ignore(category => category.CompanyId)
            .Ignore(category => category.Company)
            .Ignore(category => category.RowVersion)
            .Ignore(category => category.Invoices)
            .Map(category => category.Name, request => request.Name.Trim())
            .Map(category => category.IsActive, request => request.IsActive)
            .Map(category => category.Notes, request => Normalize(request.Notes));

        config.ForType<ItemsCategoryUpdateRequest, ItemsCategory>()
            .Ignore(category => category.Id)
            .Ignore(category => category.CompanyId)
            .Ignore(category => category.Company)
            .Ignore(category => category.RowVersion)
            .Ignore(category => category.Invoices)
            .Map(category => category.Name, request => request.Name.Trim())
            .Map(category => category.IsActive, request => request.IsActive)
            .Map(category => category.Notes, request => Normalize(request.Notes));

        config.ForType<ItemsCategory, ItemsCategoryResponse>()
            .Map(response => response.Id, category => category.Id)
            .Map(response => response.CompanyId, category => category.CompanyId)
            .Map(response => response.Name, category => category.Name)
            .Map(response => response.IsActive, category => category.IsActive)
            .Map(response => response.Notes, category => category.Notes)
            .Map(response => response.RowVersion, category => category.RowVersion);

        config.ForType<ItemsCategory, ItemsCategorySelectResponse>()
            .Map(response => response.Id, category => category.Id)
            .Map(response => response.Name, category => category.Name);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
