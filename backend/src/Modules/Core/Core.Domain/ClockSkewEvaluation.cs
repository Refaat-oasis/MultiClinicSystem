namespace Core.Domain;

public sealed record ClockSkewEvaluation(long SignedSkewSeconds, long AbsoluteSkewSeconds, bool IsLargeSkew);

