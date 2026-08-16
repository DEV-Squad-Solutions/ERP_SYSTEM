using MiniErp.Domain.Common.Entities;

namespace MiniErp.Domain.Entities.ReferenceData;

public sealed class Country : AuditableEntity
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string EnglishName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
