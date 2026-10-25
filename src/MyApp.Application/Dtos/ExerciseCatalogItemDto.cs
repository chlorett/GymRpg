using MyApp.Domain.Enums;

namespace MyApp.Application.Dtos;

public sealed record ExerciseCatalogItemDto(
    int Id,
    string Name,
    EquipmentType EquipmentType,
    string EquipmentLabel,
    string Description,
    string Technique);