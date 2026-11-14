using System.Globalization;
using System.Text.Json;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using ClosedXML.Excel;
using Core.Application;
using Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Core.Infrastructure;

/// <summary>
/// Implements Phase 14 bulk Excel (.xlsx) templates, dry-run validation preview, and atomic idempotent import
/// for Patients, InventoryItems, and Suppliers.
/// </summary>
public sealed class ExcelImportService : IExcelImportService
{
    private readonly CoreDbContext _dbContext;
    private readonly CurrentClinicContext _currentClinicContext;
    private readonly IClinicAuthorizationService _authorizationService;
    private readonly IAuditService _auditService;
    private readonly IClock _clock;

    public ExcelImportService(
        CoreDbContext dbContext,
        CurrentClinicContext currentClinicContext,
        IClinicAuthorizationService authorizationService,
        IAuditService auditService,
        IClock clock)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentClinicContext = currentClinicContext ?? throw new ArgumentNullException(nameof(currentClinicContext));
        _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<IReadOnlyList<ImportTargetKind>>> ListAvailableTargetsAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "excel_import.execute", "excel-import", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<ImportTargetKind>>.Failure(gate.Error);
        }

        return await ClinicTenantScopeHelper.ExecuteForClinicAsync(_currentClinicContext, clinicId, async () =>
        {
            List<string> enabled = await _dbContext.ClinicModules
                .AsNoTracking()
                .Where(m => m.ClinicId == clinicId && m.Enabled)
                .Select(m => m.ModuleKey)
                .ToListAsync(cancellationToken);

            var targets = new List<ImportTargetKind>();
            if (enabled.Contains("patients", StringComparer.OrdinalIgnoreCase))
            {
                targets.Add(ImportTargetKind.Patients);
            }

            if (enabled.Contains("inventory", StringComparer.OrdinalIgnoreCase))
            {
                targets.Add(ImportTargetKind.InventoryItems);
            }

            if (enabled.Contains("finance", StringComparer.OrdinalIgnoreCase) || enabled.Contains("inventory", StringComparer.OrdinalIgnoreCase))
            {
                targets.Add(ImportTargetKind.Suppliers);
            }

            return Result<IReadOnlyList<ImportTargetKind>>.Success(targets);
        });
    }

    public async Task<Result<ImportTemplateResult>> DownloadTemplateAsync(
        Guid userId,
        Guid clinicId,
        ImportTargetKind targetKind,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "excel_import.execute", "excel-import", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<ImportTemplateResult>.Failure(gate.Error);
        }

        return await ClinicTenantScopeHelper.ExecuteForClinicAsync(_currentClinicContext, clinicId, async () =>
        {
            Error? targetModuleError = await ValidateTargetModuleEnabledAsync(clinicId, targetKind, cancellationToken);
            if (targetModuleError != null)
            {
                return Result<ImportTemplateResult>.Failure(targetModuleError);
            }

            byte[] bytes = BuildTemplateWorkbook(targetKind);
            string fileName = $"template-{targetKind}.xlsx";
            return Result<ImportTemplateResult>.Success(new ImportTemplateResult(
                targetKind,
                fileName,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                bytes));
        });
    }

    public async Task<Result<ImportValidationResult>> PreviewImportAsync(
        Guid userId,
        Guid clinicId,
        ImportTargetKind targetKind,
        string fileName,
        byte[] content,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "excel_import.execute", "excel-import", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<ImportValidationResult>.Failure(gate.Error);
        }

        if (content.Length == 0)
        {
            return Result<ImportValidationResult>.Failure(Error.Validation("EMPTY_IMPORT_FILE", "Uploaded Excel file is empty."));
        }

        return await ClinicTenantScopeHelper.ExecuteForClinicAsync(_currentClinicContext, clinicId, async () =>
        {
            Error? targetModuleError = await ValidateTargetModuleEnabledAsync(clinicId, targetKind, cancellationToken);
            if (targetModuleError != null)
            {
                return Result<ImportValidationResult>.Failure(targetModuleError);
            }

            ParsedImportData parsed = await ParseAndValidateWorkbookAsync(clinicId, targetKind, fileName, content, cancellationToken);
            DateTimeOffset now = _clock.UtcNow;

            _dbContext.ImportBatchLogs.Add(new ImportBatchLog(
                GuidV7.NewGuid(),
                clinicId,
                targetKind,
                string.IsNullOrWhiteSpace(fileName) ? $"{targetKind}.xlsx" : fileName,
                parsed.TotalRows,
                parsed.ValidRowsCount,
                parsed.Errors.Count,
                isCommitted: false,
                userId.ToString(),
                now,
                operationId: null));
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result<ImportValidationResult>.Success(new ImportValidationResult(
                targetKind,
                fileName,
                parsed.TotalRows,
                parsed.ValidRowsCount,
                parsed.Errors.Count,
                CanCommit: parsed.TotalRows > 0 && parsed.Errors.Count == 0,
                Committed: false,
                parsed.Errors,
                parsed.PreviewSummaries));
        });
    }

    public async Task<Result<ImportValidationResult>> CommitImportAsync(
        Guid userId,
        Guid clinicId,
        ImportTargetKind targetKind,
        string fileName,
        byte[] content,
        Guid? operationId,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "excel_import.execute", "excel-import", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<ImportValidationResult>.Failure(gate.Error);
        }

        if (content.Length == 0)
        {
            return Result<ImportValidationResult>.Failure(Error.Validation("EMPTY_IMPORT_FILE", "Uploaded Excel file is empty."));
        }

        return await ClinicTenantScopeHelper.ExecuteForClinicAsync(_currentClinicContext, clinicId, async () =>
        {
            Error? targetModuleError = await ValidateTargetModuleEnabledAsync(clinicId, targetKind, cancellationToken);
            if (targetModuleError != null)
            {
                return Result<ImportValidationResult>.Failure(targetModuleError);
            }

            DateTimeOffset now = _clock.UtcNow;

            if (operationId.HasValue && operationId.Value != Guid.Empty)
            {
                ProcessedOperation? existingOp = await _dbContext.ProcessedOperations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.ClinicId == clinicId && p.OperationId == operationId.Value, cancellationToken);
                if (existingOp != null)
                {
                    ImportBatchLog? existingBatch = await _dbContext.ImportBatchLogs
                        .AsNoTracking()
                        .FirstOrDefaultAsync(b => b.ClinicId == clinicId && b.Id == existingOp.ResultEntityId, cancellationToken);
                    if (existingBatch != null)
                    {
                        return Result<ImportValidationResult>.Success(new ImportValidationResult(
                            existingBatch.TargetKind,
                            existingBatch.FileName,
                            existingBatch.TotalRows,
                            existingBatch.AcceptedRows,
                            existingBatch.RejectedRows,
                            CanCommit: true,
                            Committed: existingBatch.IsCommitted,
                            Array.Empty<ImportRowErrorResult>(),
                            Array.Empty<string>()));
                    }
                }
            }

            ParsedImportData parsed = await ParseAndValidateWorkbookAsync(clinicId, targetKind, fileName, content, cancellationToken);

            // Reject the entire batch if any row fails validation — zero partial inserts!
            if (parsed.TotalRows == 0 || parsed.Errors.Count > 0)
            {
                _dbContext.ImportBatchLogs.Add(new ImportBatchLog(
                    GuidV7.NewGuid(),
                    clinicId,
                    targetKind,
                    string.IsNullOrWhiteSpace(fileName) ? $"{targetKind}.xlsx" : fileName,
                    parsed.TotalRows,
                    parsed.ValidRowsCount,
                    parsed.Errors.Count,
                    isCommitted: false,
                    userId.ToString(),
                    now,
                    operationId));
                await _dbContext.SaveChangesAsync(cancellationToken);

                return Result<ImportValidationResult>.Success(new ImportValidationResult(
                    targetKind,
                    fileName,
                    parsed.TotalRows,
                    parsed.ValidRowsCount,
                    parsed.Errors.Count,
                    CanCommit: false,
                    Committed: false,
                    parsed.Errors,
                    parsed.PreviewSummaries));
            }

            var batchLog = new ImportBatchLog(
                GuidV7.NewGuid(),
                clinicId,
                targetKind,
                string.IsNullOrWhiteSpace(fileName) ? $"{targetKind}.xlsx" : fileName,
                parsed.TotalRows,
                parsed.ValidRowsCount,
                rejectedRows: 0,
                isCommitted: true,
                userId.ToString(),
                now,
                operationId);

            var strategy = _dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);

                switch (targetKind)
                {
                    case ImportTargetKind.Patients:
                        await InsertPatientsBatchAsync(clinicId, userId, deviceId, parsed.Patients, now, cancellationToken);
                        break;
                    case ImportTargetKind.InventoryItems:
                        await InsertInventoryBatchAsync(clinicId, parsed.InventoryItems, operationId, now, cancellationToken);
                        break;
                    case ImportTargetKind.Suppliers:
                        foreach (var s in parsed.Suppliers)
                        {
                            _dbContext.Suppliers.Add(new Supplier(
                                GuidV7.NewGuid(), clinicId, s.Name, s.SupplierType, s.Phone, s.Notes, operationId, now));
                        }
                        break;
                }

                _dbContext.ImportBatchLogs.Add(batchLog);
                if (operationId.HasValue && operationId.Value != Guid.Empty)
                {
                    _dbContext.ProcessedOperations.Add(new ProcessedOperation(
                        GuidV7.NewGuid(), clinicId, operationId.Value, "CommitExcelImport", batchLog.Id, now));
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
            });

            await _auditService.LogAsync(
                "ExcelImportCommitted",
                "ImportBatchLog",
                batchLog.Id.ToString(),
                null,
                JsonSerializer.Serialize(new { batchLog.Id, batchLog.TargetKind, batchLog.AcceptedRows }),
                userId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            return Result<ImportValidationResult>.Success(new ImportValidationResult(
                targetKind,
                fileName,
                parsed.TotalRows,
                parsed.ValidRowsCount,
                0,
                CanCommit: true,
                Committed: true,
                Array.Empty<ImportRowErrorResult>(),
                parsed.PreviewSummaries));
        });
    }

    public async Task<Result<IReadOnlyList<ImportBatchLogResult>>> ListImportHistoryAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(userId, clinicId, "excel_import.execute", "excel-import", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<ImportBatchLogResult>>.Failure(gate.Error);
        }

        return await ClinicTenantScopeHelper.ExecuteForClinicAsync(_currentClinicContext, clinicId, async () =>
        {
            List<ImportBatchLog> logs = await _dbContext.ImportBatchLogs
                .AsNoTracking()
                .Where(b => b.ClinicId == clinicId)
                .OrderByDescending(b => b.ExecutedAtUtc)
                .Take(100)
                .ToListAsync(cancellationToken);

            return Result<IReadOnlyList<ImportBatchLogResult>>.Success(logs.Select(b => new ImportBatchLogResult(
                b.Id, b.TargetKind, b.FileName, b.TotalRows, b.AcceptedRows, b.RejectedRows, b.IsCommitted, b.ExecutedAtUtc)).ToList());
        });
    }

    private async Task<Error?> ValidateTargetModuleEnabledAsync(Guid clinicId, ImportTargetKind targetKind, CancellationToken cancellationToken)
    {
        List<string> enabled = await _dbContext.ClinicModules
            .AsNoTracking()
            .Where(m => m.ClinicId == clinicId && m.Enabled)
            .Select(m => m.ModuleKey)
            .ToListAsync(cancellationToken);

        return targetKind switch
        {
            ImportTargetKind.Patients when !enabled.Contains("patients", StringComparer.OrdinalIgnoreCase)
                => Error.Forbidden("MODULE_DISABLED", "Module 'patients' is not enabled for this clinic."),
            ImportTargetKind.InventoryItems when !enabled.Contains("inventory", StringComparer.OrdinalIgnoreCase)
                => Error.Forbidden("MODULE_DISABLED", "Module 'inventory' is not enabled for this clinic."),
            ImportTargetKind.Suppliers when !enabled.Contains("finance", StringComparer.OrdinalIgnoreCase) && !enabled.Contains("inventory", StringComparer.OrdinalIgnoreCase)
                => Error.Forbidden("MODULE_DISABLED", "Neither 'finance' nor 'inventory' module is enabled for supplier imports."),
            _ => null
        };
    }

    private static byte[] BuildTemplateWorkbook(ImportTargetKind targetKind)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(targetKind.ToString());

        switch (targetKind)
        {
            case ImportTargetKind.Patients:
                WriteHeaderAndSample(
                    ws,
                    ["FullName", "Phone", "Gender", "DateOfBirth", "SpecialtyCode", "Address", "Notes"],
                    ["محمد علي حسن", "01012345678", "Male", "1992-05-14", "dental", "Maadi, Cairo", "Imported sample row"]);
                break;

            case ImportTargetKind.InventoryItems:
                WriteHeaderAndSample(
                    ws,
                    ["ItemCode", "NameAr", "NameEn", "UnitName", "PackSize", "UnitCost", "DefaultMarkupPercent", "LowStockThreshold", "SpecialtyCode", "StorageLocation"],
                    ["GLOVE-M-01", "قفاز طبي وسط", "Medical Gloves M", "glove", 100, 3.50m, 40m, 50m, "dental", "Cabinet-A"]);
                break;

            case ImportTargetKind.Suppliers:
                WriteHeaderAndSample(
                    ws,
                    ["Name", "SupplierType", "Phone", "Notes"],
                    ["Delta Dental Supply", "MedicalSupplies", "01099887766", "Primary dental consumables vendor"]);
                break;
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void WriteHeaderAndSample(IXLWorksheet ws, string[] headers, object[] sample)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
            ws.Cell(2, i + 1).Value = sample[i] switch
            {
                int n => n,
                decimal d => d,
                _ => sample[i].ToString() ?? string.Empty
            };
        }

        ws.Row(1).Style.Font.Bold = true;
    }

    private async Task<ParsedImportData> ParseAndValidateWorkbookAsync(
        Guid clinicId,
        ImportTargetKind targetKind,
        string fileName,
        byte[] content,
        CancellationToken cancellationToken)
    {
        var result = new ParsedImportData();
        XLWorkbook wb;
        try
        {
            using var ms = new MemoryStream(content);
            wb = new XLWorkbook(ms);
        }
        catch (Exception)
        {
            result.Errors.Add(new ImportRowErrorResult(1, "File", "INVALID_XLSX", "File is not a valid Excel (.xlsx) workbook."));
            return result;
        }

        using (wb)
        {
            IXLWorksheet? ws = wb.Worksheets.FirstOrDefault();
            if (ws == null)
            {
                result.Errors.Add(new ImportRowErrorResult(1, "Workbook", "MISSING_WORKSHEET", "Workbook contains no worksheets."));
                return result;
            }

            var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int lastCol = ws.Row(1).LastCellUsed()?.Address.ColumnNumber ?? 0;
            for (int col = 1; col <= lastCol; col++)
            {
                string h = ws.Cell(1, col).GetString().Trim();
                if (!string.IsNullOrWhiteSpace(h) && !headerMap.ContainsKey(h))
                {
                    headerMap[h] = col;
                }
            }

            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
            if (lastRow <= 1)
            {
                result.Errors.Add(new ImportRowErrorResult(1, "Rows", "NO_DATA_ROWS", "Worksheet has no data rows beneath the header."));
                return result;
            }

            switch (targetKind)
            {
                case ImportTargetKind.Patients:
                    await ParsePatientRowsAsync(clinicId, ws, headerMap, lastRow, result, cancellationToken);
                    break;
                case ImportTargetKind.InventoryItems:
                    await ParseInventoryRowsAsync(clinicId, ws, headerMap, lastRow, result, cancellationToken);
                    break;
                case ImportTargetKind.Suppliers:
                    await ParseSupplierRowsAsync(clinicId, ws, headerMap, lastRow, result, cancellationToken);
                    break;
            }
        }

        return result;
    }

    private async Task ParsePatientRowsAsync(
        Guid clinicId,
        IXLWorksheet ws,
        Dictionary<string, int> headers,
        int lastRow,
        ParsedImportData result,
        CancellationToken cancellationToken)
    {
        if (!headers.ContainsKey("FullName") || !headers.ContainsKey("Phone"))
        {
            result.Errors.Add(new ImportRowErrorResult(1, "Header", "MISSING_COLUMNS", "Required columns: FullName, Phone."));
            return;
        }

        List<string> existingPhones = await _dbContext.Patients
            .AsNoTracking()
            .Where(p => p.ClinicId == clinicId && !p.IsVoided)
            .Select(p => p.NormalizedPhone)
            .ToListAsync(cancellationToken);
        var seenPhones = new HashSet<string>(existingPhones, StringComparer.OrdinalIgnoreCase);

        for (int row = 2; row <= lastRow; row++)
        {
            string fullName = GetCell(ws, headers, row, "FullName");
            string phone = GetCell(ws, headers, row, "Phone");
            string gender = GetCell(ws, headers, row, "Gender");
            string dobText = GetCell(ws, headers, row, "DateOfBirth");
            string specialty = GetCell(ws, headers, row, "SpecialtyCode");
            string address = GetCell(ws, headers, row, "Address");
            if (string.IsNullOrWhiteSpace(fullName) && string.IsNullOrWhiteSpace(phone) && string.IsNullOrWhiteSpace(dobText))
            {
                continue;
            }

            result.TotalRows++;
            bool rowFailed = false;

            if (string.IsNullOrWhiteSpace(fullName))
            {
                result.Errors.Add(new ImportRowErrorResult(row, "FullName", "REQUIRED_FIELD", "FullName is required."));
                rowFailed = true;
            }

            string normalizedPhone = ArabicNormalizer.NormalizePhone(phone);
            if (string.IsNullOrWhiteSpace(normalizedPhone) || normalizedPhone.Length < 6)
            {
                result.Errors.Add(new ImportRowErrorResult(row, "Phone", "INVALID_PHONE", "Phone must contain at least 6 digits."));
                rowFailed = true;
            }
            else if (!seenPhones.Add(normalizedPhone))
            {
                result.Errors.Add(new ImportRowErrorResult(row, "Phone", "DUPLICATE_PHONE", $"Duplicate phone number '{phone}'."));
                rowFailed = true;
            }

            DateOnly? dob = null;
            if (!string.IsNullOrWhiteSpace(dobText))
            {
                if (DateOnly.TryParse(dobText, CultureInfo.InvariantCulture, out var parsedDob))
                {
                    dob = parsedDob;
                }
                else
                {
                    result.Errors.Add(new ImportRowErrorResult(row, "DateOfBirth", "INVALID_DATE", $"Invalid DateOfBirth '{dobText}'. Expected YYYY-MM-DD."));
                    rowFailed = true;
                }
            }

            if (!rowFailed)
            {
                result.ValidRowsCount++;
                string effGender = string.IsNullOrWhiteSpace(gender) ? "Unspecified" : gender.Trim();
                result.Patients.Add(new ParsedPatientRow(fullName, phone, dob, effGender, string.IsNullOrWhiteSpace(specialty) ? null : specialty.Trim(), string.IsNullOrWhiteSpace(address) ? null : address.Trim()));
                if (result.PreviewSummaries.Count < 10)
                {
                    result.PreviewSummaries.Add($"Row {row}: {fullName} ({phone})");
                }
            }
        }
    }

    private async Task ParseInventoryRowsAsync(
        Guid clinicId,
        IXLWorksheet ws,
        Dictionary<string, int> headers,
        int lastRow,
        ParsedImportData result,
        CancellationToken cancellationToken)
    {
        if (!headers.ContainsKey("ItemCode") || !headers.ContainsKey("NameEn"))
        {
            result.Errors.Add(new ImportRowErrorResult(1, "Header", "MISSING_COLUMNS", "Required columns: ItemCode, NameEn, UnitName, PackSize, UnitCost."));
            return;
        }

        List<string> existingCodes = await _dbContext.InventoryItems
            .AsNoTracking()
            .Where(i => i.ClinicId == clinicId && !i.IsVoided)
            .Select(i => i.Code)
            .ToListAsync(cancellationToken);
        var seenCodes = new HashSet<string>(existingCodes, StringComparer.OrdinalIgnoreCase);

        for (int row = 2; row <= lastRow; row++)
        {
            string itemCode = GetCell(ws, headers, row, "ItemCode");
            string nameAr = GetCell(ws, headers, row, "NameAr");
            string nameEn = GetCell(ws, headers, row, "NameEn");
            string unitName = GetCell(ws, headers, row, "UnitName");
            string packSizeText = GetCell(ws, headers, row, "PackSize");
            string unitCostText = GetCell(ws, headers, row, "UnitCost");
            string markupText = GetCell(ws, headers, row, "DefaultMarkupPercent");
            string lowStockText = GetCell(ws, headers, row, "LowStockThreshold");
            string specialty = GetCell(ws, headers, row, "SpecialtyCode");
            string location = GetCell(ws, headers, row, "StorageLocation");

            if (string.IsNullOrWhiteSpace(itemCode) && string.IsNullOrWhiteSpace(nameEn) && string.IsNullOrWhiteSpace(nameAr))
            {
                continue;
            }

            result.TotalRows++;
            bool rowFailed = false;

            if (string.IsNullOrWhiteSpace(itemCode))
            {
                result.Errors.Add(new ImportRowErrorResult(row, "ItemCode", "REQUIRED_FIELD", "ItemCode is required."));
                rowFailed = true;
            }
            else if (!seenCodes.Add(itemCode.Trim().ToUpperInvariant()))
            {
                result.Errors.Add(new ImportRowErrorResult(row, "ItemCode", "DUPLICATE_ITEM_CODE", $"Duplicate ItemCode '{itemCode}'."));
                rowFailed = true;
            }

            if (string.IsNullOrWhiteSpace(nameEn) && string.IsNullOrWhiteSpace(nameAr))
            {
                result.Errors.Add(new ImportRowErrorResult(row, "NameEn", "REQUIRED_FIELD", "NameEn or NameAr is required."));
                rowFailed = true;
            }

            if (!int.TryParse(packSizeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int packSize) || packSize < 1)
            {
                result.Errors.Add(new ImportRowErrorResult(row, "PackSize", "INVALID_NUMBER", "PackSize must be an integer >= 1."));
                rowFailed = true;
            }

            if (!decimal.TryParse(unitCostText, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal unitCost) || unitCost < 0m)
            {
                result.Errors.Add(new ImportRowErrorResult(row, "UnitCost", "INVALID_NUMBER", "UnitCost must be a non-negative decimal."));
                rowFailed = true;
            }

            decimal markup = 0m;
            if (!string.IsNullOrWhiteSpace(markupText) && (!decimal.TryParse(markupText, NumberStyles.Number, CultureInfo.InvariantCulture, out markup) || markup < 0m))
            {
                result.Errors.Add(new ImportRowErrorResult(row, "DefaultMarkupPercent", "INVALID_NUMBER", "DefaultMarkupPercent must be >= 0."));
                rowFailed = true;
            }

            decimal lowStock = 0m;
            if (!string.IsNullOrWhiteSpace(lowStockText) && (!decimal.TryParse(lowStockText, NumberStyles.Number, CultureInfo.InvariantCulture, out lowStock) || lowStock < 0m))
            {
                result.Errors.Add(new ImportRowErrorResult(row, "LowStockThreshold", "INVALID_NUMBER", "LowStockThreshold must be >= 0."));
                rowFailed = true;
            }

            if (!rowFailed)
            {
                result.ValidRowsCount++;
                result.InventoryItems.Add(new ParsedInventoryRow(
                    itemCode.Trim().ToUpperInvariant(),
                    string.IsNullOrWhiteSpace(nameAr) ? nameEn : nameAr,
                    string.IsNullOrWhiteSpace(nameEn) ? nameAr : nameEn,
                    string.IsNullOrWhiteSpace(unitName) ? "unit" : unitName,
                    packSize,
                    unitCost,
                    markup,
                    lowStock,
                    string.IsNullOrWhiteSpace(specialty) ? null : specialty.Trim().ToLowerInvariant(),
                    string.IsNullOrWhiteSpace(location) ? null : location.Trim()));
                if (result.PreviewSummaries.Count < 10)
                {
                    result.PreviewSummaries.Add($"Row {row}: {itemCode} - {nameEn} (Cost: {unitCost:F2})");
                }
            }
        }
    }

    private async Task ParseSupplierRowsAsync(
        Guid clinicId,
        IXLWorksheet ws,
        Dictionary<string, int> headers,
        int lastRow,
        ParsedImportData result,
        CancellationToken cancellationToken)
    {
        if (!headers.ContainsKey("Name") || !headers.ContainsKey("SupplierType"))
        {
            result.Errors.Add(new ImportRowErrorResult(1, "Header", "MISSING_COLUMNS", "Required columns: Name, SupplierType."));
            return;
        }

        List<string> existingNames = await _dbContext.Suppliers
            .AsNoTracking()
            .Where(s => s.ClinicId == clinicId && !s.IsVoided)
            .Select(s => s.Name)
            .ToListAsync(cancellationToken);
        var seenNames = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);

        for (int row = 2; row <= lastRow; row++)
        {
            string name = GetCell(ws, headers, row, "Name");
            string typeText = GetCell(ws, headers, row, "SupplierType");
            string phone = GetCell(ws, headers, row, "Phone");
            string notes = GetCell(ws, headers, row, "Notes");

            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(typeText))
            {
                continue;
            }

            result.TotalRows++;
            bool rowFailed = false;

            if (string.IsNullOrWhiteSpace(name))
            {
                result.Errors.Add(new ImportRowErrorResult(row, "Name", "REQUIRED_FIELD", "Supplier Name is required."));
                rowFailed = true;
            }
            else if (!seenNames.Add(name.Trim()))
            {
                result.Errors.Add(new ImportRowErrorResult(row, "Name", "DUPLICATE_SUPPLIER", $"Duplicate supplier '{name}'."));
                rowFailed = true;
            }

            if (!Enum.TryParse<SupplierType>(typeText, true, out SupplierType supType))
            {
                result.Errors.Add(new ImportRowErrorResult(row, "SupplierType", "INVALID_ENUM", $"Invalid SupplierType '{typeText}'. Allowed: Lab, General, MedicalSupplies."));
                rowFailed = true;
            }

            if (!rowFailed)
            {
                result.ValidRowsCount++;
                result.Suppliers.Add(new ParsedSupplierRow(
                    name.Trim(),
                    supType,
                    string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
                    string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()));
                if (result.PreviewSummaries.Count < 10)
                {
                    result.PreviewSummaries.Add($"Row {row}: {name} ({supType})");
                }
            }
        }
    }

    private async Task InsertPatientsBatchAsync(
        Guid clinicId,
        Guid userId,
        string? deviceId,
        List<ParsedPatientRow> rows,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return;
        }

        string lockResource = $"NumberBlock_{clinicId:N}_PatientFile";
        await _dbContext.Database.ExecuteSqlRawAsync(
            "EXEC sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;",
            [lockResource],
            cancellationToken);

        string effectiveDeviceId = string.IsNullOrWhiteSpace(deviceId) ? "excel-import" : deviceId.Trim();
        NumberBlock? block = await _dbContext.NumberBlocks
            .Where(n => n.ClinicId == clinicId && n.Kind == "PatientFile" && n.Status == "Active")
            .OrderBy(n => n.FromNumber)
            .FirstOrDefaultAsync(cancellationToken);

        foreach (var row in rows)
        {
            if (block == null || block.NextNumber > block.ToNumber)
            {
                long? maxTo = await _dbContext.NumberBlocks
                    .Where(n => n.ClinicId == clinicId && n.Kind == "PatientFile")
                    .MaxAsync(n => (long?)n.ToNumber, cancellationToken);

                long fromNum = (maxTo ?? 0L) + 1L;
                long toNum = fromNum + NumberBlock.DefaultBlockSize - 1L;
                block = new NumberBlock(GuidV7.NewGuid(), clinicId, effectiveDeviceId, "PatientFile", fromNum, toNum, now);
                _dbContext.NumberBlocks.Add(block);
            }

            var consumed = block.ConsumeNext();
            string fileNumber = $"P-{consumed.Value:D6}";
            _dbContext.Patients.Add(new Patient(
                GuidV7.NewGuid(),
                clinicId,
                fileNumber,
                row.FullName,
                row.Phone,
                secondaryPhone: null,
                row.DateOfBirth,
                approximateAgeYears: null,
                row.Gender,
                nationalId: null,
                row.Address,
                assignedDoctorId: null,
                row.SpecialtyCode,
                userId,
                now));
        }
    }

    private async Task InsertInventoryBatchAsync(
        Guid clinicId,
        List<ParsedInventoryRow> rows,
        Guid? operationId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        Clinic? clinic = await _dbContext.Clinics.FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
        string currency = clinic?.Currency ?? Money.DefaultCurrency;

        foreach (var row in rows)
        {
            _dbContext.InventoryItems.Add(new InventoryItem(
                GuidV7.NewGuid(),
                clinicId,
                row.Code,
                row.NameAr,
                row.NameEn,
                row.UnitName,
                row.PackSize,
                new Money(row.UnitCost, currency),
                row.MarkupPercent,
                row.LowStockThreshold,
                row.SpecialtyCode,
                row.StorageLocation,
                operationId,
                now));
        }
    }

    private static string GetCell(IXLWorksheet ws, Dictionary<string, int> headers, int row, string columnName) =>
        headers.TryGetValue(columnName, out int col) ? ws.Cell(row, col).GetString().Trim() : string.Empty;

    private sealed class ParsedImportData
    {
        public int TotalRows { get; set; }
        public int ValidRowsCount { get; set; }
        public List<ImportRowErrorResult> Errors { get; } = new();
        public List<string> PreviewSummaries { get; } = new();
        public List<ParsedPatientRow> Patients { get; } = new();
        public List<ParsedInventoryRow> InventoryItems { get; } = new();
        public List<ParsedSupplierRow> Suppliers { get; } = new();
    }

    private sealed record ParsedPatientRow(string FullName, string Phone, DateOnly? DateOfBirth, string Gender, string? SpecialtyCode, string? Address);

    private sealed record ParsedInventoryRow(string Code, string NameAr, string NameEn, string UnitName, int PackSize, decimal UnitCost, decimal MarkupPercent, decimal LowStockThreshold, string? SpecialtyCode, string? StorageLocation);

    private sealed record ParsedSupplierRow(string Name, SupplierType SupplierType, string? Phone, string? Notes);
}

