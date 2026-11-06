using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Wpf.Services;

namespace MyApp.Wpf.ViewModels;

public sealed class MuscleLoadViewModel : ObservableObject
{
    private readonly IMuscleLoadService _muscleLoadService;
    private readonly CurrentUserSession _userSession;
    private readonly ILogger<MuscleLoadViewModel> _logger;

    private string _selectedStatusFilter = "Усі";
    private int _totalGroupsCount;
    private int _activeGroupsCount;
    private decimal _totalLoadValue;
    private string _topMuscleGroupName = "—";
    private string _leastLoadedMuscleGroupName = "—";
    private string? _statusMessage;
    private string? _errorMessage;
    private bool _isBusy;

    public MuscleLoadViewModel(
        IMuscleLoadService muscleLoadService,
        CurrentUserSession userSession,
        ILogger<MuscleLoadViewModel> logger)
    {
        _muscleLoadService = muscleLoadService ?? throw new ArgumentNullException(nameof(muscleLoadService));
        _userSession = userSession ?? throw new ArgumentNullException(nameof(userSession));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        LoadOverviewCommand = new AsyncRelayCommand(LoadOverviewAsync, () => !IsBusy);
        RecalculateCommand = new AsyncRelayCommand(RecalculateAsync, () => !IsBusy);
    }

    public ObservableCollection<MuscleGroupLoadItemDto> MuscleGroups { get; } = new();

    public IReadOnlyList<string> StatusFilterOptions { get; } = new[]
    {
        "Усі",
        "Активні",
        "Потребують уваги",
    };

    public string SelectedStatusFilter
    {
        get => _selectedStatusFilter;
        set
        {
            if (SetProperty(ref _selectedStatusFilter, value))
            {
                _ = LoadOverviewAsync();
            }
        }
    }

    public int TotalGroupsCount
    {
        get => _totalGroupsCount;
        set
        {
            if (SetProperty(ref _totalGroupsCount, value))
            {
                OnPropertyChanged(nameof(ActiveGroupsBadge));
                OnPropertyChanged(nameof(SummaryHeaderText));
            }
        }
    }

    public int ActiveGroupsCount
    {
        get => _activeGroupsCount;
        set
        {
            if (SetProperty(ref _activeGroupsCount, value))
            {
                OnPropertyChanged(nameof(ActiveGroupsBadge));
                OnPropertyChanged(nameof(SummaryHeaderText));
            }
        }
    }

    public decimal TotalLoadValue
    {
        get => _totalLoadValue;
        set
        {
            if (SetProperty(ref _totalLoadValue, value))
            {
                OnPropertyChanged(nameof(SummaryHeaderText));
            }
        }
    }

    public string TopMuscleGroupName
    {
        get => _topMuscleGroupName;
        set
        {
            if (SetProperty(ref _topMuscleGroupName, value))
            {
                OnPropertyChanged(nameof(SummaryDetailsText));
            }
        }
    }

    public string LeastLoadedMuscleGroupName
    {
        get => _leastLoadedMuscleGroupName;
        set
        {
            if (SetProperty(ref _leastLoadedMuscleGroupName, value))
            {
                OnPropertyChanged(nameof(SummaryDetailsText));
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
                LoadOverviewCommand.NotifyCanExecuteChanged();
                RecalculateCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string ActiveGroupsBadge => $"{ActiveGroupsCount} / {TotalGroupsCount}";

    public string SummaryHeaderText => string.Create(
        CultureInfo.InvariantCulture,
        $"Загальне навантаження: {TotalLoadValue:F1} од. • Задіяно груп м'язів: {ActiveGroupsCount} із {TotalGroupsCount}");

    public string SummaryDetailsText =>
        $"Атлет: {_userSession.UserName} • Найактивніша група: {TopMuscleGroupName} • Потребує уваги: {LeastLoadedMuscleGroupName}";

    public IAsyncRelayCommand LoadOverviewCommand { get; }

    public IAsyncRelayCommand RecalculateCommand { get; }

    public async Task LoadOverviewAsync()
    {
        ErrorMessage = null;
        IsBusy = true;

        try
        {
            var overview = await _muscleLoadService.GetUserMuscleLoadOverviewAsync(
                _userSession.UserId,
                SelectedStatusFilter);

            TotalGroupsCount = overview.TotalGroupsCount;
            ActiveGroupsCount = overview.ActiveGroupsCount;
            TotalLoadValue = overview.TotalLoadValue;
            TopMuscleGroupName = overview.TopMuscleGroupName;
            LeastLoadedMuscleGroupName = overview.LeastLoadedMuscleGroupName;

            MuscleGroups.Clear();
            foreach (var item in overview.MuscleGroups)
            {
                MuscleGroups.Add(item);
            }

            _logger.LogInformation(
                "Аналітику м'язів оновлено у WPF для {Username}: активних {Active}/{Total}, сумарно {TotalLoad}",
                _userSession.UserName,
                overview.ActiveGroupsCount,
                overview.TotalGroupsCount,
                overview.TotalLoadValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка завантаження аналітики м'язового навантаження");
            ErrorMessage = "Не вдалося завантажити аналітику м'язового навантаження.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task RecalculateAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;
        IsBusy = true;

        try
        {
            var recalcResult = await _muscleLoadService.RecalculateMuscleLoadAsync(_userSession.UserId);
            if (!recalcResult.Result.IsSuccess)
            {
                _logger.LogWarning(
                    "Помилка валідації при перерахунку навантаження м'язів для UserId={UserId}: [{ErrorCode}] Field={Field} Message={Message}",
                    _userSession.UserId,
                    recalcResult.Result.ErrorCode,
                    recalcResult.Result.Field,
                    recalcResult.Result.Message);
                ErrorMessage = recalcResult.Result.Message ?? "Не вдалося перерахувати навантаження м'язів.";
                return;
            }

            StatusMessage = string.Create(
                CultureInfo.InvariantCulture,
                $"Баланс м'язів успішно перераховано! Оновлено груп: {recalcResult.UpdatedGroupsCount}, загальне навантаження: {recalcResult.TotalLoadValue:F1} од. (лідер: {recalcResult.TopMuscleGroupName}).");

            await LoadOverviewAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка під час перерахунку навантаження м'язів");
            ErrorMessage = "Сталася помилка під час перерахунку м'язового навантаження.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
