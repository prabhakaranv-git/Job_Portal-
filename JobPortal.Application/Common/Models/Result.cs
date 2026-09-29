namespace JobPortal.Application.Common.Models;

public class Result<T>
{
    public bool Succeeded { get; }
    public T? Data { get; }
    public string? ErrorMessage { get; }
    public IDictionary<string, string[]>? ValidationErrors { get; }
    public int StatusCode { get; }

    private Result(bool succeeded, T? data, string? errorMessage, IDictionary<string, string[]>? validationErrors, int statusCode)
    {
        Succeeded = succeeded;
        Data = data;
        ErrorMessage = errorMessage;
        ValidationErrors = validationErrors;
        StatusCode = statusCode;
    }

    public static Result<T> Success(T data, int statusCode = 200) =>
        new(true, data, null, null, statusCode);

    public static Result<T> Failure(string errorMessage, int statusCode = 400) =>
        new(false, default, errorMessage, null, statusCode);

    public static Result<T> Failure(IDictionary<string, string[]> validationErrors, int statusCode = 400) =>
        new(false, default, "Validation failed", validationErrors, statusCode);
}
