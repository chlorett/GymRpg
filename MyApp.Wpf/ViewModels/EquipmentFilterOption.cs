using MyApp.Domain.Enums;

namespace MyApp.Wpf.ViewModels;

/// <summary>Опція фільтра за типом обладнання.</summary>
/// <param name="Label">Підпис у списку.</param>
/// <param name="Value">Значення фільтра (null — усі).</param>
public sealed record EquipmentFilterOption(string Label, EquipmentType? Value);