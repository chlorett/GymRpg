using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Wpf.Services;

namespace MyApp.Wpf.ViewModels;

public sealed class StrengthStandardsViewModel : ObservableObject
{
    private readonly IStrengthStandardsService _strengthService;
    private readonly CurrentUserSession _userSession;
    private readonly ILogger<StrengthStandardsViewModel> _logger;

    private string _selectedRankFilter = "Усі";
    private ExerciseStrengthStandardItemDto? _selectedStandard;
    private int _targetReps = 5;
    private decimal _athleteBodyWeightKg = 75.0m;
    private decimal _bestRelativeRatio;
    private string _overallStrengthRank = "Початківець";
    private int _pushSharePercent = 34;
    private int _pullSharePercent = 33;
    private int _legsSharePercent = 33;
    private int _symmetryScorePercent = 100;
    private string _symmetryAdviceText = "—";
    private string? _statusMessage;
    private string? _errorMessage;
    private bool _isBusy;

    public StrengthStandardsViewModel(
        IStrengthStandardsService strengthService,
        CurrentUserSession userSession,
        ILogger<StrengthStandardsViewModel> logger)
    {
        _strengthService = strengthService ?? throw new ArgumentNullException(nameof(strengthService));
        _userSession = userSession ?? throw new ArgumentNullException(nameof(userSession));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        LoadOverviewCommand = new AsyncRelayCommand(() => LoadOverviewAsync(preserveStatus: false), () => !IsBusy);
        CalculateTargetCommand = new AsyncRelayCommand(
            CalculateNextStandardTargetAsync,
            () => !IsBusy && SelectedStandard is not null);
    }

    public ObservableCollection<ExerciseStrengthStandardItemDto> ExerciseStandards { get; } = new();

    public IReadOnlyList<string> RankFilterOptions { get; } = new[]
    {
        "Усі",
        "Початківець",
        "Любитель",
        "Атлет",
        "Еліта",
    };

    public IReadOnlyList<int> TargetRepsOptions { get; } = new[] { 1, 3, 5, 8, 10, 12, 15 };

    public string SelectedRankFilter
    {
        get => _selectedRankFilter;
        set
        {
            if (SetProperty(ref _selectedRankFilter, value))
            {
                _ = LoadOverviewAsync(preserveStatus: false);
            }
        }
    }

