namespace MyApp.Application.Dtos;

public sealed record OperationResult
{
    public bool IsSuccess { get; init; }

    public string? ErrorCode { get; init; }

    public string? Field { get; init; }

    public string? Message { get; init; }

    public static OperationResult Success() => new() { IsSuccess = true };

    public static OperationResult Fail(string errorCode, string? field = null, string? message = null) =>
        new() { IsSuccess = false, ErrorCode = errorCode, Field = field, Message = message };
}
