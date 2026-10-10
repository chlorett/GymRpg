using MyApp.Domain.Enums;

namespace MyApp.Domain.Entities;

public class User
{
    public int Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.User;

    public decimal WeightKg { get; set; }

    public decimal HeightCm { get; set; }

    public DateOnly BirthDate { get; set; }

    public bool IsBlocked { get; set; }
}
