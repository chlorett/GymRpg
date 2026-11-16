using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Domain.Enums;
using MyApp.Wpf.Services;

namespace MyApp.Wpf.ViewModels;

/// <summary>ViewModel вікна «Каталог вправ».</summary>
public sealed partial class ExerciseCatalogViewModel : ObservableObject
{
    private const int MaxSearchLength = 100;

    private readonly IExerciseCatalogService service;
    private readonly CurrentUserSession userSession;
    private readonly ILogger<ExerciseCatalogViewModel>? logger;

    private int? editingId;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private EquipmentFilterOption selectedFilter;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ArchiveExerciseCommand))]
    private ExerciseCatalogItemDto? selectedExercise;

    [ObservableProperty]
    private bool isEmpty;

    [ObservableProperty]
    private string formName = string.Empty;

    [ObservableProperty]
    private EquipmentFilterOption selectedFormEquipment;

    [ObservableProperty]
    private string formDescription = string.Empty;

    [ObservableProperty]
    private string formTechnique = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCreating))]
    private bool isEditing;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private string? statusMessage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveExerciseCommand))]
    [NotifyCanExecuteChangedFor(nameof(ArchiveExerciseCommand))]
    private bool isBusy;

    /// <summary>Initializes a new instance of the <see cref="ExerciseCatalogViewModel"/> class.</summary>
    /// <param name="service">Сервіс каталогу вправ.</param>
    /// <param name="userSession">Сесія поточного користувача.</param>
    /// <param name="logger">Логер для фіксації попереджень валідації.</param>
    public ExerciseCatalogViewModel(
        IExerciseCatalogService service,
        CurrentUserSession userSession,
        ILogger<ExerciseCatalogViewModel>? logger = null)
    {
        this.service = service;
        this.userSession = userSession;
        this.logger = logger;
        this.FilterOptions = new[]
        {
            new EquipmentFilterOption("Усі", null),
            new EquipmentFilterOption("Тренажерний зал", EquipmentType.Gym),
            new EquipmentFilterOption("Калістеніка / Власна вага", EquipmentType.Calisthenics),
            new EquipmentFilterOption("Інше", EquipmentType.Other),
        };
        this.FormEquipmentOptions = this.FilterOptions.Where(o => o.Value is not null).ToArray();
        this.selectedFilter = this.FilterOptions[0];
        this.selectedFormEquipment = this.FormEquipmentOptions[0];
    }

    /// <summary>Gets доступні опції фільтра.</summary>
    public IReadOnlyList<EquipmentFilterOption> FilterOptions { get; }

    /// <summary>Gets доступні типи обладнання для форми вправи.</summary>
    public IReadOnlyList<EquipmentFilterOption> FormEquipmentOptions { get; }

    /// <summary>Gets список вправ, що відображається.</summary>
    public ObservableCollection<ExerciseCatalogItemDto> Exercises { get; } = new();

    /// <summary>Gets a value indicating whether поточний користувач (тренер або адміністратор) може керувати каталогом.</summary>
    public bool CanManage => this.ActorRole is UserRole.Trainer or UserRole.Admin;

    /// <summary>Gets a value indicating whether форма створює нову вправу (назву й обладнання можна змінювати).</summary>
    public bool IsCreating => !this.IsEditing;

    private UserRole ActorRole => this.userSession.Role;

    partial void OnSearchTextChanged(string value) => this.Reload();

    partial void OnSelectedFilterChanged(EquipmentFilterOption value) => this.Reload();

    partial void OnSelectedExerciseChanged(ExerciseCatalogItemDto? value)
    {
        if (value is null || value.Id == this.editingId)
        {
            return;
        }

        this.editingId = value.Id;
        this.IsEditing = true;
        this.FormName = value.Name;
        this.SelectedFormEquipment =
            this.FormEquipmentOptions.FirstOrDefault(o => o.Value == value.EquipmentType)
            ?? this.FormEquipmentOptions[0];
        this.FormDescription = value.Description;
        this.FormTechnique = value.Technique;
        this.ErrorMessage = null;
        this.StatusMessage = null;
    }

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
        var result = await this.service.GetExercisesAsync(this.SearchText, this.SelectedFilter.Value, ct);
        var items = result.IsSuccess && result.Value is not null
            ? result.Value
            : Array.Empty<ExerciseCatalogItemDto>();

        if (!result.IsSuccess)
        {
            this.ErrorMessage = result.Message;
        }

        this.Exercises.Clear();
        foreach (var item in items)
        {
            this.Exercises.Add(item);
        }

        this.IsEmpty = this.Exercises.Count == 0;
        this.SelectedExercise = this.Exercises.FirstOrDefault(e => e.Id == selectedId);
    }

    [RelayCommand]
    private void NewExercise()
    {
        this.ResetForm();
        this.ErrorMessage = null;
        this.StatusMessage = null;
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveExerciseAsync(CancellationToken ct)
    {
        this.ErrorMessage = null;
        this.StatusMessage = null;
        this.IsBusy = true;

        try
        {
            var request = new SaveExerciseRequest(
                this.editingId,
                this.FormName,
                this.SelectedFormEquipment.Value ?? EquipmentType.Gym,
                this.FormDescription,
                this.FormTechnique);

            var result = await this.service.SaveExerciseAsync(request, this.ActorRole, ct);
            if (!result.IsSuccess)
            {
                this.ErrorMessage = result.Message ?? "Не вдалося зберегти вправу.";
                return;
            }

            if (this.editingId is null)
            {
                this.StatusMessage = "Вправу додано до каталогу.";
                this.ResetForm();
            }
            else
            {
                this.StatusMessage = "Зміни збережено.";
            }

            await this.RefreshAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            this.logger?.LogError(ex, "Непередбачена помилка при збереженні вправи");
            this.ErrorMessage = "Сталася помилка під час збереження вправи.";
        }
        finally
        {
            this.IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanArchive))]
    private async Task ArchiveExerciseAsync(CancellationToken ct)
    {
        if (this.SelectedExercise is not { } exercise)
        {
            return;
        }

        this.ErrorMessage = null;
        this.StatusMessage = null;
        this.IsBusy = true;

        try
        {
            var result = await this.service.ArchiveExerciseAsync(exercise.Id, this.ActorRole, ct);
            if (!result.IsSuccess)
            {
                this.ErrorMessage = result.Message ?? "Не вдалося архівувати вправу.";
                return;
            }

            this.StatusMessage = $"Вправу «{exercise.Name}» архівовано.";
            this.ResetForm();
            await this.RefreshAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            this.logger?.LogError(ex, "Непередбачена помилка при архівації вправи {ExerciseId}", exercise.Id);
            this.ErrorMessage = "Сталася помилка під час архівації вправи.";
        }
        finally
        {
            this.IsBusy = false;
        }
    }

    private bool CanSave() => this.CanManage && !this.IsBusy;

    private bool CanArchive() => this.CanManage && !this.IsBusy && this.SelectedExercise is not null;

    private void ResetForm()
    {
        this.editingId = null;
        this.IsEditing = false;
        this.FormName = string.Empty;
        this.SelectedFormEquipment = this.FormEquipmentOptions[0];
        this.FormDescription = string.Empty;
        this.FormTechnique = string.Empty;
        this.SelectedExercise = null;
    }

    private void Reload()
    {
        this.RefreshCommand.Cancel();
        this.RefreshCommand.Execute(null);
    }
}