using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Wpf.Services;

namespace MyApp.Wpf.ViewModels;

public class TrackWorkoutViewModel : ObservableObject
{
    private readonly IWorkoutTrackingService _trackingService;
    private readonly IExerciseRepository? _exerciseRepository;
    private readonly CurrentUserSession _userSession;
    private readonly ILogger<TrackWorkoutViewModel> _logger;

    private Exercise? _selectedExercise;
    private string? _statusMessage;
    private string? _errorMessage;
    private bool _isBusy;

    public TrackWorkoutViewModel(
        IWorkoutTrackingService trackingService,
        CurrentUserSession userSession,
        ILogger<TrackWorkoutViewModel> logger,
        IExerciseRepository? exerciseRepository = null)
    {
        _trackingService = trackingService ?? throw new ArgumentNullException(nameof(trackingService));
        _userSession = userSession ?? throw new ArgumentNullException(nameof(userSession));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _exerciseRepository = exerciseRepository;

        SaveCommand = new AsyncRelayCommand(SaveWorkoutAsync, () => !IsBusy);
        AddSetCommand = new RelayCommand(AddSet);
        RemoveSetCommand = new RelayCommand<WorkoutSetItem>(RemoveSet);
        LoadExercisesCommand = new AsyncRelayCommand(LoadExercisesAsync);

        // Додаємо початковий підхід за замовчуванням
        AddSet();
    }

    public ObservableCollection<Exercise> Exercises { get; } = new();

    public ObservableCollection<WorkoutSetItem> Sets { get; } = new();

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

    public IRelayCommand AddSetCommand { get; }

    public IRelayCommand<WorkoutSetItem> RemoveSetCommand { get; }

    public IAsyncRelayCommand LoadExercisesCommand { get; }

    public async Task LoadExercisesAsync()
    {
        try
        {
            Exercises.Clear();
            if (_exerciseRepository is not null)
            {
                var exercises = await _exerciseRepository.GetAllAsync();
                foreach (var exercise in exercises)
                {
                    Exercises.Add(exercise);
                }
            }

            // Якщо репозиторій вправ ще не реалізований Денисом, використовуємо базовий список для перегляду форми
            if (Exercises.Count == 0)
            {
                Exercises.Add(new Exercise { Id = 1, Name = "Жим лежачи" });
                Exercises.Add(new Exercise { Id = 2, Name = "Присідання зі штангою" });
                Exercises.Add(new Exercise { Id = 3, Name = "Станова тяга" });
                Exercises.Add(new Exercise { Id = 4, Name = "Підтягування" });
            }

            SelectedExercise ??= Exercises[0];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка при завантаженні списку вправ");
            ErrorMessage = "Не вдалося завантажити список вправ.";
        }
    }

    public void AddSet()
    {
        int nextNumber = Sets.Count + 1;
        decimal lastWeight = Sets.Count > 0 ? Sets[^1].Weight : 50m;
        int lastReps = Sets.Count > 0 ? Sets[^1].Reps : 10;

        Sets.Add(new WorkoutSetItem
        {
            SetNumber = nextNumber,
            Weight = lastWeight,
            Reps = lastReps,
        });

        ErrorMessage = null;
    }

    public void RemoveSet(WorkoutSetItem? item)
    {
        if (item is null || !Sets.Contains(item))
        {
            return;
        }

        Sets.Remove(item);
        ReorderSets();
        ErrorMessage = null;
    }

    private void ReorderSets()
    {
        for (int i = 0; i < Sets.Count; i++)
        {
            Sets[i].SetNumber = i + 1;
        }
    }

    private async Task SaveWorkoutAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;

        if (SelectedExercise is null)
        {
            ErrorMessage = "Будь ласка, оберіть вправу.";
            return;
        }

        if (Sets.Count == 0)
        {
            ErrorMessage = "Додайте щонайменше один підхід.";
            return;
        }

        IsBusy = true;
        try
        {
            var setDtos = Sets.Select(s => new WorkoutSetDto(s.Reps, s.Weight)).ToList();
            var request = new TrackWorkoutRequest(_userSession.UserId, SelectedExercise.Id, setDtos);

            var result = await _trackingService.TrackAsync(request);
            if (!result.IsSuccess)
            {
                ErrorMessage = result.Message ?? "Не вдалося зберегти тренування.";
                _logger.LogWarning("Помилка збереження тренування: {Message}", result.Message);
                return;
            }

            StatusMessage = "Тренування успішно збережено!";
            _logger.LogInformation("Тренування успішно збережено з форми WPF для вправи {Exercise}", SelectedExercise.Name);

            // Очищаємо підходи до одного базового
            Sets.Clear();
            AddSet();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Непередбачена помилка при збереженні тренування");
            ErrorMessage = $"Помилка збереження: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
