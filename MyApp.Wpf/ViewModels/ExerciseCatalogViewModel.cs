using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Domain.Enums;

namespace MyApp.Wpf.ViewModels;

/// <summary>ViewModel вікна «Каталог вправ».</summary>
public sealed partial class ExerciseCatalogViewModel : ObservableObject
{
    private const int MaxSearchLength = 100;

    private readonly IExerciseCatalogService service;
    private readonly ILogger<ExerciseCatalogViewModel>? logger;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private EquipmentFilterOption selectedFilter;

    [ObservableProperty]
    private ExerciseCatalogItemDto? selectedExercise;

    [ObservableProperty]
    private bool isEmpty;

    /// <summary>Initializes a new instance of the <see cref="ExerciseCatalogViewModel"/> class.</summary>
    /// <param name="service">Сервіс каталогу вправ.</param>
    /// <param name="logger">Логер для фіксації попереджень валідації.</param>
    public ExerciseCatalogViewModel(
        IExerciseCatalogService service,
        ILogger<ExerciseCatalogViewModel>? logger = null)
    {
        this.service = service;
        this.logger = logger;
        this.FilterOptions = new[]
        {
            new EquipmentFilterOption("Усі", null),
            new EquipmentFilterOption("Тренажерний зал", EquipmentType.Gym),
            new EquipmentFilterOption("Калістеніка / Власна вага", EquipmentType.Calisthenics),
        };
        this.selectedFilter = this.FilterOptions[0];
    }

    /// <summary>Gets доступні опції фільтра.</summary>
    public IReadOnlyList<EquipmentFilterOption> FilterOptions { get; }

    /// <summary>Gets список вправ, що відображається.</summary>
    public ObservableCollection<ExerciseCatalogItemDto> Exercises { get; } = new();

    partial void OnSearchTextChanged(string value) => this.Reload();

    partial void OnSelectedFilterChanged(EquipmentFilterOption value) => this.Reload();

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task RefreshAsync(CancellationToken ct)
    {
        if (this.SearchText.Trim().Length > MaxSearchLength)
        {
            this.logger?.LogWarning(
                "Помилка валідації пошуку в каталозі вправ: довжина запиту перевищує {MaxLength} символів",
                MaxSearchLength);
        }

        var selectedId = this.SelectedExercise?.Id;
        var items = await this.service.GetExercisesAsync(this.SearchText, this.SelectedFilter.Value, ct);

        this.Exercises.Clear();
        foreach (var item in items)
        {
            this.Exercises.Add(item);
        }

        this.IsEmpty = this.Exercises.Count == 0;
        this.SelectedExercise = this.Exercises.FirstOrDefault(e => e.Id == selectedId);
    }

    private void Reload()
    {
        this.RefreshCommand.Cancel();
        this.RefreshCommand.Execute(null);
    }
}
