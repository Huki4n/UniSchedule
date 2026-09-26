namespace UniSchedule.Services;

public enum SubjectRollbackDecision
{
    Keep,
    Remember,
    Forget
}

public static class SubjectRollbackPolicy
{
    public static SubjectRollbackDecision Decide(string previous, string? stored, string next)
    {
        if (string.Equals(next.Trim(), (stored ?? previous).Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return stored is not null ? SubjectRollbackDecision.Forget : SubjectRollbackDecision.Keep;
        }

        return SubjectRollbackDecision.Remember;
    }

    public static string OriginalName(string previous, string? stored) => stored ?? previous;
}
