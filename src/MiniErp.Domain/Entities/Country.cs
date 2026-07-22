using MiniErp.Domain.Common.Entities;

namespace MiniErp.Domain.Entities;

public sealed class Country : AuditableEntity
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string ArabicName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
