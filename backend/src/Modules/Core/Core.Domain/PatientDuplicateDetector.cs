namespace Core.Domain;

/// <summary>
/// Detects potential duplicate patients inside a clinic using normalized phone numbers and normalized Arabic/Latin names.
/// </summary>
public static class PatientDuplicateDetector
{
    public static IReadOnlyList<PatientDuplicateMatch> FindMatches(
        string fullName,
        string phone,
        IEnumerable<PatientDuplicateCandidate> existingCandidates,
        Guid? excludePatientId = null)
    {
        ArgumentNullException.ThrowIfNull(existingCandidates);

        string normName = ArabicNormalizer.NormalizeText(fullName);
        string normPhone = ArabicNormalizer.NormalizePhone(phone);
        var matches = new List<PatientDuplicateMatch>();

        foreach (var candidate in existingCandidates)
        {
            if (excludePatientId.HasValue && candidate.Id == excludePatientId.Value)
            {
                continue;
            }

            bool phoneMatch = !string.IsNullOrEmpty(normPhone) &&
                              string.Equals(candidate.NormalizedPhone, normPhone, StringComparison.Ordinal);
            bool nameMatch = !string.IsNullOrEmpty(normName) &&
                             string.Equals(candidate.NormalizedName, normName, StringComparison.Ordinal);

            if (phoneMatch && nameMatch)
            {
                matches.Add(new PatientDuplicateMatch(
                    candidate.Id,
                    candidate.FileNumber,
                    candidate.FullName,
                    candidate.Phone,
                    "PHONE_AND_NAME_EXACT",
                    100));
            }
            else if (phoneMatch)
            {
                matches.Add(new PatientDuplicateMatch(
                    candidate.Id,
                    candidate.FileNumber,
                    candidate.FullName,
                    candidate.Phone,
                    "PHONE_MATCH",
                    85));
            }
            else if (nameMatch)
            {
                matches.Add(new PatientDuplicateMatch(
                    candidate.Id,
                    candidate.FileNumber,
                    candidate.FullName,
                    candidate.Phone,
                    "NORMALIZED_NAME_MATCH",
                    75));
            }
        }

        return matches
            .OrderByDescending(m => m.ConfidencePercent)
            .ThenBy(m => m.FileNumber, StringComparer.Ordinal)
            .ToList();
    }
}

