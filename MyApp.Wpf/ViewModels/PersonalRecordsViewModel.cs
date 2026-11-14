using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Wpf.Services;

namespace MyApp.Wpf.ViewModels;

public sealed class PersonalRecordsViewModel : ObservableObject
{
    private readonly IPersonalRecordService _personalRecordService;
    private readonly IExerciseRepository _exerciseRepository;
    private readonly CurrentUserSession _userSession;
    private readonly ILogger<PersonalRecordsViewModel> _logger;

    private bool _exercisesLoaded;
    private bool _suppressAutoLoad;
    private int _selectedExerciseId;
    private string _selectedHonestyFilter = "Усі";
    private WorkoutLogEntryDto? _selectedLogEntry;
    private int _totalWorkoutsLogged;
    private int _exercisesWithRecordsCount;
    private decimal _bestOneRepMaxKg;
    private string _bestExerciseName = "—";
    private string? _statusMessage;
    private string? _errorMessage;
    private bool _isBusy;

    public PersonalRecordsViewModel(
        IPersonalRecordService personalRecordService,
        IExerciseRepository exerciseRepository,
        CurrentUserSession userSession,
        ILogger<PersonalRecordsViewModel> logger)
    {
        _personalRecordService = personalRecordService ?? throw new ArgumentNullException(nameof(personalRecordService));
        _exerciseRepository = exerciseRepository ?? throw new ArgumentNullException(nameof(exerciseRepository));
        _userSession = userSession ?? throw new ArgumentNullException(nameof(userSession));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        LoadOverviewCommand = new AsyncRelayCommand(() => LoadOverviewAsync(preserveStatus: false), () => !IsBusy);
        ResetFiltersCommand = new AsyncRelayCommand(ResetFiltersAsync, () => !IsBusy);
        DeleteSelectedRecordCommand = new AsyncRelayCommand(
            DeleteSelectedRecordAsync,
            () => !IsBusy && SelectedLogEntry is not null);
    }

    public ObservableCollection<Exercise> Exercises { get; } = new();

    public ObservableCollection<ExercisePersonalRecordDto> PersonalRecords { get; } = new();

    public ObservableCollection<WorkoutLogEntryDto> WorkoutLogs { get; } = new();

    public IReadOnlyList<string> HonestyFilterOptions { get; } = new[]
    {
        "Усі",
        "Тільки чесні",
        "Підозрілі",
    };

    public int SelectedExerciseId
    {
        get => _selectedExerciseId;
        set
        {
            if (SetProperty(ref _selectedExerciseId, value) && !_suppressAutoLoad)
            {
                _ = LoadOverviewAsync(preserveStatus: false);
            }
        }
    }

    public string SelectedHonestyFilter
    {
        get => _selectedHonestyFilter;
        set
        {
            if (SetProperty(ref _selectedHonestyFilter, value) && !_suppressAutoLoad)
            {
                _ = LoadOverviewAsync(preserveStatus: false);
            }
        }
    }

