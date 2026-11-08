using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Domain.Enums;
using MyApp.Wpf.Services;

namespace MyApp.Wpf.ViewModels;

public sealed class WorkoutSessionsViewModel : ObservableObject
{
    private readonly IWorkoutSessionService _sessionService;
    private readonly IWorkoutTemplateService _templateService;
    private readonly CurrentUserSession _userSession;
    private readonly ILogger<WorkoutSessionsViewModel> _logger;

    private WorkoutTemplateSummaryDto? _selectedTemplate;
    private DateTime? _scheduledDate = DateTime.Today;
    private string _workoutName = string.Empty;
    private SessionStatusFilterOption _selectedFilter;
    private WorkoutSessionItemDto? _selectedSession;
    private string? _statusMessage;
    private string? _errorMessage;
    private bool _isBusy;

    public WorkoutSessionsViewModel(
        IWorkoutSessionService sessionService,
        IWorkoutTemplateService templateService,
        CurrentUserSession userSession,
        ILogger<WorkoutSessionsViewModel> logger)
    {
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
        _userSession = userSession ?? throw new ArgumentNullException(nameof(userSession));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        StatusFilters = new ObservableCollection<SessionStatusFilterOption>
        {
            new("Усі", null),
            new("Заплановано", SessionStatus.Planned),
            new("Виконано", SessionStatus.Completed),
            new("Пропущено", SessionStatus.Skipped),
            new("Скасовано", SessionStatus.Canceled),
        };
        _selectedFilter = StatusFilters[0];

        LoadCommand = new AsyncRelayCommand(LoadAsync, () => !IsBusy);
        ScheduleCommand = new AsyncRelayCommand(ScheduleAsync, () => !IsBusy);
        MarkCompletedCommand = new AsyncRelayCommand(
            () => ChangeStatusAsync(SessionStatus.Completed), CanChangeStatus);
        SkipCommand = new AsyncRelayCommand(
            () => ChangeStatusAsync(SessionStatus.Skipped), CanChangeStatus);
        CancelCommand = new AsyncRelayCommand(
            () => ChangeStatusAsync(SessionStatus.Canceled), CanChangeStatus);
    }

    public ObservableCollection<WorkoutTemplateSummaryDto> Templates { get; } = new();

    public ObservableCollection<WorkoutSessionItemDto> Sessions { get; } = new();

    public ObservableCollection<SessionStatusFilterOption> StatusFilters { get; }

    public WorkoutTemplateSummaryDto? SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            if (SetProperty(ref _selectedTemplate, value) && string.IsNullOrWhiteSpace(WorkoutName) && value is not null)
            {
                WorkoutName = value.Name;
            }
        }
    }

    public DateTime? ScheduledDate
    {
        get => _scheduledDate;
        set => SetProperty(ref _scheduledDate, value);
    }

    public string WorkoutName
    {
        get => _workoutName;
        set => SetProperty(ref _workoutName, value);
    }

    public SessionStatusFilterOption SelectedFilter
    {
        get => _selectedFilter;
        set
        {
            if (SetProperty(ref _selectedFilter, value))
            {
                _ = LoadSessionsAsync();
            }
        }
    }

    public WorkoutSessionItemDto? SelectedSession
    {
        get => _selectedSession;
        set
        {
            if (SetProperty(ref _selectedSession, value))
            {
                NotifyStatusCommands();
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
                LoadCommand.NotifyCanExecuteChanged();
                ScheduleCommand.NotifyCanExecuteChanged();
                NotifyStatusCommands();
            }
        }
    }

    public IAsyncRelayCommand LoadCommand { get; }

    public IAsyncRelayCommand ScheduleCommand { get; }

    public IAsyncRelayCommand MarkCompletedCommand { get; }

    public IAsyncRelayCommand SkipCommand { get; }

    public IAsyncRelayCommand CancelCommand { get; }

    public async Task LoadAsync()
    {
        ErrorMessage = null;
        IsBusy = true;

        try
        {
            var templates = await _templateService.GetUserTemplatesAsync(
                _userSession.TrainerId,
                string.Empty);

            Templates.Clear();
            foreach (var template in templates)
            {
                Templates.Add(template);
            }

            await LoadSessionsCoreAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка завантаження розкладу тренувань");
            ErrorMessage = "Не вдалося завантажити розклад тренувань.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanChangeStatus() => !IsBusy && SelectedSession is not null;

    private void NotifyStatusCommands()
    {
        MarkCompletedCommand.NotifyCanExecuteChanged();
        SkipCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }

    private async Task LoadSessionsAsync()
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            await LoadSessionsCoreAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка фільтрації сесій");
            ErrorMessage = "Не вдалося оновити список сесій.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadSessionsCoreAsync()
    {
        int previousId = SelectedSession?.SessionId ?? 0;

        var items = await _sessionService.GetUserSessionsAsync(
            _userSession.TrainerId,
            SelectedFilter.Value);

        Sessions.Clear();
        foreach (var item in items)
        {
            Sessions.Add(item);
        }

        SelectedSession = Sessions.FirstOrDefault(s => s.SessionId == previousId);
    }

    private async Task ScheduleAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;
        IsBusy = true;

        try
        {
            var request = new ScheduleWorkoutSessionRequest(
                _userSession.TrainerId,
                SelectedTemplate?.TemplateId ?? 0,
                WorkoutName,
                ScheduledDate ?? default);

            var result = await _sessionService.ScheduleSessionFromTemplateAsync(request);
            if (!result.IsSuccess)
            {
                ErrorMessage = result.Message ?? "Не вдалося запланувати тренування.";
                return;
            }

            StatusMessage = $"Тренування «{WorkoutName}» заплановано.";
            WorkoutName = string.Empty;
            await LoadSessionsCoreAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Непередбачена помилка при плануванні тренування");
            ErrorMessage = "Сталася помилка під час планування тренування.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ChangeStatusAsync(SessionStatus newStatus)
    {
        if (SelectedSession is null)
        {
            return;
        }

        ErrorMessage = null;
        StatusMessage = null;
        IsBusy = true;

        try
        {
            var request = new UpdateSessionStatusRequest(
                _userSession.TrainerId,
                SelectedSession.SessionId,
                newStatus);

            var result = await _sessionService.UpdateSessionStatusAsync(request);
            if (!result.IsSuccess)
            {
                ErrorMessage = result.Message ?? "Не вдалося змінити статус сесії.";
                return;
            }

            StatusMessage = "Статус сесії оновлено.";
            await LoadSessionsCoreAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Непередбачена помилка при зміні статусу сесії");
            ErrorMessage = "Сталася помилка під час зміни статусу.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}