using MyApp.Application.Dtos;

namespace MyApp.Application.Services;

public static class XpCalculator
{
    private const int XpPerLevel = 1000;
    private const int DefaultBodyweightWeight = 10;
    private const decimal SuspiciousWeightThreshold = 400m;
    private const int SuspiciousRepsThreshold = 100;
    private const int SuspiciousSetsCountThreshold = 30;

    public static int CalculateXp(IReadOnlyList<WorkoutSetDto>? sets)
    {
        if (sets is null || sets.Count == 0)
        {
            return 0;
        }

        int totalXp = 0;
        foreach (var set in sets)
        {
            if (set.Reps <= 0)
            {
                continue;
            }

            decimal effectiveWeight = set.Weight > 0 ? set.Weight : DefaultBodyweightWeight;
            totalXp += (int)Math.Round(set.Reps * effectiveWeight);
        }

        return totalXp;
    }

    public static int CalculateLevel(int totalXp)
    {
        if (totalXp <= 0)
        {
            return 1;
        }

        return 1 + (totalXp / XpPerLevel);
    }

    public static bool IsSuspicious(IReadOnlyList<WorkoutSetDto>? sets)
    {
        if (sets is null || sets.Count == 0)
        {
            return false;
        }

        if (sets.Count > SuspiciousSetsCountThreshold)
        {
            return true;
        }

        foreach (var set in sets)
        {
            if (set.Weight > SuspiciousWeightThreshold || set.Reps > SuspiciousRepsThreshold)
            {
                return true;
            }
        }

        return false;
    }
}
