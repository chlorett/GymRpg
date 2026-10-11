using MyApp.Application.Interfaces;

namespace MyApp.Infrastructure.Security;

public sealed class BcryptPasswordHasher : IPasswordHasher
{
    // Кожна одиниця подвоює час хешування; 12 — розумний баланс для десктопного застосунку.
    private const int WorkFactor = 12;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    public bool Verify(string password, string hash)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        ArgumentException.ThrowIfNullOrEmpty(hash);

        return BCrypt.Net.BCrypt.Verify(password, hash);
    }
}
