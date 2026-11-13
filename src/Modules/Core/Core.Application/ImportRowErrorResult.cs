namespace Core.Application;

public sealed record ImportRowErrorResult(
    int RowNumber,
    string Column,
    string ErrorCode,
    string Message);

