namespace JobPortal.Domain.Exceptions;

public class UnauthorizedDomainException : DomainException
{
    public UnauthorizedDomainException(string message = "Unauthorized access.") : base(message, 401)
    {
    }
}
