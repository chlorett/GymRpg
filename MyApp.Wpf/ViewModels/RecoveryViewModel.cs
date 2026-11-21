using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Wpf.Services;

namespace MyApp.Wpf.ViewModels;

public sealed class RecoveryViewModel : ObservableObject
{
    private const int DefaultDeloadPercent = 30;

    private readonly IRecoveryService _recoveryService;
    private readonly CurrentUserSession _userSession;
    private readonly ILogger<RecoveryViewModel> _logger;

    private int _selectedPeriodDays = 7;
    private int _currentStreakDays;
    private int _bestStreakDays;
    private int _workoutsInPeriod;
    private int _totalSetsInPeriod;
    private decimal _totalVolumeInPeriodKg;
    private int _earnedXpInPeriod;
    private int _readinessScorePercent = 100;
    private string _readinessSummaryText = "—";
    private string? _statusMessage;
    private string? _errorMessage;
    private bool _isBusy;

    public RecoveryViewModel(
        IRecoveryService recoveryService,
        CurrentUserSession userSession,
        ILogger<RecoveryViewModel> logger)
    {
        _recoveryService = recoveryService ?? throw new ArgumentNullException(nameof(recoveryService));
        _userSession = userSession ?? throw new ArgumentNullException(nameof(userSession));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        LoadOverviewCommand = new AsyncRelayCommand(() => LoadOverviewAsync(preserveStatus: false), () => !IsBusy);
        ApplyDeloadCommand = new AsyncRelayCommand(ApplyDeloadAsync, () => !IsBusy);
    }

    public ObservableCollection<MuscleRecoveryStatusItemDto> MuscleRecoveryItems { get; } = new();

    public IReadOnlyList<int> PeriodDayOptions { get; } = new[] { 7, 14, 30 };

    public int SelectedPeriodDays
    {
        get => _selectedPeriodDays;
        set
        {
            if (SetProperty(ref _selectedPeriodDays, value))
            {
                _ = LoadOverviewAsync(preserveStatus: false);
            }
        }
    }

    public int CurrentStreakDays
    {
        get => _currentStreakDays;
        set => SetProperty(ref _currentStreakDays, value);
    }

    public int BestStreakDays
    {
        get => _bestStreakDays;
        set => SetProperty(ref _bestStreakDays, value);
    }

    public int WorkoutsInPeriod
    {
        get => _workoutsInPeriod;
        set => SetProperty(ref _workoutsInPeriod, value);
    }

    public int TotalSetsInPeriod
    {
        get => _totalSetsInPeriod;
        set => SetProperty(ref _totalSetsInPeriod, value);
    }

    public decimal TotalVolumeInPeriodKg
    {
        get => _totalVolumeInPeriodKg;
        set
        {
            if (SetProperty(ref _totalVolumeInPeriodKg, value))
            {
                OnPropertyChanged(nameof(TotalVolumeInPeriodText));
            }
        }
    }

    public string TotalVolumeInPeriodText => string.Create(
        CultureInfo.InvariantCulture,
        $"{TotalVolumeInPeriodKg:0.#} кг");

    public int EarnedXpInPeriod
    {
        get => _earnedXpInPeriod;
        set => SetProperty(ref _earnedXpInPeriod, value);
    }

    public int ReadinessScorePercent
    {
        get => _readinessScorePercent;
        set
        {
            if (SetProperty(ref _readinessScorePercent, value))
            {
                OnPropertyChanged(nameof(ReadinessScoreText));
            }
        }
    }

    public string ReadinessScoreText => $"{ReadinessScorePercent}%";

    public string ReadinessSummaryText
    {
        get => _readinessSummaryText;
        set => SetProperty(ref _readinessSummaryText, value);
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
                ApplyDeloadCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public IAsyncRelayCommand LoadOverviewCommand { get; }

    public IAsyncRelayCommand ApplyDeloadCommand { get; }

    public async Task LoadOverviewAsync(bool preserveStatus = false)
    {
        if (!preserveStatus)
        {
            ErrorMessage = null;
        }

        IsBusy = true;

        try
        {
            var result = await _recoveryService.GetRecoveryOverviewAsync(
                _userSession.UserId,
                SelectedPeriodDays);

            if (!result.IsSuccess || result.Value is null)
            {
                _logger.LogWarning(
                    "Помилка валідації при завантаженні активності та відновлення для UserId={UserId}: [{ErrorCode}] {Message}",
                    _userSession.UserId,
                    result.ErrorCode,
                    result.Message);
                ErrorMessage = result.Message ?? "Не вдалося завантажити показники активності та відновлення.";
                return;
            }

            var overview = result.Value;
            CurrentStreakDays = overview.CurrentStreakDays;
            BestStreakDays = overview.BestStreakDays;
            WorkoutsInPeriod = overview.WorkoutsInPeriod;
            TotalSetsInPeriod = overview.TotalSetsInPeriod;
            TotalVolumeInPeriodKg = overview.TotalVolumeInPeriodKg;
            EarnedXpInPeriod = overview.EarnedXpInPeriod;
            ReadinessScorePercent = overview.ReadinessScorePercent;
            ReadinessSummaryText = overview.ReadinessSummaryText;

            MuscleRecoveryItems.Clear();
            foreach (var item in overview.MuscleRecoveryItems)
            {
                MuscleRecoveryItems.Add(item);
            }

            if (!preserveStatus)
            {
                StatusMessage = $"Атлет: {_userSession.UserName} • Період: {SelectedPeriodDays} дн. • Серія: {CurrentStreakDays} дн. (рекорд {BestStreakDays} дн.) • Готовність: {ReadinessScorePercent}%";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка завантаження вікна активності та відновлення");
            ErrorMessage = "Сталася помилка при завантаженні даних активності та відновлення.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ApplyDeloadAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;
        IsBusy = true;

        try
        {
            var result = await _recoveryService.ApplyRecoveryDeloadAsync(
                _userSession.UserId,
                DefaultDeloadPercent);

            if (!result.IsSuccess || result.Value is null)
            {
                _logger.LogWarning(
                    "Помилка проведення Deload для UserId={UserId}: [{ErrorCode}] Field={Field} Message={Message}",
                    _userSession.UserId,
                    result.ErrorCode,
                    result.Field,
                    result.Message);
                ErrorMessage = result.Message ?? "Не вдалося зареєструвати день відновлення.";
                return;
            }

            var deload = result.Value;
            StatusMessage = $"День відновлення (Deload -{deload.ReductionPercent}%) застосовано! Сумарне навантаження знижено з {deload.PreviousTotalLoad:0.#} до {deload.NewTotalLoad:0.#} од. Новий індекс готовності: {deload.NewReadinessScorePercent}%.";
            await LoadOverviewAsync(preserveStatus: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Непередбачена помилка під час проведення Deload");
            ErrorMessage = "Не вдалося застосувати відновлення м'язів.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
