using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Wpf.Services;

namespace MyApp.Wpf.ViewModels;

public partial class CreateTemplateViewModel : ObservableObject
{
    private const int DefaultTargetSets = 3;
    private const int DefaultTargetReps = 10;

    private readonly IWorkoutTemplateService _templateService;
    private readonly IExerciseRepository _exerciseRepository;
    private readonly CurrentUserSession _userSession;
    private readonly ILogger<CreateTemplateViewModel> _logger;

    private string _name = string.Empty;
    private Exercise? _selectedExercise;
    private int _targetSets = DefaultTargetSets;
    private int _targetReps = DefaultTargetReps;
    private string? _statusMessage;
    private string? _errorMessage;
    private bool _isBusy;

    public CreateTemplateViewModel(
        IWorkoutTemplateService templateService,
        IExerciseRepository exerciseRepository,
        CurrentUserSession userSession,
        ILogger<CreateTemplateViewModel> logger)
    {
        _templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
        _exerciseRepository = exerciseRepository ?? throw new ArgumentNullException(nameof(exerciseRepository));
        _userSession = userSession ?? throw new ArgumentNullException(nameof(userSession));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        SaveCommand = new AsyncRelayCommand(SaveTemplateAsync, () => !IsBusy);
        AddExerciseCommand = new RelayCommand(AddExercise);
        RemoveExerciseCommand = new RelayCommand<TemplateExerciseItem>(RemoveExercise);
        LoadExercisesCommand = new AsyncRelayCommand(LoadExercisesAsync);
    }

    public ObservableCollection<Exercise> Exercises { get; } = new();

    public ObservableCollection<TemplateExerciseItem> TemplateExercises { get; } = new();

    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
            {
                ErrorMessage = null;
            }
        }
    }

    public Exercise? SelectedExercise
    {
        get => _selectedExercise;
        set
        {
            if (SetProperty(ref _selectedExercise, value))
            {
                ErrorMessage = null;
            }
        }
    }

    public int TargetSets
    {
        get => _targetSets;
        set
        {
            if (SetProperty(ref _targetSets, value))
            {
                ErrorMessage = null;
            }
        }
    }

    public int TargetReps
    {
        get => _targetReps;
        set
        {
            if (SetProperty(ref _targetReps, value))
            {
                ErrorMessage = null;
            }
        }
    }

    public string CurrentTrainerInfo => $"Тренер: {_userSession.TrainerName} (ID: {_userSession.TrainerId})";

    public string? StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                SaveCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public IAsyncRelayCommand SaveCommand { get; }

    public IRelayCommand AddExerciseCommand { get; }

    public IRelayCommand<TemplateExerciseItem> RemoveExerciseCommand { get; }

    public IAsyncRelayCommand LoadExercisesCommand { get; }

    public async Task LoadExercisesAsync()
    {
        try
        {
            Exercises.Clear();

            var exercises = await _exerciseRepository.GetAllAsync();
            foreach (var exercise in exercises)
            {
                Exercises.Add(exercise);
            }

            if (Exercises.Count == 0)
            {
                Exercises.Add(new Exercise { Id = 1, Name = "Жим лежачи на лаві" });
                Exercises.Add(new Exercise { Id = 2, Name = "Присідання зі штангою" });
                Exercises.Add(new Exercise { Id = 3, Name = "Станова тяга" });
                Exercises.Add(new Exercise { Id = 4, Name = "Підтягування на перекладині" });
            }

            SelectedExercise ??= Exercises[0];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка при завантаженні каталогу вправ");
            ErrorMessage = "Не вдалося завантажити каталог вправ.";
        }
    }

    public void AddExercise()
    {
        StatusMessage = null;

        if (SelectedExercise is null)
        {
            ErrorMessage = "Оберіть вправу з каталогу.";
            _logger.LogWarning("Помилка валідації шаблону тренування: не обрано вправу з каталогу");
            return;
        }

        if (TemplateExercises.Any(item => item.ExerciseId == SelectedExercise.Id))
        {
            ErrorMessage = "Ця вправа вже є в шаблоні.";
            _logger.LogWarning(
                "Помилка валідації шаблону тренування: спроба додати дублікат вправи {ExerciseId}",
                SelectedExercise.Id);
            return;
        }

        TemplateExercises.Add(new TemplateExerciseItem(
            SelectedExercise.Id,
            SelectedExercise.Name,
            TargetSets,
            TargetReps));

        ErrorMessage = null;
    }

    public void RemoveExercise(TemplateExerciseItem? item)
    {
        if (item is null || !TemplateExercises.Contains(item))
        {
            return;
        }

        TemplateExercises.Remove(item);
        ErrorMessage = null;
    }

    private async Task SaveTemplateAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;

        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Вкажіть назву шаблону.";
            _logger.LogWarning("Помилка валідації шаблону тренування: порожня назва шаблону");
            return;
        }

        if (TemplateExercises.Count == 0)
        {
            ErrorMessage = "Додайте щонайменше одну вправу до шаблону.";
            _logger.LogWarning("Помилка валідації шаблону тренування: список вправ порожній");
            return;
        }

        IsBusy = true;
        try
        {
            var exercises = TemplateExercises
                .Select(item => new TemplateExerciseDto(item.ExerciseId, item.TargetSets, item.TargetReps))
                .ToList();
            var request = new CreateTemplateRequest(_userSession.TrainerId, Name.Trim(), exercises);

            var result = await _templateService.CreateAsync(request);
            if (!result.IsSuccess)
            {
                ErrorMessage = result.Message ?? "Не вдалося створити шаблон.";
                _logger.LogWarning("Помилка створення шаблону: {Message}", result.Message);
                return;
            }

            _logger.LogInformation("Шаблон тренування створено з форми WPF: {Name}", request.Name);

            var createdName = request.Name;
            ResetForm();
            StatusMessage = $"Шаблон '{createdName}' успішно створено!";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Непередбачена помилка при створенні шаблону");
            ErrorMessage = $"Помилка збереження: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ResetForm()
    {
        Name = string.Empty;
        TargetSets = DefaultTargetSets;
        TargetReps = DefaultTargetReps;
        TemplateExercises.Clear();
    }
}
