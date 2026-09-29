namespace JobPortal.Domain.Exceptions;

public class ForbiddenException : DomainException
{
    public ForbiddenException(string message = "You do not have permission to access this resource.") : base(message, 403)
    {
    }
}