    public WorkoutLogEntryDto? SelectedLogEntry
    {
        get => _selectedLogEntry;
        set
        {
            if (SetProperty(ref _selectedLogEntry, value))
            {
                OnPropertyChanged(nameof(HasSelectedLogEntry));
                DeleteSelectedRecordCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool HasSelectedLogEntry => SelectedLogEntry is not null;

    public int TotalWorkoutsLogged
    {
        get => _totalWorkoutsLogged;
        set => SetProperty(ref _totalWorkoutsLogged, value);
    }

    public int ExercisesWithRecordsCount
    {
        get => _exercisesWithRecordsCount;
        set => SetProperty(ref _exercisesWithRecordsCount, value);
    }

    public decimal BestOneRepMaxKg
    {
        get => _bestOneRepMaxKg;
        set
        {
            if (SetProperty(ref _bestOneRepMaxKg, value))
            {
                OnPropertyChanged(nameof(BestOneRepMaxText));
            }
        }
    }

    public string BestOneRepMaxText => string.Create(
        CultureInfo.InvariantCulture,
        $"{BestOneRepMaxKg:0.#} кг");

    public string BestExerciseName
    {
        get => _bestExerciseName;
        set => SetProperty(ref _bestExerciseName, value);
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
                LoadOverviewCommand.NotifyCanExecuteChanged();
                ResetFiltersCommand.NotifyCanExecuteChanged();
                DeleteSelectedRecordCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public IAsyncRelayCommand LoadOverviewCommand { get; }

    public IAsyncRelayCommand ResetFiltersCommand { get; }

    public IAsyncRelayCommand DeleteSelectedRecordCommand { get; }

    public async Task LoadOverviewAsync(bool preserveStatus = false)
    {
        if (!preserveStatus)
        {
            ErrorMessage = null;
        }

        IsBusy = true;

        try
        {
            if (!_exercisesLoaded)
            {
                await LoadExercisesDictionaryAsync();
            }

            int? exerciseFilter = SelectedExerciseId > 0 ? SelectedExerciseId : null;
            var overview = await _personalRecordService.GetPersonalRecordsOverviewAsync(
                _userSession.UserId,
                exerciseFilter,
                SelectedHonestyFilter);

            TotalWorkoutsLogged = overview.TotalWorkoutsLogged;
            ExercisesWithRecordsCount = overview.ExercisesWithRecordsCount;
            BestOneRepMaxKg = overview.BestOneRepMaxKg;
            BestExerciseName = overview.BestExerciseName;

            PersonalRecords.Clear();
            foreach (var pr in overview.PersonalRecords)
            {
                PersonalRecords.Add(pr);
            }

            WorkoutLogs.Clear();
            foreach (var log in overview.WorkoutLogs)
            {
                WorkoutLogs.Add(log);
            }

            if (!preserveStatus)
            {
                StatusMessage = $"Атлет: {_userSession.UserName} • Записів у журналі: {WorkoutLogs.Count} • Вправ із PR: {PersonalRecords.Count}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка завантаження історії та особистих рекордів");
            ErrorMessage = "Не вдалося завантажити історію тренувань та особисті рекорди.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ResetFiltersAsync()
    {
        _suppressAutoLoad = true;
        SelectedExerciseId = 0;
        SelectedHonestyFilter = "Усі";
        _suppressAutoLoad = false;

        await LoadOverviewAsync(preserveStatus: false);
    }

    public async Task DeleteSelectedRecordAsync()
    {
        if (SelectedLogEntry is null)
        {
            return;
        }

        int targetRecordId = SelectedLogEntry.RecordId;
        string exerciseName = SelectedLogEntry.ExerciseName;

        ErrorMessage = null;
        StatusMessage = null;
        IsBusy = true;

        try
        {
            var deleteResult = await _personalRecordService.DeleteWorkoutRecordAsync(
                _userSession.UserId,
                targetRecordId);

            if (!deleteResult.Result.IsSuccess)
            {
                _logger.LogWarning(
                    "Помилка валідації при видаленні запису тренування RecordId={RecordId} для UserId={UserId}: [{ErrorCode}] Field={Field} Message={Message}",
                    targetRecordId,
                    _userSession.UserId,
                    deleteResult.Result.ErrorCode,
                    deleteResult.Result.Field,
                    deleteResult.Result.Message);
                ErrorMessage = deleteResult.Result.Message ?? "Не вдалося видалити запис із журналу.";
                return;
            }

            StatusMessage = $"Запис вправи «{exerciseName}» (#{targetRecordId}) видалено з журналу. Актуальний прогрес: рівень {deleteResult.NewLevel} ({deleteResult.AdjustedXp} XP).";
            await LoadOverviewAsync(preserveStatus: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка під час видалення запису тренування");
            ErrorMessage = "Сталася помилка під час видалення запису з журналу.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadExercisesDictionaryAsync()
    {
        var exercises = await _exerciseRepository.GetAllAsync();
        Exercises.Clear();
        Exercises.Add(new Exercise { Id = 0, Name = "Усі вправи" });
        foreach (var ex in exercises.OrderBy(e => e.Name))
        {
            Exercises.Add(ex);
        }

        _exercisesLoaded = true;
    }
}
