namespace JobPortal.Domain.Common;

public interface IAuditableEntity
{
    Guid Id { get; }
    DateTime CreatedAt { get; set; }
    DateTime? UpdatedAt { get; set; }
}
