namespace Core.Domain;

/// <summary>
/// Pure offline-sync domain policies for online-only boundary checks, entity conflict classification,
/// clock-skew evaluation, backoff calculation, and offline auth window enforcement.
/// </summary>
public static class SyncCalculator
{
    private static readonly HashSet<string> OnlineOnlyActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "CloseDay",
        "ReopenDay",
        "Refund",
        "CreateRefund",
        "PriceListUpsert",
        "RolesAndClaims",
        "AddClinic",
        "Onboarding",
        "ModuleChange",
        "AdminAction",
        "PasswordOrEmailChange",
        "PortalEndpoint",
        "CrossClinicReport",
        "FinalizePayroll"
    };

    public static bool IsOnlineOnlyAction(string? actionType)
    {
        if (string.IsNullOrWhiteSpace(actionType))
        {
            return false;
        }

        return OnlineOnlyActions.Contains(actionType.Trim());
    }

    public static ConflictClass ClassifyConflictClass(string? entityType)
    {
        string normalized = entityType?.Trim() ?? string.Empty;
        return normalized.ToUpperInvariant() switch
        {
            "CLINICSETTINGS" or "CLINICSYNCSETTINGS" or "TIMEENGINESETTINGS" or "WORKINGHOURS" => ConflictClass.LastWriterWins,
            "APPOINTMENT" or "QUEUEENTRY" or "PATIENT" or "INVENTORYSTOCK" or "STOCKDEDUCTION" => ConflictClass.ServerValidated,
            _ => ConflictClass.AppendOnly
        };
    }

    public static ClockSkewEvaluation EvaluateClockSkew(
        DateTimeOffset clientTimestampUtc,
        DateTimeOffset serverReceivedAtUtc,
        int thresholdSeconds = 300)
    {
        long signedSkew = (long)Math.Round((serverReceivedAtUtc - clientTimestampUtc).TotalSeconds);
        long absSkew = Math.Abs(signedSkew);
        return new ClockSkewEvaluation(signedSkew, absSkew, absSkew > Math.Max(0, thresholdSeconds));
    }

    public static int ComputeBackoffDelayMs(int attempt, int baseMs = 500, int maxMs = 30000)
    {
        int clampedAttempt = Math.Clamp(attempt, 0, 20);
        long delay = (long)(baseMs * Math.Pow(2, clampedAttempt));
        return (int)Math.Min(maxMs, Math.Max(baseMs, delay));
    }

    public static bool IsOfflineSessionValid(DateTimeOffset lastOnlineAuthAtUtc, DateTimeOffset nowUtc, int maxHours)
    {
        if (maxHours <= 0)
        {
            return false;
        }

        return nowUtc >= lastOnlineAuthAtUtc && (nowUtc - lastOnlineAuthAtUtc).TotalHours <= maxHours;
    }
}

