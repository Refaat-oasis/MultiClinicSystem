namespace BuildingBlocks.Domain;

/// <summary>
/// Domain error representation with code and descriptive message.
/// </summary>
public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);
    public static readonly Error NullValue = new("ERROR_NULL_VALUE", "A null value was provided where a non-null value was required.");

    public static Error NotFound(string code, string message) => new(code, message);
    public static Error Validation(string code, string message) => new(code, message);
    public static Error Conflict(string code, string message) => new(code, message);
    public static Error Failure(string code, string message) => new(code, message);
    public static Error Unauthorized(string code, string message) => new(code, message);
    public static Error Forbidden(string code, string message) => new(code, message);
}
