namespace JobPortal.Domain.Exceptions;

public abstract class DomainException : Exception
{
    public int StatusCode { get; }

    protected DomainException(string message, int statusCode = 400) : base(message)
    {
        StatusCode = statusCode;
    }

    protected DomainException(string message, Exception innerException, int statusCode = 400) : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}
