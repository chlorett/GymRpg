namespace MyApp.Application.Dtos;

public record OperationResult
{
    public bool IsSuccess { get; init; }

    public bool IsFailure => !IsSuccess;

    public string? ErrorCode { get; init; }

    public string? Field { get; init; }

    public string? Message { get; init; }

    public static OperationResult Success() => new() { IsSuccess = true };

    public static OperationResult<T> Success<T>(T value) =>
        new() { IsSuccess = true, Value = value };

    public static OperationResult Fail(string errorCode, string? field = null, string? message = null) =>
        new() { IsSuccess = false, ErrorCode = errorCode, Field = field, Message = message };

    public static OperationResult<T> Fail<T>(string errorCode, string? field = null, string? message = null) =>
        new() { IsSuccess = false, Value = default, ErrorCode = errorCode, Field = field, Message = message };
}

public sealed record OperationResult<T> : OperationResult
{
    public T? Value { get; init; }
}
