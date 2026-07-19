namespace MiniErp.Domain.Common.Entities;

public abstract class AuditableEntity
{
    public string CreatedById { get; set; } = string.Empty;

    public DateTime CreatedOn { get; set; }

    public string CreatedByPc { get; set; } = string.Empty;

    public string? UpdatedById { get; set; }

    public DateTime? UpdatedOn { get; set; }

    public string? UpdatedByPc { get; set; }

    public string? DeletedById { get; set; }

    public DateTime? DeletedOn { get; set; }

    public string? DeletedByPc { get; set; }

    public bool IsDeleted { get; set; }
}
