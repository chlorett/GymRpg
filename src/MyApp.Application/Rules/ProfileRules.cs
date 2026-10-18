namespace MyApp.Application.Rules;

public static class ProfileRules
{
    public const decimal MinWeightKg = 20;

    public const decimal MaxWeightKg = 500;

    public const decimal MinHeightCm = 50;

    public const decimal MaxHeightCm = 272;

    public const int MinAge = 10;

    public const int MaxAge = 120;

    public static int CalculateAge(DateOnly birth, DateOnly today)
    {
        var age = today.Year - birth.Year;

        if (birth > today.AddYears(-age))
        {
            age--;
        }

        return age;
    }
}