    public ExerciseStrengthStandardItemDto? SelectedStandard
    {
        get => _selectedStandard;
        set
        {
            if (SetProperty(ref _selectedStandard, value))
            {
                CalculateTargetCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public int TargetReps
    {
        get => _targetReps;
        set => SetProperty(ref _targetReps, value);
    }

    public decimal AthleteBodyWeightKg
    {
        get => _athleteBodyWeightKg;
        set
        {
            if (SetProperty(ref _athleteBodyWeightKg, value))
            {
                OnPropertyChanged(nameof(AthleteBodyWeightText));
            }
        }
    }

    public string AthleteBodyWeightText => string.Create(
        CultureInfo.InvariantCulture,
        $"{AthleteBodyWeightKg:0.#} кг");

    public decimal BestRelativeRatio
    {
        get => _bestRelativeRatio;
        set
        {
            if (SetProperty(ref _bestRelativeRatio, value))
            {
                OnPropertyChanged(nameof(BestRelativeRatioText));
            }
        }
    }

    public string BestRelativeRatioText => string.Create(
        CultureInfo.InvariantCulture,
        $"{BestRelativeRatio:0.00}x власної ваги");

    public string OverallStrengthRank
    {
        get => _overallStrengthRank;
        set => SetProperty(ref _overallStrengthRank, value);
    }

    public int PushSharePercent
    {
        get => _pushSharePercent;
        set
        {
            if (SetProperty(ref _pushSharePercent, value))
            {
                OnPropertyChanged(nameof(BalanceDistributionText));
            }
        }
    }

    public int PullSharePercent
    {
        get => _pullSharePercent;
        set
        {
            if (SetProperty(ref _pullSharePercent, value))
            {
                OnPropertyChanged(nameof(BalanceDistributionText));
            }
        }
    }

    public int LegsSharePercent
    {
        get => _legsSharePercent;
        set
        {
            if (SetProperty(ref _legsSharePercent, value))
            {
                OnPropertyChanged(nameof(BalanceDistributionText));
            }
        }
    }

    public string BalanceDistributionText =>
        $"Push: {PushSharePercent}% · Pull: {PullSharePercent}% · Legs: {LegsSharePercent}%";

    public int SymmetryScorePercent
    {
        get => _symmetryScorePercent;
        set
        {
            if (SetProperty(ref _symmetryScorePercent, value))
            {
                OnPropertyChanged(nameof(SymmetryScoreText));
            }
        }
    }

    public string SymmetryScoreText => $"{SymmetryScorePercent}%";

    public string SymmetryAdviceText
    {
        get => _symmetryAdviceText;
        set => SetProperty(ref _symmetryAdviceText, value);
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
                CalculateTargetCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public IAsyncRelayCommand LoadOverviewCommand { get; }

    public IAsyncRelayCommand CalculateTargetCommand { get; }

    public async Task LoadOverviewAsync(bool preserveStatus = false)
    {
        if (!preserveStatus)
        {
            ErrorMessage = null;
        }

        IsBusy = true;

        try
        {
            var result = await _strengthService.GetStrengthStandardsOverviewAsync(
                _userSession.UserId,
                SelectedRankFilter);

            if (!result.IsSuccess || result.Value is null)
            {
                _logger.LogWarning(
                    "Помилка завантаження силових стандартів для UserId={UserId}: [{ErrorCode}] {Message}",
                    _userSession.UserId,
                    result.ErrorCode,
                    result.Message);
                ErrorMessage = result.Message ?? "Не вдалося завантажити силові стандарти та симетрію.";
                return;
            }

            var overview = result.Value;
            AthleteBodyWeightKg = overview.AthleteBodyWeightKg;
            BestRelativeRatio = overview.BestRelativeRatio;
            OverallStrengthRank = overview.OverallStrengthRank;
            PushSharePercent = overview.PushSharePercent;
            PullSharePercent = overview.PullSharePercent;
            LegsSharePercent = overview.LegsSharePercent;
            SymmetryScorePercent = overview.SymmetryScorePercent;
            SymmetryAdviceText = overview.SymmetryAdviceText;

            ExerciseStandards.Clear();
            foreach (var item in overview.ExerciseStandards)
            {
                ExerciseStandards.Add(item);
            }

            SelectedStandard ??= ExerciseStandards.FirstOrDefault();

            if (!preserveStatus)
            {
                StatusMessage = $"Атлет: {_userSession.UserName} ({AthleteBodyWeightText}) • Силовий розряд: {OverallStrengthRank} • Симетрія: {SymmetryScorePercent}%";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка завантаження вікна силових стандартів");
            ErrorMessage = "Сталася помилка при завантаженні силових стандартів та симетрії.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task CalculateNextStandardTargetAsync()
    {
        if (SelectedStandard is null)
        {
            return;
        }

        ErrorMessage = null;
        StatusMessage = null;
        IsBusy = true;

        try
        {
            var result = await _strengthService.CalculateNextStandardTargetAsync(
                _userSession.UserId,
                SelectedStandard.ExerciseId,
                TargetReps);

            if (!result.IsSuccess || result.Value is null)
            {
                _logger.LogWarning(
                    "Помилка розрахунку цільової робочої ваги для UserId={UserId}, ExerciseId={ExerciseId}: [{ErrorCode}] {Message}",
                    _userSession.UserId,
                    SelectedStandard.ExerciseId,
                    result.ErrorCode,
                    result.Message);
                ErrorMessage = result.Message ?? "Не вдалося розрахувати цільову робочу вагу.";
                return;
            }

            StatusMessage = result.Value.PlanSummaryText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Непередбачена помилка при розрахунку цільової ваги");
            ErrorMessage = "Не вдалося виконати розрахунок робочої ваги.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
