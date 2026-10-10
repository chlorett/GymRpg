namespace MyApp.Application.Rules;

public static class WorkoutRules
{
    public const int MaxSets = 50;

    public const int MinReps = 1;

    public const int MaxReps = 100;

    public const decimal MinWeight = 0m;

    public const decimal MaxWeight = 1000m;

    public const decimal SuspiciousWeight = 500m;

    public const int XpPerLevel = 1000;

    public const decimal XpWeightBonus = 10m;

    public const decimal XpDivisor = 10m;
}
