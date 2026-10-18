namespace MyApp.Application.Dtos;

public sealed record UserProfileDto(
    int UserId,
    decimal? WeightKg,
    decimal? HeightCm,
    DateOnly? BirthDate);