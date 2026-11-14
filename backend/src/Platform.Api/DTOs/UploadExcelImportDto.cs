using Microsoft.AspNetCore.Http;

namespace Platform.Api.DTOs;

public sealed class UploadExcelImportDto
{
    public IFormFile? File { get; init; }
    public Guid? OperationId { get; init; }
}

