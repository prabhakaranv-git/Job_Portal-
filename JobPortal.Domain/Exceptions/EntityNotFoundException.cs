namespace JobPortal.Domain.Exceptions;

public class EntityNotFoundException : DomainException
{
    public string EntityName { get; }
    public object Key { get; }

    public EntityNotFoundException(string entityName, object key)
        : base($"Entity '{entityName}' with key '{key}' was not found.", 404)
    {
        EntityName = entityName;
        Key = key;
    }

    public EntityNotFoundException(string message)
        : base(message, 404)
    {
        EntityName = string.Empty;
        Key = string.Empty;
    }
}
