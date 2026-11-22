using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Wpf.Services;

namespace MyApp.Wpf.ViewModels;

public sealed class DivisionQualificationViewModel : ObservableObject
{
    private const double FullProgress = 100d;

    private readonly IDivisionQualificationService _service;
    private readonly CurrentUserSession _userSession;
    private readonly ILogger<DivisionQualificationViewModel> _logger;

    private DivisionRequirementDto? _selectedDivision;
    private string _currentDivisionText = "—";
    private string _xpText = string.Empty;
    private string _progressText = string.Empty;
    private string _standingsTitle = string.Empty;
    private string? _statusMessage;
    private string? _errorMessage;
    private double _progressPercent;
    private int? _nextDivisionId;
    private bool _canPromote;
    private bool _isBusy;
    private bool _suppressFilter;

    public DivisionQualificationViewModel(
        IDivisionQualificationService service,
        CurrentUserSession userSession,
        ILogger<DivisionQualificationViewModel> logger)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _userSession = userSession ?? throw new ArgumentNullException(nameof(userSession));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        RefreshCommand = new AsyncRelayCommand(() => LoadAsync(null), () => !IsBusy);
        PromoteCommand = new AsyncRelayCommand(PromoteAsync, () => !IsBusy && CanPromote);
    }

    public ObservableCollection<DivisionRequirementDto> Requirements { get; } = new();

    public ObservableCollection<DivisionStandingEntryDto> Standings { get; } = new();

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand PromoteCommand { get; }

    public DivisionRequirementDto? SelectedDivision
    {
        get => _selectedDivision;
        set
        {
            if (SetProperty(ref _selectedDivision, value) && value is not null && !_suppressFilter)
            {
                _ = LoadAsync(value.DivisionId);
            }
        }
    }

    public string CurrentDivisionText
    {
        get => _currentDivisionText;
        private set => SetProperty(ref _currentDivisionText, value);
    }

    public string XpText
    {
        get => _xpText;
        private set => SetProperty(ref _xpText, value);
    }

    public string ProgressText
    {
        get => _progressText;
        private set => SetProperty(ref _progressText, value);
    }

    public string StandingsTitle
    {
        get => _standingsTitle;
        private set => SetProperty(ref _standingsTitle, value);
    }

    public double ProgressPercent
    {
        get => _progressPercent;
        private set => SetProperty(ref _progressPercent, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public bool CanPromote
    {
        get => _canPromote;
        private set
        {
            if (SetProperty(ref _canPromote, value))
            {
                PromoteCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RefreshCommand.NotifyCanExecuteChanged();
                PromoteCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public async Task InitializeAsync()
    {
        await LoadAsync(null);
    }

    private static double CalculateProgress(DivisionQualificationOverviewDto overview)
    {
        if (overview.NextDivisionId is not { } nextId)
        {
            return FullProgress;
        }

        var currentMin = overview.Divisions.FirstOrDefault(d => d.IsCurrent)?.MinXp ?? 0;
        var nextMin = overview.Divisions.FirstOrDefault(d => d.DivisionId == nextId)?.MinXp ?? currentMin;
        var span = nextMin - currentMin;
        if (span <= 0)
        {
            return FullProgress;
        }

        var done = (double)(overview.CurrentXp - currentMin) / span * FullProgress;
        return Math.Clamp(done, 0d, FullProgress);
    }

    private static string BuildProgressText(DivisionQualificationOverviewDto overview)
    {
        if (overview.NextDivisionName is null)
        {
            return "Ви в найвищому дивізіоні.";
        }

        if (overview.CanPromote)
        {
            return $"Норматив виконано: доступний перехід до «{overview.NextDivisionName}».";
        }

        return $"До «{overview.NextDivisionName}» залишилось {overview.XpToNextDivision} XP.";
    }

    private async Task LoadAsync(int? divisionIdFilter)
    {
        ErrorMessage = null;
        IsBusy = true;

        try
        {
            var result = await _service.GetDivisionQualificationOverviewAsync(_userSession.UserId, divisionIdFilter);
            if (!result.IsSuccess || result.Value is null)
            {
                _logger.LogWarning(
                    "Не вдалося завантажити кваліфікацію дивізіонів: {ErrorCode}, поле {Field}",
                    result.ErrorCode,
                    result.Field);
                ErrorMessage = result.Message ?? "Не вдалося завантажити дані дивізіонів.";
                return;
            }

            Apply(result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Непередбачена помилка під час завантаження дивізіонів");
            ErrorMessage = "Не вдалося завантажити дані. Спробуйте пізніше.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Apply(DivisionQualificationOverviewDto overview)
    {
        _suppressFilter = true;
        try
        {
            Requirements.Clear();
            foreach (var requirement in overview.Divisions)
            {
                Requirements.Add(requirement);
            }

            SelectedDivision = Requirements.FirstOrDefault(r => r.DivisionId == overview.SelectedDivisionId);
        }
        finally
        {
            _suppressFilter = false;
        }

        Standings.Clear();
        foreach (var entry in overview.Standings)
        {
            Standings.Add(entry);
        }

        CurrentDivisionText = overview.CurrentDivisionName;
        XpText = $"XP: {overview.CurrentXp} · Рівень: {overview.CurrentLevel}";
        StandingsTitle = $"Таблиця лідерів: {overview.SelectedDivisionName}";
        ProgressPercent = CalculateProgress(overview);
        _nextDivisionId = overview.NextDivisionId;
        CanPromote = overview.CanPromote;

        ProgressText = BuildProgressText(overview);
    }

    private async Task PromoteAsync()
    {
        if (_nextDivisionId is not { } targetId)
        {
            return;
        }

        ErrorMessage = null;
        StatusMessage = null;
        IsBusy = true;

        try
        {
            var result = await _service.PromoteToDivisionAsync(new PromoteDivisionRequest(_userSession.UserId, targetId));
            if (!result.IsSuccess || result.Value is null)
            {
                _logger.LogWarning(
                    "Підвищення дивізіону користувача {UserId} відхилено: {ErrorCode}, поле {Field}",
                    _userSession.UserId,
                    result.ErrorCode,
                    result.Field);
                ErrorMessage = result.Message ?? "Не вдалося виконати підвищення.";
                return;
            }

            _logger.LogInformation(
                "Користувач {UserId} перейшов у дивізіон {DivisionId} через вікно WPF",
                _userSession.UserId,
                result.Value.NewDivisionId);
            _userSession.SetAffiliation(_userSession.UserName, _userSession.GymId, result.Value.NewDivisionId);
            StatusMessage = $"Вітаємо! Ви перейшли до дивізіону «{result.Value.NewDivisionName}».";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Непередбачена помилка під час підвищення дивізіону");
            ErrorMessage = "Не вдалося виконати підвищення. Спробуйте пізніше.";
            return;
        }
        finally
        {
            IsBusy = false;
        }

        await LoadAsync(null);
    }
}
