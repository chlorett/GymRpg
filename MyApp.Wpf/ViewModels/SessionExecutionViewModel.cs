using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Wpf.Services;

namespace MyApp.Wpf.ViewModels;

public sealed class SessionExecutionViewModel : ObservableObject
{
    private readonly IWorkoutSessionService _sessionService;
    private readonly CurrentUserSession _userSession;
    private readonly ILogger<SessionExecutionViewModel> _logger;

    private string _workoutName = string.Empty;
    private string _scheduledDateText = string.Empty;
    private string? _statusMessage;
    private string? _errorMessage;
    private bool _isBusy;
    private bool _isCompleted;

    public SessionExecutionViewModel(
        IWorkoutSessionService sessionService,
        CurrentUserSession userSession,
        ILogger<SessionExecutionViewModel> logger)
    {
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _userSession = userSession ?? throw new ArgumentNullException(nameof(userSession));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        CompleteCommand = new AsyncRelayCommand(CompleteAsync, CanComplete);
    }

    public ObservableCollection<ExecutionExerciseRow> Exercises { get; } = new();

    public int SessionId { get; set; }

    public string WorkoutName
    {
        get => _workoutName;
        set => SetProperty(ref _workoutName, value);
    }

    public string ScheduledDateText
    {
        get => _scheduledDateText;
        set => SetProperty(ref _scheduledDateText, value);
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
                OnPropertyChanged(nameof(IsEditable));
                CompleteCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsCompleted
    {
        get => _isCompleted;
        set
        {
            if (SetProperty(ref _isCompleted, value))
            {
                OnPropertyChanged(nameof(IsEditable));
                CompleteCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsEditable => !IsBusy && !IsCompleted;

    public IAsyncRelayCommand CompleteCommand { get; }

    public async Task LoadAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;
        IsBusy = true;

        try
        {
            var result = await _sessionService.GetSessionExecutionPlanAsync(_userSession.UserId, SessionId);
            if (!result.IsSuccess || result.Value is null)
            {
                ErrorMessage = result.Message ?? "Не вдалося завантажити план сесії.";
                return;
            }

            var plan = result.Value;
            WorkoutName = plan.WorkoutName;
            ScheduledDateText = $"Дата: {plan.ScheduledDate.ToString("dd.MM.yyyy", CultureInfo.CurrentCulture)}";

            Exercises.Clear();
            foreach (var exercise in plan.Exercises)
            {
                Exercises.Add(new ExecutionExerciseRow(exercise));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка завантаження плану сесії {SessionId}", SessionId);
            ErrorMessage = "Не вдалося завантажити план сесії.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static bool TryParseWeight(string text, out decimal weight)
    {
        if (text.Length == 0)
        {
            weight = 0m;
            return true;
        }

        return decimal.TryParse(
            text.Replace(',', '.'),
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out weight);
    }

    private bool CanComplete() => IsEditable && Exercises.Count > 0;

    private async Task CompleteAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;

        var request = BuildRequest();
        if (request is null)
        {
            return;
        }

        IsBusy = true;

        try
        {
            var result = await _sessionService.CompleteSessionWithRecordsAsync(request);
            if (!result.IsSuccess || result.Value is null)
            {
                ErrorMessage = result.Message ?? "Не вдалося зберегти сесію.";
                return;
            }

            var summary = result.Value;
            StatusMessage =
                $"Сесію завершено! +{summary.EarnedXp} XP, рівень {summary.NewLevel}, " +
                $"виконано вправ: {summary.CompletedExercisesCount}.";
            IsCompleted = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Непередбачена помилка при завершенні сесії {SessionId}", SessionId);
            ErrorMessage = "Сталася помилка під час збереження сесії.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private CompleteSessionExecutionRequest? BuildRequest()
    {
        var executed = new List<ExecutedExerciseDto>();

        foreach (var exercise in Exercises)
        {
            var sets = new List<WorkoutSetDto>();

            foreach (var set in exercise.Sets)
            {
                string repsText = set.Reps.Trim();
                string weightText = set.Weight.Trim();

                if (repsText.Length == 0 && weightText.Length == 0)
                {
                    continue;
                }

                if (!int.TryParse(repsText, NumberStyles.None, CultureInfo.InvariantCulture, out int reps))
                {
                    ErrorMessage = $"«{exercise.Name}», підхід {set.SetNumber}: вкажіть повторення цілим числом.";
                    return null;
                }

                if (!TryParseWeight(weightText, out decimal weight))
                {
                    ErrorMessage = $"«{exercise.Name}», підхід {set.SetNumber}: вага має бути невід'ємним числом.";
                    return null;
                }

                sets.Add(new WorkoutSetDto(reps, weight));
            }

            executed.Add(new ExecutedExerciseDto(exercise.ExerciseId, sets));
        }

        return new CompleteSessionExecutionRequest(_userSession.UserId, SessionId, executed);
    }
}