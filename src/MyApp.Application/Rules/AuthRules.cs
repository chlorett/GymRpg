namespace MyApp.Application.Rules;

public static class AuthRules
{
    public const int MinUsernameLength = 3;

    // Збігається з users.username varchar(30).
    public const int MaxUsernameLength = 30;

    // Збігається з users.email varchar(254).
    public const int MaxEmailLength = 254;

    public const int MinPasswordLength = 8;

    // bcrypt обробляє лише перші 72 байти, решту мовчки відкидає.
    public const int MaxPasswordBytes = 72;

    public const decimal MinWeightKg = 20m;

    public const decimal MaxWeightKg = 500m;

    public const decimal MinHeightCm = 50m;

    public const decimal MaxHeightCm = 260m;

    public const int MinAge = 13;

    public const int MaxAge = 120;
}
