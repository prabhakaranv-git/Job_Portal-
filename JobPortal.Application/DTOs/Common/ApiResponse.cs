namespace JobPortal.Application.DTOs.Common;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public IDictionary<string, string[]>? Errors { get; set; }
    public string? TraceId { get; set; }

    public static ApiResponse<T> Ok(T data, string message = "Success", string? traceId = null) =>
        new() { Success = true, Message = message, Data = data, TraceId = traceId };

    public static ApiResponse<T> Fail(string message, IDictionary<string, string[]>? errors = null, string? traceId = null) =>
        new() { Success = false, Message = message, Errors = errors, TraceId = traceId };
}

public class ApiResponse : ApiResponse<object>
{
    public static ApiResponse Ok(string message = "Success", string? traceId = null) =>
        new() { Success = true, Message = message, TraceId = traceId };

    public static new ApiResponse Fail(string message, IDictionary<string, string[]>? errors = null, string? traceId = null) =>
        new() { Success = false, Message = message, Errors = errors, TraceId = traceId };
}
