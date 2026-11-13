using System.Text.Json;

namespace Core.Domain;

/// <summary>
/// Pure helper translating between FDI and Universal dental numbering systems (permanent & primary)
/// and deriving each tooth's latest clinical state from visit records.
/// </summary>
public static class DentalNotationConverter
{
    private static readonly Dictionary<string, (string Universal, bool IsPrimary)> FdiMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Permanent Q1 Upper Right
        ["18"] = ("1", false), ["17"] = ("2", false), ["16"] = ("3", false), ["15"] = ("4", false),
        ["14"] = ("5", false), ["13"] = ("6", false), ["12"] = ("7", false), ["11"] = ("8", false),
        // Permanent Q2 Upper Left
        ["21"] = ("9", false), ["22"] = ("10", false), ["23"] = ("11", false), ["24"] = ("12", false),
        ["25"] = ("13", false), ["26"] = ("14", false), ["27"] = ("15", false), ["28"] = ("16", false),
        // Permanent Q3 Lower Left
        ["38"] = ("17", false), ["37"] = ("18", false), ["36"] = ("19", false), ["35"] = ("20", false),
        ["34"] = ("21", false), ["33"] = ("22", false), ["32"] = ("23", false), ["31"] = ("24", false),
        // Permanent Q4 Lower Right
        ["41"] = ("25", false), ["42"] = ("26", false), ["43"] = ("27", false), ["44"] = ("28", false),
        ["45"] = ("29", false), ["46"] = ("30", false), ["47"] = ("31", false), ["48"] = ("32", false),
        // Primary Q5 Upper Right
        ["55"] = ("A", true), ["54"] = ("B", true), ["53"] = ("C", true), ["52"] = ("D", true), ["51"] = ("E", true),
        // Primary Q6 Upper Left
        ["61"] = ("F", true), ["62"] = ("G", true), ["63"] = ("H", true), ["64"] = ("I", true), ["65"] = ("J", true),
        // Primary Q7 Lower Left
        ["75"] = ("K", true), ["74"] = ("L", true), ["73"] = ("M", true), ["72"] = ("N", true), ["71"] = ("O", true),
        // Primary Q8 Lower Right
        ["81"] = ("P", true), ["82"] = ("Q", true), ["83"] = ("R", true), ["84"] = ("S", true), ["85"] = ("T", true)
    };

    private static readonly Dictionary<string, (string Fdi, bool IsPrimary)> UniversalMap = BuildUniversalReverseMap();

    public static bool TryResolveTooth(
        string code,
        DentalNumberingSystem? systemHint,
        out string fdiNumber,
        out string universalCode,
        out bool isPrimary)
    {
        fdiNumber = string.Empty;
        universalCode = string.Empty;
        isPrimary = false;

        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        string trimmed = code.Trim().ToUpperInvariant();

        if (systemHint != DentalNumberingSystem.Universal && FdiMap.TryGetValue(trimmed, out var fromFdi))
        {
            fdiNumber = trimmed;
            universalCode = fromFdi.Universal;
            isPrimary = fromFdi.IsPrimary;
            return true;
        }

        if (systemHint != DentalNumberingSystem.Fdi && UniversalMap.TryGetValue(trimmed, out var fromUniv))
        {
            fdiNumber = fromUniv.Fdi;
            universalCode = trimmed;
            isPrimary = fromUniv.IsPrimary;
            return true;
        }

        return false;
    }

    public static IReadOnlyList<DerivedToothState> DeriveCurrentChartState(IEnumerable<ToothRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        var latestByTooth = new Dictionary<string, ToothRecord>(StringComparer.OrdinalIgnoreCase);

        foreach (ToothRecord record in records
                     .Where(r => !r.IsVoided)
                     .OrderBy(r => r.RecordedAtUtc)
                     .ThenBy(r => r.Id))
        {
            latestByTooth[record.FdiToothNumber] = record;
        }

        var output = new List<DerivedToothState>(latestByTooth.Count);
        foreach (ToothRecord latest in latestByTooth.Values.OrderBy(r => r.FdiToothNumber, StringComparer.Ordinal))
        {
            IReadOnlyList<string> surfaces = DeserializeSurfaces(latest.SurfacesJson);
            output.Add(new DerivedToothState(
                latest.FdiToothNumber,
                latest.UniversalToothCode,
                latest.IsPrimaryTooth,
                latest.Condition,
                surfaces,
                latest.Notes,
                latest.VisitId,
                latest.RecordedAtUtc));
        }

        return output;
    }

    private static IReadOnlyList<string> DeserializeSurfaces(string? surfacesJson)
    {
        if (string.IsNullOrWhiteSpace(surfacesJson))
        {
            return Array.Empty<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(surfacesJson) ?? [];
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    private static Dictionary<string, (string Fdi, bool IsPrimary)> BuildUniversalReverseMap()
    {
        var reverse = new Dictionary<string, (string Fdi, bool IsPrimary)>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in FdiMap)
        {
            reverse[kvp.Value.Universal] = (kvp.Key, kvp.Value.IsPrimary);
        }

        return reverse;
    }
}

