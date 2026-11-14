using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Wpf.Services;

namespace MyApp.Wpf.ViewModels;

public sealed class GymStandingsViewModel : ObservableObject
{
    private readonly IGymStandingsService _standings;
    private readonly CurrentUserSession _userSession;
    private readonly ILogger<GymStandingsViewModel> _logger;

    private RegionDto? _filterRegion;
    private DivisionDto? _filterDivision;
    private RegionDto? _newGymRegion;
    private string _newGymName = string.Empty;
    private string? _regionError;
    private string? _nameError;
    private string? _generalError;
    private string? _successMessage;
    private string _summary = string.Empty;
    private bool _canRegisterGyms;
    private bool _isBusy;
    private bool _referenceLoaded;

    public GymStandingsViewModel(
        IGymStandingsService standings,
        CurrentUserSession userSession,
        ILogger<GymStandingsViewModel> logger)
    {
        _standings = standings ?? throw new ArgumentNullException(nameof(standings));
        _userSession = userSession ?? throw new ArgumentNullException(nameof(userSession));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        RefreshCommand = new AsyncRelayCommand(LoadAsync, () => !IsBusy);
        ResetFiltersCommand = new AsyncRelayCommand(ResetFiltersAsync, () => !IsBusy);
        AddGymCommand = new AsyncRelayCommand(AddGymAsync, () => !IsBusy && CanRegisterGyms);
    }

    public ObservableCollection<GymStandingDto> Standings { get; } = new();

    public ObservableCollection<RegionDto> Regions { get; } = new();

    public ObservableCollection<DivisionDto> Divisions { get; } = new();

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand ResetFiltersCommand { get; }

    public IAsyncRelayCommand AddGymCommand { get; }

    public RegionDto? FilterRegion
    {
        get => _filterRegion;
        set
        {
            if (SetProperty(ref _filterRegion, value) && _referenceLoaded)
            {
                _ = LoadAsync();
            }
        }
    }

    public DivisionDto? FilterDivision
    {
        get => _filterDivision;
        set
        {
            if (SetProperty(ref _filterDivision, value) && _referenceLoaded)
            {
                _ = LoadAsync();
            }
        }
    }

    public RegionDto? NewGymRegion
    {
        get => _newGymRegion;
        set => SetProperty(ref _newGymRegion, value);
    }

    public string NewGymName
    {
        get => _newGymName;
        set => SetProperty(ref _newGymName, value);
    }

    public string? RegionError
    {
        get => _regionError;
        set => SetProperty(ref _regionError, value);
    }

    public string? NameError
    {
        get => _nameError;
        set => SetProperty(ref _nameError, value);
    }

    public string? GeneralError
    {
        get => _generalError;
        set => SetProperty(ref _generalError, value);
    }

    public string? SuccessMessage
    {
        get => _successMessage;
        set => SetProperty(ref _successMessage, value);
    }

    public string Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    public bool CanRegisterGyms
    {
        get => _canRegisterGyms;
        private set
        {
            if (SetProperty(ref _canRegisterGyms, value))
            {
                AddGymCommand.NotifyCanExecuteChanged();
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
                ResetFiltersCommand.NotifyCanExecuteChanged();
                AddGymCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public async Task InitializeAsync()
    {
        await LoadAsync();
    }

    private static string FormatSummary(int clubs, int athletes)
    {
        return $"Клубів у рейтингу: {clubs} · чесних атлетів: {athletes}";
    }

    private async Task LoadAsync()
    {
        GeneralError = null;
        IsBusy = true;

        try
        {
            var overview = await _standings.GetStandingsAsync(
                _userSession.UserId,
                FilterRegion?.Id,
                FilterDivision?.Id);

            Standings.Clear();
            foreach (var row in overview.Standings)
            {
                Standings.Add(row);
            }

            Summary = FormatSummary(overview.Standings.Count, overview.Standings.Sum(s => s.ActiveAthletes));
            CanRegisterGyms = overview.CanRegisterGyms;

            if (!_referenceLoaded)
            {
                FillReferenceData(overview);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Непередбачена помилка під час завантаження рейтингу спортзалів");
            GeneralError = "Не вдалося завантажити рейтинг. Спробуйте пізніше.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void FillReferenceData(GymStandingsOverviewDto overview)
    {
        Regions.Clear();
        foreach (var region in overview.Regions)
        {
            Regions.Add(region);
        }

        Divisions.Clear();
        foreach (var division in overview.Divisions)
        {
            Divisions.Add(division);
        }

        _referenceLoaded = true;
    }

    private async Task ResetFiltersAsync()
    {
        _referenceLoaded = false;
        FilterRegion = null;
        FilterDivision = null;
        _referenceLoaded = true;

        await LoadAsync();
    }

    private async Task AddGymAsync()
    {
        RegionError = null;
        NameError = null;
        GeneralError = null;
        SuccessMessage = null;
        IsBusy = true;

        try
        {
            var request = new RegisterGymRequest(_userSession.UserId, NewGymRegion?.Id ?? 0, NewGymName);
            var result = await _standings.RegisterGymAsync(request);
            if (!result.IsSuccess)
            {
                _logger.LogWarning(
                    "Реєстрацію спортзалу користувачем {UserId} відхилено: {ErrorCode}, поле {Field}",
                    _userSession.UserId,
                    result.ErrorCode,
                    result.Field);
                ShowError(result);
                return;
            }

            _logger.LogInformation("Користувач {UserId} зареєстрував спортзал через форму WPF", _userSession.UserId);
            NewGymName = string.Empty;
            SuccessMessage = "Спортзал додано.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Непередбачена помилка під час реєстрації спортзалу");
            GeneralError = "Не вдалося додати спортзал. Спробуйте пізніше.";
            return;
        }
        finally
        {
            IsBusy = false;
        }

        await LoadAsync();
    }

    private void ShowError(OperationResult result)
    {
        var message = result.Message ?? "Не вдалося додати спортзал.";

        switch (result.Field)
        {
            case nameof(RegisterGymRequest.Name):
                NameError = message;
                break;
            case nameof(RegisterGymRequest.RegionId):
                RegionError = message;
                break;
            default:
                GeneralError = message;
                break;
        }
    }
}
