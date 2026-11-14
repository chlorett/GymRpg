using MyApp.Domain.Enums;

namespace MyApp.Application.Dtos;

public sealed record SaveExerciseRequest(
    int? Id,
    string Name,
    EquipmentType EquipmentType,
    string? Description,
    string? Technique);