using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Core.Application;
using Core.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Core.Infrastructure;

/// <summary>
/// Implements Phase 10 Patient Portal lifecycle: public clinic landing resolution with redirect &amp; 404 parity,
/// isolated PatientAccount onboarding/authentication, read-only modular medical views, short-lived signed downloads,
/// clinic portal settings, access logs, and Super Admin read-only inspection.
/// </summary>
public sealed class PatientPortalService : IPatientPortalService
{
    private const string PortalRealm = "PatientPortal";
    private const string ActivationPurpose = "PatientActivation";
    private const string ResetPasswordPurpose = "PatientPasswordReset";
    private const string PrintedPinPurpose = "PatientPrintedPin";
    private const int SignedUrlTtlSeconds = 300;
    private const int PortalTokenMinutes = 30;
    private const int MaxHourlyCodes = 5;

    private readonly CoreDbContext _dbContext;
    private readonly CurrentClinicContext _currentClinicContext;
    private readonly IClinicAuthorizationService _authorizationService;
    private readonly IPatientPortalQueries _patientQueries;
    private readonly ISchedulingPortalQueries _schedulingQueries;
    private readonly IClinicalPortalQueries _clinicalQueries;
    private readonly ITreatmentPlanPortalQueries _treatmentPlanQueries;
    private readonly IVerificationCodes _verificationCodes;
    private readonly IOutbox _outbox;
    private readonly IAuditService _auditService;
    private readonly IClock _clock;
    private readonly PasswordHasher<PatientAccount> _passwordHasher = new();
    private readonly string _jwtSecret;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly string _signedUrlSecret;

    public PatientPortalService(
        CoreDbContext dbContext,
        CurrentClinicContext currentClinicContext,
        IClinicAuthorizationService authorizationService,
        IPatientPortalQueries patientQueries,
        ISchedulingPortalQueries schedulingQueries,
        IClinicalPortalQueries clinicalQueries,
        ITreatmentPlanPortalQueries treatmentPlanQueries,
        IVerificationCodes verificationCodes,
        IOutbox outbox,
        IAuditService auditService,
        IClock clock,
        IConfiguration configuration)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentClinicContext = currentClinicContext ?? throw new ArgumentNullException(nameof(currentClinicContext));
        _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
        _patientQueries = patientQueries ?? throw new ArgumentNullException(nameof(patientQueries));
        _schedulingQueries = schedulingQueries ?? throw new ArgumentNullException(nameof(schedulingQueries));
        _clinicalQueries = clinicalQueries ?? throw new ArgumentNullException(nameof(clinicalQueries));
        _treatmentPlanQueries = treatmentPlanQueries ?? throw new ArgumentNullException(nameof(treatmentPlanQueries));
        _verificationCodes = verificationCodes ?? throw new ArgumentNullException(nameof(verificationCodes));
        _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        ArgumentNullException.ThrowIfNull(configuration);

