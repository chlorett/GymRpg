using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Wpf.Services;

namespace MyApp.Wpf.ViewModels;

public sealed class WorkoutTemplatesViewModel : ObservableObject
{
    private readonly IWorkoutTemplateService _templateService;
    private readonly CurrentUserSession _userSession;
    private readonly ILogger<WorkoutTemplatesViewModel> _logger;

    private string _searchQuery = string.Empty;
    private WorkoutTemplateSummaryDto? _selectedTemplate;
    private WorkoutTemplateDetailsDto? _selectedTemplateDetails;
    private string? _statusMessage;
    private string? _errorMessage;
    private bool _isBusy;

    public WorkoutTemplatesViewModel(
        IWorkoutTemplateService templateService,
        CurrentUserSession userSession,
        ILogger<WorkoutTemplatesViewModel> logger)
    {
        _templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
        _userSession = userSession ?? throw new ArgumentNullException(nameof(userSession));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        LoadTemplatesCommand = new AsyncRelayCommand(LoadTemplatesAsync, () => !IsBusy);
        ClearSearchCommand = new AsyncRelayCommand(ClearSearchAsync, () => !IsBusy);
        DeleteTemplateCommand = new AsyncRelayCommand(
            DeleteSelectedTemplateAsync,
            () => !IsBusy && SelectedTemplate is not null);
    }

    public ObservableCollection<WorkoutTemplateSummaryDto> Templates { get; } = new();

    public ObservableCollection<TemplateExerciseItemDto> SelectedExercises { get; } = new();

    public string SearchQuery
    {
        get => _searchQuery;
        set => SetProperty(ref _searchQuery, value);
    }

    public WorkoutTemplateSummaryDto? SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            if (SetProperty(ref _selectedTemplate, value))
            {
                DeleteTemplateCommand.NotifyCanExecuteChanged();
                _ = LoadSelectedTemplateDetailsAsync(value?.TemplateId);
            }
        }
    }

    public WorkoutTemplateDetailsDto? SelectedTemplateDetails
    {
        get => _selectedTemplateDetails;
        set
        {
            if (SetProperty(ref _selectedTemplateDetails, value))
            {
                OnPropertyChanged(nameof(SelectedTemplateTitleText));
                OnPropertyChanged(nameof(SelectedTemplateMetaText));
                OnPropertyChanged(nameof(SelectedTemplateDescriptionText));
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
                LoadTemplatesCommand.NotifyCanExecuteChanged();
                ClearSearchCommand.NotifyCanExecuteChanged();
                DeleteTemplateCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public int TotalTemplatesCount => Templates.Count;

    public string TrainerHeaderText =>
        $"Тренер: {_userSession.TrainerName} (ID: {_userSession.TrainerId}) • Збережених шаблонів: {Templates.Count}";

    public string SelectedTemplateTitleText =>
        SelectedTemplateDetails is { } details
            ? details.Name
            : "Оберіть шаблон зі списку";

    public string SelectedTemplateMetaText =>
        SelectedTemplateDetails is { } details
            ? $"Створено: {details.CreatedAtText} • Вправ: {details.ExercisesCount} • Підходів: {details.TotalSetsCount} • Повторень: {details.TotalVolumeReps}"
            : "Натисніть на будь-який шаблон ліворуч, щоб переглянути його структуру вправ";

    public string SelectedTemplateDescriptionText =>
        SelectedTemplateDetails?.Description ?? string.Empty;

    public IAsyncRelayCommand LoadTemplatesCommand { get; }

    public IAsyncRelayCommand ClearSearchCommand { get; }

    public IAsyncRelayCommand DeleteTemplateCommand { get; }

    public async Task LoadTemplatesAsync()
    {
        ErrorMessage = null;
        IsBusy = true;

        try
        {
            int previousSelectedId = SelectedTemplate?.TemplateId ?? 0;

            var items = await _templateService.GetUserTemplatesAsync(
                _userSession.TrainerId,
                SearchQuery);

            Templates.Clear();
            foreach (var item in items)
            {
                Templates.Add(item);
            }

            OnPropertyChanged(nameof(TotalTemplatesCount));
            OnPropertyChanged(nameof(TrainerHeaderText));

            var restored = Templates.FirstOrDefault(t => t.TemplateId == previousSelectedId)
                ?? Templates.FirstOrDefault();

            SelectedTemplate = restored;
            if (restored is null)
            {
                SelectedTemplateDetails = null;
                SelectedExercises.Clear();
            }

            _logger.LogInformation(
                "Список шаблонів тренувань завантажено у WPF для тренера {TrainerId}: {Count} записів",
                _userSession.TrainerId,
                Templates.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка завантаження списку шаблонів тренувань");
            ErrorMessage = "Не вдалося завантажити список шаблонів тренувань.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task DeleteSelectedTemplateAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;

        if (SelectedTemplate is null)
        {
            ErrorMessage = "Оберіть шаблон тренування для видалення.";
            _logger.LogWarning("Спроба видалення шаблону без обраного елемента у WPF");
            return;
        }

        string templateName = SelectedTemplate.Name;
        int templateId = SelectedTemplate.TemplateId;

        IsBusy = true;
        try
        {
            var result = await _templateService.DeleteTemplateAsync(templateId, _userSession.TrainerId);
            if (!result.IsSuccess)
            {
                _logger.LogWarning(
                    "Помилка видалення шаблону {TemplateId}: [{ErrorCode}] Field={Field} Message={Message}",
                    templateId,
                    result.ErrorCode,
                    result.Field,
                    result.Message);
                ErrorMessage = result.Message ?? "Не вдалося видалити шаблон тренування.";
                return;
            }

            StatusMessage = $"Шаблон «{templateName}» успішно видалено.";
            await LoadTemplatesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Непередбачена помилка при видаленні шаблону {TemplateId}", templateId);
            ErrorMessage = "Сталася помилка під час видалення шаблону.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadSelectedTemplateDetailsAsync(int? templateId)
    {
        if (templateId is null or <= 0)
        {
            SelectedTemplateDetails = null;
            SelectedExercises.Clear();
            return;
        }

        try
        {
            var details = await _templateService.GetTemplateDetailsAsync(
                templateId.Value,
                _userSession.TrainerId);

            SelectedTemplateDetails = details;
            SelectedExercises.Clear();

            if (details is not null)
            {
                foreach (var ex in details.Exercises)
                {
                    SelectedExercises.Add(ex);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка завантаження деталей шаблону {TemplateId}", templateId);
            ErrorMessage = "Не вдалося завантажити вправи обраного шаблону.";
        }
    }

    private async Task ClearSearchAsync()
    {
        SearchQuery = string.Empty;
        StatusMessage = null;
        await LoadTemplatesAsync();
    }
}