        _jwtSecret = configuration["Security:JwtSecret"]
                     ?? "clinic-platform-ultra-secure-jwt-secret-signing-key-minimum-32-chars";
        _issuer = configuration["Security:JwtIssuer"] ?? "ClinicPlatform";
        _audience = configuration["Security:JwtAudience"] ?? "ClinicPlatformClients";
        _signedUrlSecret = configuration["Security:PortalSignedUrlSecret"] ?? _jwtSecret;
    }

    public async Task<Result<PublicClinicLandingResult>> ResolvePublicLandingAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        var resolved = await ResolvePortalClinicAsync(slug, treatModuleDisabledAsNotFound: true, cancellationToken);
        if (resolved.IsFailure)
        {
            return Result<PublicClinicLandingResult>.Failure(resolved.Error);
        }

        PortalClinicResolution ctx = resolved.Value;
        ClinicPortalSetting settings = await GetOrBuildDefaultSettingsAsync(ctx.Clinic.Id, cancellationToken);
        IReadOnlyList<string> sections = BuildAvailableSections(ctx.EnabledModules);

        return Result<PublicClinicLandingResult>.Success(new PublicClinicLandingResult(
            ClinicId: ctx.Clinic.Id,
            Name: ctx.Clinic.Name,
            CurrentSlug: ctx.Clinic.Slug,
            RequestedSlug: ctx.RequestedSlug,
            IsRedirect: ctx.IsRedirect,
            RedirectUrl: $"/c/{ctx.Clinic.Slug}",
            LogoAssetId: ctx.Clinic.LogoAssetId,
            Address: FormatClinicAddress(ctx.Clinic),
            MainPhone: ctx.Clinic.MainPhone,
            WelcomeMessageAr: settings.WelcomeMessageAr,
            WelcomeMessageEn: settings.WelcomeMessageEn,
            TermsTextAr: settings.TermsTextAr,
            TermsTextEn: settings.TermsTextEn,
            AvailableSections: sections));
    }

    public async Task<Result<PatientPortalSessionResult>> ActivateWithEmailCodeAsync(
        string slug,
        ActivatePatientPortalInput input,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var resolved = await ResolvePortalClinicAsync(slug, treatModuleDisabledAsNotFound: false, cancellationToken);
        if (resolved.IsFailure)
        {
            return Result<PatientPortalSessionResult>.Failure(resolved.Error);
        }

        if (string.IsNullOrWhiteSpace(input.Email) ||
            string.IsNullOrWhiteSpace(input.VerificationCode) ||
            string.IsNullOrWhiteSpace(input.Password) ||
            input.Password.Length < 8)
        {
            return Result<PatientPortalSessionResult>.Failure(Error.Validation(
                "INVALID_ACTIVATION_INPUT",
                "Email, verification code, and a password of at least 8 characters are required."));
        }

        Guid clinicId = resolved.Value.Clinic.Id;
        string normalizedEmail = input.Email.Trim().ToUpperInvariant();
        DateTimeOffset now = _clock.UtcNow;

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            PatientAccount? account = await _dbContext.PatientAccounts
                .FirstOrDefaultAsync(
                    a => a.ClinicId == clinicId && !a.IsVoided && a.NormalizedEmail == normalizedEmail,
                    cancellationToken);

            if (account == null || account.Status == PatientAccountStatus.Disabled)
            {
                return Result<PatientPortalSessionResult>.Failure(Error.Validation(
                    "INVALID_OR_EXPIRED_CODE",
                    "The verification code is invalid or expired."));
            }

            VerificationCode? codeEntity = await _dbContext.VerificationCodes
                .Where(c => c.UserId == account.Id
                            && c.Realm == PortalRealm
                            && c.Purpose == ActivationPurpose
                            && c.UsedAtUtc == null)
                .OrderByDescending(c => c.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (codeEntity == null || codeEntity.IsExpired(now))
            {
                return Result<PatientPortalSessionResult>.Failure(Error.Validation(
                    "INVALID_OR_EXPIRED_CODE",
                    "The verification code is invalid or expired."));
            }

            if (codeEntity.IsDead())
            {
                return Result<PatientPortalSessionResult>.Failure(Error.Validation(
                    "MAX_ATTEMPTS_EXCEEDED",
                    "Maximum verification attempts exceeded. Please request a new invitation code."));
            }

            bool isValid = _verificationCodes.Verify(
                input.VerificationCode.Trim(),
                codeEntity.CodeHash,
                account.Id.ToString(),
                ActivationPurpose);

            if (!isValid)
            {
                codeEntity.RecordFailedAttempt();
                await _dbContext.SaveChangesAsync(cancellationToken);
                return Result<PatientPortalSessionResult>.Failure(Error.Validation(
                    "INVALID_OR_EXPIRED_CODE",
                    "The verification code is invalid or expired."));
            }

            codeEntity.MarkUsed(now);
            string passwordHash = _passwordHasher.HashPassword(account, input.Password);
            account.ActivateWithPassword(passwordHash, now);
            account.RecordLoginSuccess(now);

            _dbContext.PatientAccessLogs.Add(new PatientAccessLog(
                GuidV7.NewGuid(),
                account.PatientId.ToString(),
                "PortalLogin",
                now,
                clinicId,
                account.Id.ToString(),
                "Patient"));

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                "PatientPortalActivated",
                "PatientAccount",
                account.Id.ToString(),
                null,
                JsonSerializer.Serialize(new { account.Id, account.PatientId, account.ClinicId }),
                null,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            PortalContactSnapshotResult? contact = null;
            var contactResult = await _patientQueries.GetPatientPortalContactAsync(clinicId, account.PatientId, cancellationToken);
            if (contactResult.IsSuccess)
            {
                contact = contactResult.Value;
            }

            return Result<PatientPortalSessionResult>.Success(CreatePortalSessionResult(
                resolved.Value.Clinic,
                account,
                contact?.FullName ?? "Patient",
                now));
        });
    }

    public async Task<Result> RegisterPrintedPinAndSendEmailCodeAsync(
        string slug,
        RegisterPrintedPinInput input,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var resolved = await ResolvePortalClinicAsync(slug, treatModuleDisabledAsNotFound: false, cancellationToken);
        if (resolved.IsFailure)
        {
            return Result.Failure(resolved.Error);
        }

        if (string.IsNullOrWhiteSpace(input.FileNumber) ||
            string.IsNullOrWhiteSpace(input.PrintedPin) ||
            string.IsNullOrWhiteSpace(input.Email) ||
            !input.Email.Contains('@', StringComparison.Ordinal))
        {
            return Result.Failure(Error.Validation(
                "INVALID_ACTIVATION_CREDENTIALS",
                "Valid file number, date of birth, activation code, and email are required."));
        }

        Guid clinicId = resolved.Value.Clinic.Id;
        DateTimeOffset now = _clock.UtcNow;

        var patientResult = await _patientQueries.FindPatientByFileNumberAndDobForPortalAsync(
            clinicId,
            input.FileNumber,
            input.DateOfBirth,
            cancellationToken);

        if (patientResult.IsFailure)
        {
            return Result.Failure(Error.Validation(
                "INVALID_ACTIVATION_CREDENTIALS",
                "The file number, date of birth, or activation code could not be verified."));
        }

        Guid patientId = patientResult.Value.PatientId;
        string normalizedEmail = input.Email.Trim().ToUpperInvariant();

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            PatientAccount? account = await _dbContext.PatientAccounts
                .FirstOrDefaultAsync(a => a.ClinicId == clinicId && !a.IsVoided && a.PatientId == patientId, cancellationToken);

            if (account == null || !account.CanAttemptPrintedPin(now))
            {
                return Result.Failure(Error.Validation(
                    "INVALID_ACTIVATION_CREDENTIALS",
                    "The file number, date of birth, or activation code could not be verified."));
            }

            string normalizedPin = input.PrintedPin.Trim().ToUpperInvariant();
            bool pinValid = _verificationCodes.Verify(
                normalizedPin,
                account.PrintedCodeHmac!,
                account.Id.ToString(),
                PrintedPinPurpose);

            if (!pinValid)
            {
                account.RecordPrintedPinFailure(now);
                await _dbContext.SaveChangesAsync(cancellationToken);
                return Result.Failure(Error.Validation(
                    "INVALID_ACTIVATION_CREDENTIALS",
                    "The file number, date of birth, or activation code could not be verified."));
            }

            bool emailTaken = await _dbContext.PatientAccounts
                .AnyAsync(
                    a => a.ClinicId == clinicId
                         && !a.IsVoided
                         && a.Id != account.Id
                         && a.NormalizedEmail == normalizedEmail,
                    cancellationToken);

            if (emailTaken)
            {
                return Result.Failure(Error.Conflict(
                    "PORTAL_EMAIL_IN_USE",
                    "Another patient portal account in this clinic already uses that email."));
            }

            account.ConsumePrintedPinAndSetPendingEmail(input.Email.Trim(), now);
            await InvalidateUnusedCodesAsync(account.Id, ActivationPurpose, now, cancellationToken);

            string rawEmailCode = _verificationCodes.GenerateCode();
            string hmac = _verificationCodes.ComputeHmac(rawEmailCode, account.Id.ToString(), ActivationPurpose);
            _dbContext.VerificationCodes.Add(new VerificationCode(
                GuidV7.NewGuid(),
                account.Id,
                PortalRealm,
                ActivationPurpose,
                hmac,
                now.AddHours(24),
                now,
                maxAttempts: 5));

            await _dbContext.SaveChangesAsync(cancellationToken);

            var payload = new
            {
                to = account.Email,
                subject = $"{resolved.Value.Clinic.Name} - تأكيد البريد وتفعيل بوابة المريض / Portal Email Activation Code",
                body = $"رمز تأكيد البريد الإلكتروني وتفعيل حسابك في {resolved.Value.Clinic.Name} هو: {rawEmailCode} (ينتهي خلال 24 ساعة).\nYour portal email verification code for {resolved.Value.Clinic.Name} is: {rawEmailCode} (expires in 24 hours)."
            };
            await _outbox.EnqueueAsync("SendEmail", JsonSerializer.Serialize(payload), cancellationToken);

            await _auditService.LogAsync(
                "PatientPortalEmailCodeSent",
                "PatientAccount",
                account.Id.ToString(),
                null,
                JsonSerializer.Serialize(new { account.Id, account.PatientId }),
                null,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            return Result.Success();
        });
    }

    public async Task<Result<PatientPortalSessionResult>> LoginAsync(
        string slug,
        PatientPortalLoginInput input,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var resolved = await ResolvePortalClinicAsync(slug, treatModuleDisabledAsNotFound: false, cancellationToken);
        if (resolved.IsFailure)
        {
            return Result<PatientPortalSessionResult>.Failure(resolved.Error);
        }

        if (string.IsNullOrWhiteSpace(input.Email) || string.IsNullOrWhiteSpace(input.Password))
        {
            return Result<PatientPortalSessionResult>.Failure(Error.Unauthorized(
                "INVALID_CREDENTIALS",
                "Invalid email or password."));
        }

        Guid clinicId = resolved.Value.Clinic.Id;
        string normalizedEmail = input.Email.Trim().ToUpperInvariant();
        DateTimeOffset now = _clock.UtcNow;

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            PatientAccount? account = await _dbContext.PatientAccounts
                .FirstOrDefaultAsync(
                    a => a.ClinicId == clinicId && !a.IsVoided && a.NormalizedEmail == normalizedEmail,
                    cancellationToken);

            if (account == null || account.Status != PatientAccountStatus.Active || string.IsNullOrWhiteSpace(account.PasswordHash))
            {
                _dbContext.PatientAccessLogs.Add(new PatientAccessLog(
                    GuidV7.NewGuid(),
                    account?.PatientId.ToString() ?? "anonymous",
                    "PortalLoginFailed",
                    now,
                    clinicId,
                    null,
                    "Patient"));
                await _dbContext.SaveChangesAsync(cancellationToken);

                return Result<PatientPortalSessionResult>.Failure(Error.Unauthorized(
                    "INVALID_CREDENTIALS",
                    "Invalid email or password."));
            }

            if (account.IsLockedOut(now))
            {
                _dbContext.PatientAccessLogs.Add(new PatientAccessLog(
                    GuidV7.NewGuid(),
                    account.PatientId.ToString(),
                    "PortalLoginFailed",
                    now,
                    clinicId,
                    "locked",
                    "Patient"));
                await _dbContext.SaveChangesAsync(cancellationToken);

                return Result<PatientPortalSessionResult>.Failure(Error.Unauthorized(
                    "ACCOUNT_LOCKED",
                    "Account is temporarily locked due to repeated failed attempts."));
            }

            PasswordVerificationResult verify = _passwordHasher.VerifyHashedPassword(
                account,
                account.PasswordHash,
                input.Password);

            if (verify == PasswordVerificationResult.Failed)
            {
                account.RecordLoginFailure(now);
                _dbContext.PatientAccessLogs.Add(new PatientAccessLog(
                    GuidV7.NewGuid(),
                    account.PatientId.ToString(),
                    "PortalLoginFailed",
                    now,
                    clinicId,
                    null,
                    "Patient"));
                await _dbContext.SaveChangesAsync(cancellationToken);

                return Result<PatientPortalSessionResult>.Failure(Error.Unauthorized(
                    "INVALID_CREDENTIALS",
                    "Invalid email or password."));
            }

            account.RecordLoginSuccess(now);
            _dbContext.PatientAccessLogs.Add(new PatientAccessLog(
                GuidV7.NewGuid(),
                account.PatientId.ToString(),
                "PortalLogin",
                now,
                clinicId,
                account.Id.ToString(),
                "Patient"));
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                "PatientPortalLogin",
                "PatientAccount",
                account.Id.ToString(),
                null,
                null,
                null,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            var contactResult = await _patientQueries.GetPatientPortalContactAsync(clinicId, account.PatientId, cancellationToken);
            string fullName = contactResult.IsSuccess ? contactResult.Value.FullName : "Patient";

            return Result<PatientPortalSessionResult>.Success(
                CreatePortalSessionResult(resolved.Value.Clinic, account, fullName, now));
        });
    }

    public async Task<Result> ForgotPasswordAsync(
        string slug,
        string email,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var resolved = await ResolvePortalClinicAsync(slug, treatModuleDisabledAsNotFound: false, cancellationToken);
        if (resolved.IsFailure)
        {
            return Result.Failure(resolved.Error);
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return Result.Success();
        }

        Guid clinicId = resolved.Value.Clinic.Id;
        string normalizedEmail = email.Trim().ToUpperInvariant();
        DateTimeOffset now = _clock.UtcNow;

        await ExecuteForClinicAsync(clinicId, async () =>
        {
            PatientAccount? account = await _dbContext.PatientAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    a => a.ClinicId == clinicId && !a.IsVoided && a.NormalizedEmail == normalizedEmail,
                    cancellationToken);

            if (account == null || account.Status == PatientAccountStatus.Disabled || string.IsNullOrWhiteSpace(account.Email))
            {
                return true;
            }

            VerificationCode? recent = await _dbContext.VerificationCodes
                .Where(c => c.UserId == account.Id && c.Realm == PortalRealm && c.Purpose == ResetPasswordPurpose)
                .OrderByDescending(c => c.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (recent != null && (now - recent.CreatedAtUtc).TotalSeconds < 60)
            {
                return true;
            }

            int lastHourCount = await _dbContext.VerificationCodes
                .CountAsync(
                    c => c.UserId == account.Id
                         && c.Realm == PortalRealm
                         && c.Purpose == ResetPasswordPurpose
                         && c.CreatedAtUtc >= now.AddHours(-1),
                    cancellationToken);

            if (lastHourCount >= MaxHourlyCodes)
            {
                return true;
            }

            await InvalidateUnusedCodesAsync(account.Id, ResetPasswordPurpose, now, cancellationToken);

            string rawCode = _verificationCodes.GenerateCode();
            string hmac = _verificationCodes.ComputeHmac(rawCode, account.Id.ToString(), ResetPasswordPurpose);

            _dbContext.VerificationCodes.Add(new VerificationCode(
                GuidV7.NewGuid(),
                account.Id,
                PortalRealm,
                ResetPasswordPurpose,
                hmac,
                now.AddMinutes(15),
                now,
                maxAttempts: 5));

            await _dbContext.SaveChangesAsync(cancellationToken);

            var payload = new
            {
                to = account.Email,
                subject = $"{resolved.Value.Clinic.Name} - إعادة تعيين كلمة المرور / Portal Password Reset Code",
                body = $"رمز إعادة تعيين كلمة المرور لحسابك في {resolved.Value.Clinic.Name} هو: {rawCode} (ينتهي خلال 15 دقيقة).\nYour password reset code for {resolved.Value.Clinic.Name} is: {rawCode} (expires in 15 minutes)."
            };
            await _outbox.EnqueueAsync("SendEmail", JsonSerializer.Serialize(payload), cancellationToken);

            await _auditService.LogAsync(
                "PatientPortalPasswordResetRequested",
                "PatientAccount",
                account.Id.ToString(),
                null,
                null,
                null,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            return true;
        });

        return Result.Success();
    }

    public async Task<Result> ResetPasswordAsync(
        string slug,
        PatientPortalResetPasswordInput input,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var resolved = await ResolvePortalClinicAsync(slug, treatModuleDisabledAsNotFound: false, cancellationToken);
        if (resolved.IsFailure)
        {
            return Result.Failure(resolved.Error);
        }

        if (string.IsNullOrWhiteSpace(input.Email) ||
            string.IsNullOrWhiteSpace(input.VerificationCode) ||
            string.IsNullOrWhiteSpace(input.NewPassword) ||
            input.NewPassword.Length < 8)
        {
            return Result.Failure(Error.Validation(
                "INVALID_RESET_INPUT",
                "Email, verification code, and a new password of at least 8 characters are required."));
        }

        Guid clinicId = resolved.Value.Clinic.Id;
        string normalizedEmail = input.Email.Trim().ToUpperInvariant();
        DateTimeOffset now = _clock.UtcNow;

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            PatientAccount? account = await _dbContext.PatientAccounts
                .FirstOrDefaultAsync(
                    a => a.ClinicId == clinicId && !a.IsVoided && a.NormalizedEmail == normalizedEmail,
                    cancellationToken);

            if (account == null || account.Status == PatientAccountStatus.Disabled)
            {
                return Result.Failure(Error.Validation(
                    "INVALID_OR_EXPIRED_CODE",
                    "The verification code is invalid or expired."));
            }

            VerificationCode? codeEntity = await _dbContext.VerificationCodes
                .Where(c => c.UserId == account.Id
                            && c.Realm == PortalRealm
                            && c.Purpose == ResetPasswordPurpose
                            && c.UsedAtUtc == null)
                .OrderByDescending(c => c.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (codeEntity == null || codeEntity.IsExpired(now))
            {
                return Result.Failure(Error.Validation(
                    "INVALID_OR_EXPIRED_CODE",
                    "The verification code is invalid or expired."));
            }

            if (codeEntity.IsDead())
            {
                return Result.Failure(Error.Validation(
                    "MAX_ATTEMPTS_EXCEEDED",
                    "Maximum verification attempts exceeded. Please request a new reset code."));
            }

            bool valid = _verificationCodes.Verify(
                input.VerificationCode.Trim(),
                codeEntity.CodeHash,
                account.Id.ToString(),
                ResetPasswordPurpose);

            if (!valid)
            {
                codeEntity.RecordFailedAttempt();
                await _dbContext.SaveChangesAsync(cancellationToken);
                return Result.Failure(Error.Validation(
                    "INVALID_OR_EXPIRED_CODE",
                    "The verification code is invalid or expired."));
            }

            codeEntity.MarkUsed(now);
            string hash = _passwordHasher.HashPassword(account, input.NewPassword);
            account.ResetPassword(hash, now);
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(account.Email))
            {
                var noticePayload = new
                {
                    to = account.Email,
                    subject = $"{resolved.Value.Clinic.Name} - تم تغيير كلمة المرور / Portal Password Changed",
                    body = $"تم تغيير كلمة المرور الخاصة بحسابك في بوابة {resolved.Value.Clinic.Name} وإلغاء الجلسات النشطة.\nYour portal password for {resolved.Value.Clinic.Name} was changed and active sessions were revoked."
                };
                await _outbox.EnqueueAsync("SendEmail", JsonSerializer.Serialize(noticePayload), cancellationToken);
            }

            await _auditService.LogAsync(
                "PatientPortalPasswordReset",
                "PatientAccount",
                account.Id.ToString(),
                null,
                null,
                null,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            return Result.Success();
        });
    }

    public async Task<Result<PortalHomeOverviewResult>> GetHomeOverviewAsync(
        Guid accountId,
        Guid clinicId,
        Guid patientId,
        string slug,
        CancellationToken cancellationToken = default)
    {
        var access = await EnsureActivePortalSessionForSlugAsync(accountId, clinicId, patientId, slug, cancellationToken);
        if (access.IsFailure)
        {
            return Result<PortalHomeOverviewResult>.Failure(access.Error);
        }

        PortalClinicResolution ctx = access.Value.ClinicResolution;
        ClinicPortalSetting settings = await GetOrBuildDefaultSettingsAsync(clinicId, cancellationToken);
        HashSet<string> enabled = ctx.EnabledModules;

        var summaryResult = await _patientQueries.GetPatientSummaryForPortalAsync(clinicId, patientId, cancellationToken);
        if (summaryResult.IsFailure)
        {
            return Result<PortalHomeOverviewResult>.Failure(summaryResult.Error);
        }

        bool historyEnabled = enabled.Contains("patient-history");
        bool attachmentsEnabled = enabled.Contains("attachments");
        PortalMedicalBackgroundResult? medicalBackground = null;
        if (historyEnabled || attachmentsEnabled)
        {
            var bgResult = await _patientQueries.GetPatientMedicalBackgroundForPortalAsync(
                clinicId,
                patientId,
                includeMedicalBackground: historyEnabled,
                cancellationToken);

            if (bgResult.IsSuccess)
            {
                PortalMedicalBackgroundResult bg = bgResult.Value;
                medicalBackground = attachmentsEnabled
                    ? bg
                    : bg with { SharedAttachments = Array.Empty<PortalSharedAttachmentItemResult>() };
            }
        }

        IReadOnlyList<PortalUpcomingAppointmentItemResult>? upcoming = null;
        if (enabled.Contains("scheduling"))
        {
            var upRes = await _schedulingQueries.ListUpcomingAppointmentsForPortalAsync(
                clinicId,
                patientId,
                settings.ShowDoctorNames,
                cancellationToken);
            if (upRes.IsSuccess)
            {
                upcoming = upRes.Value;
            }
        }

        IReadOnlyList<PortalVisitSummaryResult>? visits = null;
        if (enabled.Contains("clinical"))
        {
            var visitRes = await _clinicalQueries.ListPatientVisibleVisitsAsync(clinicId, patientId, cancellationToken);
            if (visitRes.IsSuccess)
            {
                visits = ApplyDoctorNameMask(visitRes.Value, settings.ShowDoctorNames);
            }
        }

        IReadOnlyList<PortalTreatmentPlanItemResult>? plans = null;
        if (enabled.Contains("treatment-plans"))
        {
            var planRes = await _treatmentPlanQueries.GetPatientTreatmentPlansForPortalAsync(clinicId, patientId, cancellationToken);
            if (planRes.IsSuccess)
            {
                bool billingEnabled = enabled.Contains("billing");
                plans = ApplyBillingModuleMask(planRes.Value, billingEnabled);
            }
        }

        await WritePatientAccessLogAsync(clinicId, patientId, "PortalViewOverview", null, cancellationToken);

        return Result<PortalHomeOverviewResult>.Success(new PortalHomeOverviewResult(
            ClinicId: clinicId,
            ClinicName: ctx.Clinic.Name,
            ClinicSlug: ctx.Clinic.Slug,
            ClinicLogoAssetId: ctx.Clinic.LogoAssetId,
            WelcomeMessageAr: settings.WelcomeMessageAr,
            WelcomeMessageEn: settings.WelcomeMessageEn,
            AvailableSections: BuildAvailableSections(enabled),
            Summary: summaryResult.Value,
            MedicalBackground: medicalBackground,
            UpcomingAppointments: upcoming,
            Visits: visits,
            TreatmentPlans: plans));
    }

    public async Task<Result<PortalMedicalBackgroundResult>> GetMedicalRecordAsync(
        Guid accountId,
        Guid clinicId,
        Guid patientId,
        string slug,
        CancellationToken cancellationToken = default)
    {
        var access = await EnsureActivePortalSessionForSlugAsync(accountId, clinicId, patientId, slug, cancellationToken);
        if (access.IsFailure)
        {
            return Result<PortalMedicalBackgroundResult>.Failure(access.Error);
        }

        HashSet<string> enabled = access.Value.ClinicResolution.EnabledModules;
        bool historyEnabled = enabled.Contains("patient-history");
        bool attachmentsEnabled = enabled.Contains("attachments");
        if (!historyEnabled && !attachmentsEnabled)
        {
            return Result<PortalMedicalBackgroundResult>.Failure(Error.Forbidden(
                "MODULE_DISABLED",
                "Medical record module is disabled for this clinic."));
        }

        var bgResult = await _patientQueries.GetPatientMedicalBackgroundForPortalAsync(
            clinicId,
            patientId,
            includeMedicalBackground: historyEnabled,
            cancellationToken);
        if (bgResult.IsFailure)
        {
            return Result<PortalMedicalBackgroundResult>.Failure(bgResult.Error);
        }

        await WritePatientAccessLogAsync(clinicId, patientId, "PortalViewRecord", null, cancellationToken);

        PortalMedicalBackgroundResult bg = bgResult.Value;
        if (!attachmentsEnabled)
        {
            bg = bg with { SharedAttachments = Array.Empty<PortalSharedAttachmentItemResult>() };
        }

        return Result<PortalMedicalBackgroundResult>.Success(bg);
    }

    public async Task<Result<IReadOnlyList<PortalVisitSummaryResult>>> GetVisitsSectionAsync(
        Guid accountId,
        Guid clinicId,
        Guid patientId,
        string slug,
        CancellationToken cancellationToken = default)
    {
        var access = await EnsureActivePortalSessionForSlugAsync(accountId, clinicId, patientId, slug, cancellationToken);
        if (access.IsFailure)
        {
            return Result<IReadOnlyList<PortalVisitSummaryResult>>.Failure(access.Error);
        }

        if (!access.Value.ClinicResolution.EnabledModules.Contains("clinical"))
        {
            return Result<IReadOnlyList<PortalVisitSummaryResult>>.Failure(Error.Forbidden(
                "MODULE_DISABLED",
                "Clinical module is disabled for this clinic."));
        }

        ClinicPortalSetting settings = await GetOrBuildDefaultSettingsAsync(clinicId, cancellationToken);
        var visitRes = await _clinicalQueries.ListPatientVisibleVisitsAsync(clinicId, patientId, cancellationToken);
        if (visitRes.IsFailure)
        {
            return Result<IReadOnlyList<PortalVisitSummaryResult>>.Failure(visitRes.Error);
        }

        return Result<IReadOnlyList<PortalVisitSummaryResult>>.Success(
            ApplyDoctorNameMask(visitRes.Value, settings.ShowDoctorNames));
    }

    public async Task<Result<PortalVisitDetailResult>> GetVisitDetailAsync(
        Guid accountId,
        Guid clinicId,
        Guid patientId,
        string slug,
        Guid visitId,
        CancellationToken cancellationToken = default)
    {
        var access = await EnsureActivePortalSessionForSlugAsync(accountId, clinicId, patientId, slug, cancellationToken);
        if (access.IsFailure)
        {
            return Result<PortalVisitDetailResult>.Failure(access.Error);
        }

        HashSet<string> enabled = access.Value.ClinicResolution.EnabledModules;
        if (!enabled.Contains("clinical"))
        {
            return Result<PortalVisitDetailResult>.Failure(Error.Forbidden(
                "MODULE_DISABLED",
                "Clinical module is disabled for this clinic."));
        }

        ClinicPortalSetting settings = await GetOrBuildDefaultSettingsAsync(clinicId, cancellationToken);
        var visitRes = await _clinicalQueries.ListPatientVisibleVisitsAsync(clinicId, patientId, cancellationToken);
        if (visitRes.IsFailure)
        {
            return Result<PortalVisitDetailResult>.Failure(visitRes.Error);
        }

        PortalVisitSummaryResult? match = visitRes.Value.FirstOrDefault(v => v.VisitId == visitId);
        if (match == null)
        {
            return Result<PortalVisitDetailResult>.Failure(Error.NotFound("VISIT_NOT_FOUND", "Visit was not found."));
        }

        IReadOnlyList<PortalSharedAttachmentItemResult> attachments = Array.Empty<PortalSharedAttachmentItemResult>();
        if (enabled.Contains("attachments"))
        {
            var bgRes = await _patientQueries.GetPatientMedicalBackgroundForPortalAsync(
                clinicId,
                patientId,
                includeMedicalBackground: false,
                cancellationToken);
            if (bgRes.IsSuccess)
            {
                attachments = bgRes.Value.SharedAttachments;
            }
        }

        await WritePatientAccessLogAsync(clinicId, patientId, "PortalViewVisit", visitId.ToString(), cancellationToken);

        PortalVisitSummaryResult maskedVisit = settings.ShowDoctorNames
            ? match
            : match with { DoctorName = string.Empty };

        return Result<PortalVisitDetailResult>.Success(new PortalVisitDetailResult(maskedVisit, attachments));
    }

    public async Task<Result<IReadOnlyList<PortalTreatmentPlanItemResult>>> GetTreatmentPlansSectionAsync(
        Guid accountId,
        Guid clinicId,
        Guid patientId,
        string slug,
        CancellationToken cancellationToken = default)
    {
        var access = await EnsureActivePortalSessionForSlugAsync(accountId, clinicId, patientId, slug, cancellationToken);
        if (access.IsFailure)
        {
            return Result<IReadOnlyList<PortalTreatmentPlanItemResult>>.Failure(access.Error);
        }

        HashSet<string> enabled = access.Value.ClinicResolution.EnabledModules;
        if (!enabled.Contains("treatment-plans"))
        {
            return Result<IReadOnlyList<PortalTreatmentPlanItemResult>>.Failure(Error.Forbidden(
                "MODULE_DISABLED",
                "Treatment plans module is disabled for this clinic."));
        }

        var planRes = await _treatmentPlanQueries.GetPatientTreatmentPlansForPortalAsync(clinicId, patientId, cancellationToken);
        if (planRes.IsFailure)
        {
            return Result<IReadOnlyList<PortalTreatmentPlanItemResult>>.Failure(planRes.Error);
        }

        return Result<IReadOnlyList<PortalTreatmentPlanItemResult>>.Success(
            ApplyBillingModuleMask(planRes.Value, enabled.Contains("billing")));
    }

    public async Task<Result<PortalSignedUrlResult>> CreateSignedPrescriptionUrlAsync(
        Guid accountId,
        Guid clinicId,
        Guid patientId,
        string slug,
        Guid prescriptionId,
        CancellationToken cancellationToken = default)
    {
        var access = await EnsureActivePortalSessionForSlugAsync(accountId, clinicId, patientId, slug, cancellationToken);
        if (access.IsFailure)
        {
            return Result<PortalSignedUrlResult>.Failure(access.Error);
        }

        if (!access.Value.ClinicResolution.EnabledModules.Contains("clinical"))
        {
            return Result<PortalSignedUrlResult>.Failure(Error.Forbidden(
                "MODULE_DISABLED",
                "Clinical module is disabled for this clinic."));
        }

        var visitRes = await _clinicalQueries.ListPatientVisibleVisitsAsync(clinicId, patientId, cancellationToken);
        if (visitRes.IsFailure)
        {
            return Result<PortalSignedUrlResult>.Failure(visitRes.Error);
        }

        bool ownsPrescription = visitRes.Value.Any(v => v.Prescriptions.Any(p => p.Id == prescriptionId));
        if (!ownsPrescription)
        {
            return Result<PortalSignedUrlResult>.Failure(Error.NotFound("PRESCRIPTION_NOT_FOUND", "Prescription was not found."));
        }

        DateTimeOffset expiresAt = _clock.UtcNow.AddSeconds(SignedUrlTtlSeconds);
        string token = PortalSignedTokenHelper.GenerateToken(
            _signedUrlSecret,
            clinicId,
            patientId,
            "prescription",
            prescriptionId,
            expiresAt.ToUnixTimeSeconds());

        string url = $"/api/v1/c/{access.Value.ClinicResolution.Clinic.Slug}/portal/downloads/prescription/{prescriptionId:D}?token={Uri.EscapeDataString(token)}";
        return Result<PortalSignedUrlResult>.Success(new PortalSignedUrlResult(url, expiresAt));
    }

    public async Task<Result<PortalSignedUrlResult>> CreateSignedAttachmentUrlAsync(
        Guid accountId,
        Guid clinicId,
        Guid patientId,
        string slug,
        Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        var access = await EnsureActivePortalSessionForSlugAsync(accountId, clinicId, patientId, slug, cancellationToken);
        if (access.IsFailure)
        {
            return Result<PortalSignedUrlResult>.Failure(access.Error);
        }

        if (!access.Value.ClinicResolution.EnabledModules.Contains("attachments"))
        {
            return Result<PortalSignedUrlResult>.Failure(Error.Forbidden(
                "MODULE_DISABLED",
                "Attachments module is disabled for this clinic."));
        }

        var bgRes = await _patientQueries.GetPatientMedicalBackgroundForPortalAsync(
            clinicId,
            patientId,
            includeMedicalBackground: false,
            cancellationToken);
        if (bgRes.IsFailure || !bgRes.Value.SharedAttachments.Any(a => a.AttachmentId == attachmentId))
        {
            return Result<PortalSignedUrlResult>.Failure(Error.NotFound("ATTACHMENT_NOT_FOUND", "Attachment was not found."));
        }

        DateTimeOffset expiresAt = _clock.UtcNow.AddSeconds(SignedUrlTtlSeconds);
        string token = PortalSignedTokenHelper.GenerateToken(
            _signedUrlSecret,
            clinicId,
            patientId,
            "attachment",
            attachmentId,
            expiresAt.ToUnixTimeSeconds());

        string url = $"/api/v1/c/{access.Value.ClinicResolution.Clinic.Slug}/portal/downloads/attachment/{attachmentId:D}?token={Uri.EscapeDataString(token)}";
        return Result<PortalSignedUrlResult>.Success(new PortalSignedUrlResult(url, expiresAt));
    }

    public async Task<Result<PatientAttachmentDownloadResult>> ExecuteSignedDownloadAsync(
        string slug,
        string resourceKind,
        Guid resourceId,
        Guid patientId,
        string token,
        CancellationToken cancellationToken = default)
    {
        var resolved = await ResolvePortalClinicAsync(slug, treatModuleDisabledAsNotFound: false, cancellationToken);
        if (resolved.IsFailure)
        {
            return Result<PatientAttachmentDownloadResult>.Failure(resolved.Error);
        }

        Guid clinicId = resolved.Value.Clinic.Id;
        string normalizedKind = resourceKind.Trim().ToLowerInvariant();
        if (normalizedKind is not ("prescription" or "attachment"))
        {
            return Result<PatientAttachmentDownloadResult>.Failure(Error.NotFound("RESOURCE_NOT_FOUND", "Download resource was not found."));
        }

        bool validToken = PortalSignedTokenHelper.ValidateToken(
            _signedUrlSecret,
            token,
            clinicId,
            patientId,
            normalizedKind,
            resourceId,
            _clock.UtcNow);

        if (!validToken)
        {
            return Result<PatientAttachmentDownloadResult>.Failure(Error.Unauthorized(
                "INVALID_OR_EXPIRED_DOWNLOAD_TOKEN",
                "The signed download link is invalid or has expired."));
        }

        if (normalizedKind == "prescription")
        {
            if (!resolved.Value.EnabledModules.Contains("clinical"))
            {
                return Result<PatientAttachmentDownloadResult>.Failure(Error.Forbidden(
                    "MODULE_DISABLED",
                    "Clinical module is disabled for this clinic."));
            }

            var pdfResult = await _clinicalQueries.GetPatientPrescriptionPdfAsync(clinicId, patientId, resourceId, cancellationToken);
            if (pdfResult.IsFailure)
            {
                return Result<PatientAttachmentDownloadResult>.Failure(pdfResult.Error);
            }

            return Result<PatientAttachmentDownloadResult>.Success(new PatientAttachmentDownloadResult(
                pdfResult.Value.FileName,
                pdfResult.Value.ContentType,
                pdfResult.Value.ContentBytes));
        }

        if (!resolved.Value.EnabledModules.Contains("attachments"))
        {
            return Result<PatientAttachmentDownloadResult>.Failure(Error.Forbidden(
                "MODULE_DISABLED",
                "Attachments module is disabled for this clinic."));
        }

        return await _patientQueries.DownloadSharedAttachmentForPortalAsync(clinicId, patientId, resourceId, cancellationToken);
    }

    public async Task<Result<PatientPortalAdminStatusResult>> GetPatientPortalAdminStatusAsync(
        Guid staffUserId,
        Guid clinicId,
        Guid patientId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(staffUserId, clinicId, "portal.manage", "portal", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<PatientPortalAdminStatusResult>.Failure(gate.Error);
        }

        var contactRes = await _patientQueries.GetPatientPortalContactAsync(clinicId, patientId, cancellationToken);
        if (contactRes.IsFailure)
        {
            return Result<PatientPortalAdminStatusResult>.Failure(contactRes.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            PatientAccount? account = await _dbContext.PatientAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.ClinicId == clinicId && !a.IsVoided && a.PatientId == patientId, cancellationToken);

            PortalConsent? consent = await _dbContext.PortalConsents
                .AsNoTracking()
                .Where(c => c.ClinicId == clinicId && !c.IsVoided && c.PatientId == patientId)
                .OrderByDescending(c => c.AcceptedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            return Result<PatientPortalAdminStatusResult>.Success(new PatientPortalAdminStatusResult(
                PatientId: patientId,
                HasAccount: account != null,
                Status: account?.Status,
                Email: account?.Email ?? contactRes.Value.Email,
                IsEmailConfirmed: account?.IsEmailConfirmed ?? false,
                IsGuardianAccount: account?.IsGuardianAccount ?? false,
                GuardianName: account?.GuardianName ?? contactRes.Value.GuardianName,
                ConsentAcceptedAtUtc: consent?.AcceptedAtUtc,
                ConsentTermsVersion: consent?.TermsVersion,
                LastLoginAtUtc: account?.LastLoginAtUtc,
                GeneratedPrintedPin: null,
                PrintedPinExpiresAtUtc: account?.PrintedCodeExpiresAtUtc));
        });
    }

    public async Task<Result<PatientPortalAdminStatusResult>> InvitePatientAsync(
        Guid staffUserId,
        Guid clinicId,
        Guid patientId,
        InvitePatientToPortalInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var gate = await _authorizationService.EnsureClinicAccessAsync(staffUserId, clinicId, "portal.invite", "portal", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<PatientPortalAdminStatusResult>.Failure(gate.Error);
        }

        if (!input.ConsentAccepted)
        {
            return Result<PatientPortalAdminStatusResult>.Failure(Error.Validation(
                "CONSENT_REQUIRED",
                "Patient or guardian consent is required before inviting to the portal."));
        }

        var contactRes = await _patientQueries.GetPatientPortalContactAsync(clinicId, patientId, cancellationToken);
        if (contactRes.IsFailure)
        {
            return Result<PatientPortalAdminStatusResult>.Failure(contactRes.Error);
        }

        PortalContactSnapshotResult contact = contactRes.Value;
        string? emailToUse = !string.IsNullOrWhiteSpace(input.Email)
            ? input.Email.Trim()
            : (input.IsGuardianAccount ? contact.GuardianEmail : contact.Email);

        bool hasEmail = !string.IsNullOrWhiteSpace(emailToUse);
        if (!hasEmail && !input.IssuePrintedPinIfNoEmail)
        {
            return Result<PatientPortalAdminStatusResult>.Failure(Error.Validation(
                "EMAIL_REQUIRED",
                "An email address is required unless issuing a one-time printed activation code."));
        }

        string? guardianName = input.IsGuardianAccount
            ? (!string.IsNullOrWhiteSpace(input.GuardianName) ? input.GuardianName.Trim() : contact.GuardianName)
            : null;
        if (input.IsGuardianAccount && string.IsNullOrWhiteSpace(guardianName))
        {
            return Result<PatientPortalAdminStatusResult>.Failure(Error.Validation(
                "GUARDIAN_NAME_REQUIRED",
                "Guardian name is required for a guardian portal account."));
        }

        if (!hasEmail && !contact.DateOfBirth.HasValue)
        {
            return Result<PatientPortalAdminStatusResult>.Failure(Error.Validation(
                "DOB_REQUIRED_FOR_PRINTED_PIN",
                "Date of birth is required on the patient file to use a printed activation code."));
        }

        DateTimeOffset now = _clock.UtcNow;
        Clinic clinic = await _dbContext.Clinics.AsNoTracking().FirstAsync(c => c.Id == clinicId, cancellationToken);
        string termsVersion = string.IsNullOrWhiteSpace(input.TermsVersion) ? "v1" : input.TermsVersion.Trim();

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            if (hasEmail)
            {
                string normEmail = emailToUse!.ToUpperInvariant();
                bool emailInUse = await _dbContext.PatientAccounts
                    .AnyAsync(
                        a => a.ClinicId == clinicId
                             && !a.IsVoided
                             && a.PatientId != patientId
                             && a.NormalizedEmail == normEmail,
                        cancellationToken);

                if (emailInUse)
                {
                    return Result<PatientPortalAdminStatusResult>.Failure(Error.Conflict(
                        "PORTAL_EMAIL_IN_USE",
                        "Another patient portal account in this clinic already uses that email."));
                }
            }

            var consent = new PortalConsent(
                GuidV7.NewGuid(),
                clinicId,
                patientId,
                staffUserId,
                now,
                termsVersion,
                "StaffRecorded",
                null);
            _dbContext.PortalConsents.Add(consent);

            PatientAccount? account = await _dbContext.PatientAccounts
                .FirstOrDefaultAsync(a => a.ClinicId == clinicId && !a.IsVoided && a.PatientId == patientId, cancellationToken);

            if (account == null)
            {
                account = new PatientAccount(
                    GuidV7.NewGuid(),
                    clinicId,
                    patientId,
                    emailToUse,
                    input.IsGuardianAccount,
                    guardianName,
                    now);
                _dbContext.PatientAccounts.Add(account);
            }
            else
            {
                account.UpdateContactAndGuardian(emailToUse, input.IsGuardianAccount, guardianName, now);
            }

            string? generatedPin = null;
            DateTimeOffset? pinExpiresAt = null;

            if (hasEmail)
            {
                await InvalidateUnusedCodesAsync(account.Id, ActivationPurpose, now, cancellationToken);
                string rawCode = _verificationCodes.GenerateCode();
                string hmac = _verificationCodes.ComputeHmac(rawCode, account.Id.ToString(), ActivationPurpose);
                _dbContext.VerificationCodes.Add(new VerificationCode(
                    GuidV7.NewGuid(),
                    account.Id,
                    PortalRealm,
                    ActivationPurpose,
                    hmac,
                    now.AddHours(72),
                    now,
                    maxAttempts: 5));

                await _dbContext.SaveChangesAsync(cancellationToken);

                var emailPayload = new
                {
                    to = emailToUse,
                    subject = $"{clinic.Name} - دعوة بوابة المريض / Patient Portal Activation Code",
                    body = $"رمز تفعيل حسابك في بوابة {clinic.Name} هو: {rawCode} (ينتهي خلال 72 ساعة).\nYour patient portal activation code for {clinic.Name} is: {rawCode} (expires in 72 hours)."
                };
                await _outbox.EnqueueAsync("SendEmail", JsonSerializer.Serialize(emailPayload), cancellationToken);
            }
            else
            {
                generatedPin = PatientAccount.GeneratePrintedActivationPin();
                string pinHmac = _verificationCodes.ComputeHmac(generatedPin, account.Id.ToString(), PrintedPinPurpose);
                pinExpiresAt = now.AddHours(72);
                account.IssuePrintedActivationCode(pinHmac, pinExpiresAt.Value, now);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            await _auditService.LogAsync(
                "PatientPortalInvited",
                "PatientAccount",
                account.Id.ToString(),
                null,
                JsonSerializer.Serialize(new { account.Id, account.PatientId, Mode = hasEmail ? "Email" : "PrintedPin" }),
                staffUserId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            return Result<PatientPortalAdminStatusResult>.Success(new PatientPortalAdminStatusResult(
                PatientId: patientId,
                HasAccount: true,
                Status: account.Status,
                Email: account.Email,
                IsEmailConfirmed: account.IsEmailConfirmed,
                IsGuardianAccount: account.IsGuardianAccount,
                GuardianName: account.GuardianName,
                ConsentAcceptedAtUtc: consent.AcceptedAtUtc,
                ConsentTermsVersion: consent.TermsVersion,
                LastLoginAtUtc: account.LastLoginAtUtc,
                GeneratedPrintedPin: generatedPin,
                PrintedPinExpiresAtUtc: pinExpiresAt));
        });
    }

    public async Task<Result<PatientPortalAdminStatusResult>> ResendInvitationAsync(
        Guid staffUserId,
        Guid clinicId,
        Guid patientId,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(staffUserId, clinicId, "portal.invite", "portal", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<PatientPortalAdminStatusResult>.Failure(gate.Error);
        }

        DateTimeOffset now = _clock.UtcNow;
        Clinic clinic = await _dbContext.Clinics.AsNoTracking().FirstAsync(c => c.Id == clinicId, cancellationToken);

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            PatientAccount? account = await _dbContext.PatientAccounts
                .FirstOrDefaultAsync(a => a.ClinicId == clinicId && !a.IsVoided && a.PatientId == patientId, cancellationToken);
            if (account == null)
            {
                return Result<PatientPortalAdminStatusResult>.Failure(Error.NotFound(
                    "PORTAL_ACCOUNT_NOT_FOUND",
                    "Patient portal invitation was not found."));
            }

            if (account.Status == PatientAccountStatus.Active)
            {
                return Result<PatientPortalAdminStatusResult>.Failure(Error.Conflict(
                    "PORTAL_ACCOUNT_ALREADY_ACTIVE",
                    "This patient portal account is already active."));
            }

            PortalConsent? consent = await _dbContext.PortalConsents
                .AsNoTracking()
                .Where(c => c.ClinicId == clinicId && !c.IsVoided && c.PatientId == patientId)
                .OrderByDescending(c => c.AcceptedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            string? generatedPin = null;
            DateTimeOffset? pinExpiresAt = null;

            if (!string.IsNullOrWhiteSpace(account.Email))
            {
                VerificationCode? recent = await _dbContext.VerificationCodes
                    .Where(c => c.UserId == account.Id && c.Realm == PortalRealm && c.Purpose == ActivationPurpose)
                    .OrderByDescending(c => c.CreatedAtUtc)
                    .FirstOrDefaultAsync(cancellationToken);

                if (recent != null && (now - recent.CreatedAtUtc).TotalSeconds < 60)
                {
                    return Result<PatientPortalAdminStatusResult>.Failure(Error.Conflict(
                        "RESEND_COOLDOWN",
                        "Please wait at least 60 seconds before resending a verification code."));
                }

                int hourCount = await _dbContext.VerificationCodes
                    .CountAsync(
                        c => c.UserId == account.Id
                             && c.Realm == PortalRealm
                             && c.Purpose == ActivationPurpose
                             && c.CreatedAtUtc >= now.AddHours(-1),
                        cancellationToken);

                if (hourCount >= MaxHourlyCodes)
                {
                    return Result<PatientPortalAdminStatusResult>.Failure(Error.Conflict(
                        "HOURLY_CODE_CAP_REACHED",
                        "Hourly verification code resend limit reached."));
                }

                await InvalidateUnusedCodesAsync(account.Id, ActivationPurpose, now, cancellationToken);
                string rawCode = _verificationCodes.GenerateCode();
                string hmac = _verificationCodes.ComputeHmac(rawCode, account.Id.ToString(), ActivationPurpose);
                _dbContext.VerificationCodes.Add(new VerificationCode(
                    GuidV7.NewGuid(),
                    account.Id,
                    PortalRealm,
                    ActivationPurpose,
                    hmac,
                    now.AddHours(72),
                    now,
                    maxAttempts: 5));

                await _dbContext.SaveChangesAsync(cancellationToken);

                var emailPayload = new
                {
                    to = account.Email,
                    subject = $"{clinic.Name} - إعادة إرسال رمز بوابة المريض / Portal Activation Code Resent",
                    body = $"رمز تفعيل حسابك في بوابة {clinic.Name} هو: {rawCode} (ينتهي خلال 72 ساعة).\nYour patient portal activation code for {clinic.Name} is: {rawCode} (expires in 72 hours)."
                };
                await _outbox.EnqueueAsync("SendEmail", JsonSerializer.Serialize(emailPayload), cancellationToken);
            }
            else
            {
                generatedPin = PatientAccount.GeneratePrintedActivationPin();
                string pinHmac = _verificationCodes.ComputeHmac(generatedPin, account.Id.ToString(), PrintedPinPurpose);
                pinExpiresAt = now.AddHours(72);
                account.IssuePrintedActivationCode(pinHmac, pinExpiresAt.Value, now);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            await _auditService.LogAsync(
                "PatientPortalInvitationResent",
                "PatientAccount",
                account.Id.ToString(),
                null,
                null,
                staffUserId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            return Result<PatientPortalAdminStatusResult>.Success(new PatientPortalAdminStatusResult(
                PatientId: patientId,
                HasAccount: true,
                Status: account.Status,
                Email: account.Email,
                IsEmailConfirmed: account.IsEmailConfirmed,
                IsGuardianAccount: account.IsGuardianAccount,
                GuardianName: account.GuardianName,
                ConsentAcceptedAtUtc: consent?.AcceptedAtUtc,
                ConsentTermsVersion: consent?.TermsVersion,
                LastLoginAtUtc: account.LastLoginAtUtc,
                GeneratedPrintedPin: generatedPin,
                PrintedPinExpiresAtUtc: pinExpiresAt));
        });
    }

    public async Task<Result<PatientPortalAdminStatusResult>> SetAccountEnabledAsync(
        Guid staffUserId,
        Guid clinicId,
        Guid patientId,
        bool enabled,
        string? reason,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(staffUserId, clinicId, "portal.manage", "portal", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<PatientPortalAdminStatusResult>.Failure(gate.Error);
        }

        DateTimeOffset now = _clock.UtcNow;
        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            PatientAccount? account = await _dbContext.PatientAccounts
                .FirstOrDefaultAsync(a => a.ClinicId == clinicId && !a.IsVoided && a.PatientId == patientId, cancellationToken);
            if (account == null)
            {
                return Result<PatientPortalAdminStatusResult>.Failure(Error.NotFound(
                    "PORTAL_ACCOUNT_NOT_FOUND",
                    "Patient portal account was not found."));
            }

            PatientAccountStatus prevStatus = account.Status;
            if (enabled)
            {
                account.Enable(now);
            }
            else
            {
                account.Disable(now);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                enabled ? "PatientPortalEnabled" : "PatientPortalDisabled",
                "PatientAccount",
                account.Id.ToString(),
                JsonSerializer.Serialize(new { Status = prevStatus.ToString() }),
                JsonSerializer.Serialize(new { Status = account.Status.ToString(), Reason = reason }),
                staffUserId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            PortalConsent? consent = await _dbContext.PortalConsents
                .AsNoTracking()
                .Where(c => c.ClinicId == clinicId && !c.IsVoided && c.PatientId == patientId)
                .OrderByDescending(c => c.AcceptedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            return Result<PatientPortalAdminStatusResult>.Success(new PatientPortalAdminStatusResult(
                PatientId: patientId,
                HasAccount: true,
                Status: account.Status,
                Email: account.Email,
                IsEmailConfirmed: account.IsEmailConfirmed,
                IsGuardianAccount: account.IsGuardianAccount,
                GuardianName: account.GuardianName,
                ConsentAcceptedAtUtc: consent?.AcceptedAtUtc,
                ConsentTermsVersion: consent?.TermsVersion,
                LastLoginAtUtc: account.LastLoginAtUtc,
                GeneratedPrintedPin: null,
                PrintedPinExpiresAtUtc: account.PrintedCodeExpiresAtUtc));
        });
    }

    public async Task<Result<ClinicPortalSettingResult>> GetClinicPortalSettingsAsync(
        Guid staffUserId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(staffUserId, clinicId, "portal.manage", "portal", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<ClinicPortalSettingResult>.Failure(gate.Error);
        }

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            ClinicPortalSetting settings = await GetOrBuildDefaultSettingsAsync(clinicId, cancellationToken);
            return Result<ClinicPortalSettingResult>.Success(MapSettings(settings));
        });
    }

    public async Task<Result<ClinicPortalSettingResult>> UpdateClinicPortalSettingsAsync(
        Guid staffUserId,
        Guid clinicId,
        UpdateClinicPortalSettingInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var gate = await _authorizationService.EnsureClinicAccessAsync(staffUserId, clinicId, "clinic.settings.edit", "portal", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<ClinicPortalSettingResult>.Failure(gate.Error);
        }

        DateTimeOffset now = _clock.UtcNow;
        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            ClinicPortalSetting? settings = await _dbContext.ClinicPortalSettings
                .FirstOrDefaultAsync(s => s.ClinicId == clinicId && !s.IsVoided, cancellationToken);

            if (settings == null)
            {
                settings = new ClinicPortalSetting(GuidV7.NewGuid(), clinicId, now);
                _dbContext.ClinicPortalSettings.Add(settings);
            }

            settings.Update(
                input.ShowPricesByDefault,
                input.ShowDoctorNames,
                input.DefaultVisitVisibility,
                input.TermsTextAr,
                input.TermsTextEn,
                input.WelcomeMessageAr,
                input.WelcomeMessageEn,
                now);

            Clinic? clinic = await _dbContext.Clinics.FirstOrDefaultAsync(c => c.Id == clinicId, cancellationToken);
            clinic?.SetPortalShowPlanCostsByDefault(input.ShowPricesByDefault);

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                "ClinicPortalSettingsUpdated",
                "ClinicPortalSetting",
                settings.Id.ToString(),
                null,
                JsonSerializer.Serialize(new
                {
                    settings.ShowPricesByDefault,
                    settings.ShowDoctorNames,
                    settings.DefaultVisitVisibility
                }),
                staffUserId,
                clinicId,
                ipAddress,
                requestId,
                cancellationToken);

            return Result<ClinicPortalSettingResult>.Success(MapSettings(settings));
        });
    }

    public async Task<Result<IReadOnlyList<PortalAccessLogItemResult>>> ListPatientPortalAccessLogsAsync(
        Guid staffUserId,
        Guid clinicId,
        Guid? patientId,
        int page,
        int pageSize,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        var gate = await _authorizationService.EnsureClinicAccessAsync(staffUserId, clinicId, "portal.manage", "portal", deviceId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<PortalAccessLogItemResult>>.Failure(gate.Error);
        }

        int safePage = Math.Max(1, page);
        int safePageSize = Math.Clamp(pageSize <= 0 ? 25 : pageSize, 1, 100);
        string? patientIdStr = patientId?.ToString();

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            IQueryable<PatientAccessLog> query = _dbContext.PatientAccessLogs
                .AsNoTracking()
                .Where(l => l.ClinicId == clinicId && l.ActorType == "Patient");

            if (!string.IsNullOrEmpty(patientIdStr))
            {
                query = query.Where(l => l.ActorId == patientIdStr);
            }

            List<PortalAccessLogItemResult> items = await query
                .OrderByDescending(l => l.TimestampUtc)
                .Skip((safePage - 1) * safePageSize)
                .Take(safePageSize)
                .Select(l => new PortalAccessLogItemResult(l.Id, l.TimestampUtc, l.ActorId, l.ActorType, l.Reason, l.Details))
                .ToListAsync(cancellationToken);

            return Result<IReadOnlyList<PortalAccessLogItemResult>>.Success(items);
        });
    }

    public async Task<Result<ClinicPortalInspectResult>> InspectClinicPortalAsync(
        string adminId,
        Guid clinicId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adminId);

        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            List<PatientAccount> accounts = await _dbContext.PatientAccounts
                .AsNoTracking()
                .Where(a => a.ClinicId == clinicId && !a.IsVoided)
                .ToListAsync(cancellationToken);

            DateTimeOffset cutoff = _clock.UtcNow.AddDays(-30);
            int viewCount = await _dbContext.PatientAccessLogs
                .AsNoTracking()
                .CountAsync(
                    l => l.ClinicId == clinicId
                         && l.ActorType == "Patient"
                         && l.TimestampUtc >= cutoff
                         && l.Reason != "PortalLogin"
                         && l.Reason != "PortalLoginFailed",
                    cancellationToken);

            ClinicPortalSetting settings = await GetOrBuildDefaultSettingsAsync(clinicId, cancellationToken);

            _dbContext.PatientAccessLogs.Add(new PatientAccessLog(
                GuidV7.NewGuid(),
                adminId,
                "InspectPortal",
                _clock.UtcNow,
                clinicId,
                null,
                "SuperAdmin"));
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result<ClinicPortalInspectResult>.Success(new ClinicPortalInspectResult(
                ClinicId: clinicId,
                TotalAccountsCount: accounts.Count,
                ActiveAccountsCount: accounts.Count(a => a.Status == PatientAccountStatus.Active),
                InvitedAccountsCount: accounts.Count(a => a.Status == PatientAccountStatus.Invited || a.Status == PatientAccountStatus.PendingEmailConfirmation),
                DisabledAccountsCount: accounts.Count(a => a.Status == PatientAccountStatus.Disabled),
                PatientViewsLast30DaysCount: viewCount,
                Settings: MapSettings(settings)));
        });
    }

    private async Task<Result<PortalSessionContext>> EnsureActivePortalSessionForSlugAsync(
        Guid accountId,
        Guid clinicId,
        Guid patientId,
        string slug,
        CancellationToken cancellationToken)
    {
        var resolved = await ResolvePortalClinicAsync(slug, treatModuleDisabledAsNotFound: false, cancellationToken);
        if (resolved.IsFailure)
        {
            return Result<PortalSessionContext>.Failure(resolved.Error);
        }

        if (resolved.Value.Clinic.Id != clinicId)
        {
            return Result<PortalSessionContext>.Failure(Error.NotFound("CLINIC_NOT_FOUND", "Clinic was not found."));
        }

        PatientAccount? account = await ExecuteForClinicAsync(clinicId, async () =>
        {
            return await _dbContext.PatientAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    a => a.ClinicId == clinicId
                         && a.Id == accountId
                         && a.PatientId == patientId
                         && !a.IsVoided
                         && a.Status == PatientAccountStatus.Active,
                    cancellationToken);
        });

        if (account == null)
        {
            return Result<PortalSessionContext>.Failure(Error.Unauthorized(
                "PORTAL_SESSION_INVALID",
                "Patient portal session is no longer active."));
        }

        return Result<PortalSessionContext>.Success(new PortalSessionContext(resolved.Value, account));
    }

    private async Task<Result<PortalClinicResolution>> ResolvePortalClinicAsync(
        string slug,
        bool treatModuleDisabledAsNotFound,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return Result<PortalClinicResolution>.Failure(Error.NotFound("CLINIC_NOT_FOUND", "Clinic portal was not found."));
        }

        string requestedSlug = slug.Trim().ToLowerInvariant();
        bool isRedirect = false;

        Clinic? clinic = await _dbContext.Clinics
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Slug == requestedSlug, cancellationToken);

        if (clinic == null)
        {
            ClinicSlugHistory? history = await _dbContext.ClinicSlugs
                .AsNoTracking()
                .Where(s => s.Slug == requestedSlug)
                .OrderByDescending(s => s.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (history != null)
            {
                clinic = await _dbContext.Clinics
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == history.ClinicId, cancellationToken);
                isRedirect = clinic != null;
            }
        }

        if (clinic == null ||
            !clinic.IsActive ||
            string.Equals(clinic.Status, "Suspended", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(clinic.Status, "PendingOnboarding", StringComparison.OrdinalIgnoreCase))
        {
            return Result<PortalClinicResolution>.Failure(Error.NotFound("CLINIC_NOT_FOUND", "Clinic portal was not found."));
        }

        List<string> enabledList = await ExecuteForClinicAsync(clinic.Id, async () =>
        {
            return await _dbContext.ClinicModules
                .AsNoTracking()
                .Where(m => m.ClinicId == clinic.Id && m.Enabled)
                .Select(m => m.ModuleKey)
                .ToListAsync(cancellationToken);
        });

        HashSet<string> enabled = enabledList.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!enabled.Contains("portal"))
        {
            return Result<PortalClinicResolution>.Failure(treatModuleDisabledAsNotFound
                ? Error.NotFound("CLINIC_NOT_FOUND", "Clinic portal was not found.")
                : Error.Forbidden("MODULE_DISABLED", "Patient portal module is disabled for this clinic."));
        }

        return Result<PortalClinicResolution>.Success(new PortalClinicResolution(clinic, requestedSlug, isRedirect, enabled));
    }

    private async Task<ClinicPortalSetting> GetOrBuildDefaultSettingsAsync(Guid clinicId, CancellationToken cancellationToken)
    {
        return await ExecuteForClinicAsync(clinicId, async () =>
        {
            ClinicPortalSetting? existing = await _dbContext.ClinicPortalSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.ClinicId == clinicId && !s.IsVoided, cancellationToken);

            return existing ?? new ClinicPortalSetting(GuidV7.NewGuid(), clinicId, _clock.UtcNow);
        });
    }

    private async Task InvalidateUnusedCodesAsync(Guid accountId, string purpose, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        List<VerificationCode> oldCodes = await _dbContext.VerificationCodes
            .Where(c => c.UserId == accountId && c.Realm == PortalRealm && c.Purpose == purpose && c.UsedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (VerificationCode oldCode in oldCodes)
        {
            oldCode.MarkUsed(nowUtc);
        }
    }

    private async Task WritePatientAccessLogAsync(
        Guid clinicId,
        Guid patientId,
        string reason,
        string? details,
        CancellationToken cancellationToken)
    {
        await ExecuteForClinicAsync(clinicId, async () =>
        {
            _dbContext.PatientAccessLogs.Add(new PatientAccessLog(
                GuidV7.NewGuid(),
                patientId.ToString(),
                reason,
                _clock.UtcNow,
                clinicId,
                details,
                "Patient"));
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        });
    }

    private PatientPortalSessionResult CreatePortalSessionResult(
        Clinic clinic,
        PatientAccount account,
        string fullName,
        DateTimeOffset nowUtc)
    {
        DateTimeOffset expiresAtUtc = nowUtc.AddMinutes(PortalTokenMinutes);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new("realm", PortalRealm),
            new("clinic_id", account.ClinicId.ToString()),
            new("patient_id", account.PatientId.ToString()),
            new("securityStamp", account.SecurityStamp),
            new(ClaimTypes.Email, account.Email ?? string.Empty)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            NotBefore = nowUtc.UtcDateTime,
            IssuedAt = nowUtc.UtcDateTime,
            Expires = expiresAtUtc.UtcDateTime,
            Issuer = _issuer,
            Audience = _audience,
            SigningCredentials = credentials
        };

        var handler = new JwtSecurityTokenHandler();
        string accessToken = handler.WriteToken(handler.CreateToken(descriptor));

        return new PatientPortalSessionResult(
            AccessToken: accessToken,
            ExpiresAtUtc: expiresAtUtc,
            PatientAccountId: account.Id,
            ClinicId: clinic.Id,
            PatientId: account.PatientId,
            FullName: fullName,
            ClinicSlug: clinic.Slug,
            Email: account.Email);
    }

    private static List<string> BuildAvailableSections(HashSet<string> enabledModules)
    {
        var sections = new List<string> { "summary" };
        if (enabledModules.Contains("patient-history"))
        {
            sections.Add("medical-record");
        }
        if (enabledModules.Contains("attachments"))
        {
            sections.Add("attachments");
        }
        if (enabledModules.Contains("scheduling"))
        {
            sections.Add("appointments");
        }
        if (enabledModules.Contains("clinical"))
        {
            sections.Add("visits");
        }
        if (enabledModules.Contains("treatment-plans"))
        {
            sections.Add("treatment-plans");
        }

        return sections;
    }

    private static IReadOnlyList<PortalVisitSummaryResult> ApplyDoctorNameMask(
        IReadOnlyList<PortalVisitSummaryResult> visits,
        bool showDoctorNames)
    {
        if (showDoctorNames)
        {
            return visits;
        }

        return visits.Select(v => v with { DoctorName = string.Empty }).ToList();
    }

    private static IReadOnlyList<PortalTreatmentPlanItemResult> ApplyBillingModuleMask(
        IReadOnlyList<PortalTreatmentPlanItemResult> plans,
        bool billingEnabled)
    {
        if (billingEnabled)
        {
            return plans;
        }

        return plans.Select(p => p with
        {
            ShowCostToPatient = false,
            TotalCostToPatient = null,
            PaidAmountToPatient = null,
            RemainingBalanceToPatient = null,
            Currency = null,
            Sessions = p.Sessions.Select(s => s with { SessionPriceAmount = null }).ToList()
        }).ToList();
    }

    private static ClinicPortalSettingResult MapSettings(ClinicPortalSetting s)
        => new(
            s.ClinicId,
            s.ShowPricesByDefault,
            s.ShowDoctorNames,
            s.DefaultVisitVisibility,
            s.TermsTextAr,
            s.TermsTextEn,
            s.WelcomeMessageAr,
            s.WelcomeMessageEn,
            s.UpdatedAtUtc);

    private static string? FormatClinicAddress(Clinic clinic)
    {
        string[] parts = new[] { clinic.Street, clinic.City, clinic.Governorate }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .ToArray();

        return parts.Length == 0 ? null : string.Join(", ", parts);
    }

    private async Task<T> ExecuteForClinicAsync<T>(Guid clinicId, Func<Task<T>> action)
    {
        Guid? prev = _currentClinicContext.ClinicId;
        bool wasAdmin = _currentClinicContext.IsPlatformAdmin;
        try
        {
            _currentClinicContext.SetTenant(clinicId);
            return await action();
        }
        finally
        {
            if (wasAdmin)
            {
                _currentClinicContext.SetPlatformAdmin();
            }
            else if (prev.HasValue)
            {
                _currentClinicContext.SetTenant(prev.Value);
            }
            else
            {
                _currentClinicContext.Clear();
            }
        }
    }

    private sealed record PortalClinicResolution(
        Clinic Clinic,
        string RequestedSlug,
        bool IsRedirect,
        HashSet<string> EnabledModules);

    private sealed record PortalSessionContext(
        PortalClinicResolution ClinicResolution,
        PatientAccount Account);
}
