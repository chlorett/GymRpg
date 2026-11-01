using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Wpf.Services;

namespace MyApp.Wpf.ViewModels;

public sealed class AccountAffiliationViewModel : ObservableObject
{
    private const string NotChosen = "не обрано";

    private readonly IAccountAffiliationService _affiliations;
    private readonly CurrentUserSession _userSession;
    private readonly ILogger<AccountAffiliationViewModel> _logger;

    private IReadOnlyList<GymDto> _allGyms = Array.Empty<GymDto>();
    private UserAffiliationDto? _saved;

    private string _username = string.Empty;
    private RegionDto? _selectedRegion;
    private GymDto? _selectedGym;
    private DivisionDto? _selectedDivision;
    private string? _usernameError;
    private string? _gymError;
    private string? _divisionError;
    private string? _generalError;
    private string? _successMessage;
    private bool _isBusy;

    public AccountAffiliationViewModel(
        IAccountAffiliationService affiliations,
        CurrentUserSession userSession,
        ILogger<AccountAffiliationViewModel> logger)
    {
        _affiliations = affiliations ?? throw new ArgumentNullException(nameof(affiliations));
        _userSession = userSession ?? throw new ArgumentNullException(nameof(userSession));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy);
        ResetCommand = new RelayCommand(ResetChanges, () => !IsBusy);
    }

    public ObservableCollection<RegionDto> Regions { get; } = new();

    public ObservableCollection<GymDto> AvailableGyms { get; } = new();

    public ObservableCollection<DivisionDto> Divisions { get; } = new();

    public IAsyncRelayCommand SaveCommand { get; }

    public IRelayCommand ResetCommand { get; }

    public string CardUsername => _saved?.Username ?? "—";

    public string CardRegion => FindName(_saved?.Regions, _saved?.RegionId, r => r.Id, r => r.Name);

    public string CardGym => FindName(_saved?.Gyms, _saved?.GymId, g => g.Id, g => g.Name);

    public string CardDivision => FindName(_saved?.Divisions, _saved?.DivisionId, d => d.Id, d => d.Name);

    public string CardMembers => _saved is { GymId: not null } saved
        ? $"{saved.GymMembersCount}"
        : "—";

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public RegionDto? SelectedRegion
    {
        get => _selectedRegion;
        set
        {
            if (SetProperty(ref _selectedRegion, value))
            {
                RefreshAvailableGyms();
            }
        }
    }

    public GymDto? SelectedGym
    {
        get => _selectedGym;
        set => SetProperty(ref _selectedGym, value);
    }

    public DivisionDto? SelectedDivision
    {
        get => _selectedDivision;
        set => SetProperty(ref _selectedDivision, value);
    }

    public string? UsernameError
    {
        get => _usernameError;
        set => SetProperty(ref _usernameError, value);
    }

    public string? GymError
    {
        get => _gymError;
        set => SetProperty(ref _gymError, value);
    }

    public string? DivisionError
    {
        get => _divisionError;
        set => SetProperty(ref _divisionError, value);
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

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                SaveCommand.NotifyCanExecuteChanged();
                ResetCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public async Task InitializeAsync()
    {
        ClearMessages();
        IsBusy = true;

        try
        {
            var data = await _affiliations.GetAffiliationDataAsync(_userSession.UserId);
            if (data is null)
            {
                _logger.LogWarning("Не вдалося завантажити приналежність для користувача {UserId}", _userSession.UserId);
                GeneralError = "Не вдалося завантажити дані приналежності.";
                return;
            }

            ApplyData(data);
            _userSession.SetAffiliation(data.Username, data.GymId, data.DivisionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Непередбачена помилка під час завантаження приналежності");
            GeneralError = "Не вдалося завантажити дані приналежності. Спробуйте пізніше.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string FindName<T>(IReadOnlyList<T>? items, int? id, Func<T, int> getId, Func<T, string> getName)
    {
        if (items is null || id is null)
        {
            return NotChosen;
        }

        foreach (var item in items)
        {
            if (getId(item) == id)
            {
                return getName(item);
            }
        }

        return NotChosen;
    }

    private void ApplyData(UserAffiliationDto data)
    {
        _saved = data;
        _allGyms = data.Gyms;

        Regions.Clear();
        foreach (var region in data.Regions)
        {
            Regions.Add(region);
        }

        Divisions.Clear();
        foreach (var division in data.Divisions)
        {
            Divisions.Add(division);
        }

        ApplySavedSelection();
        RefreshCard();
    }

    // Повертає форму до останнього збереженого стану.
    private void ApplySavedSelection()
    {
        var saved = _saved;
        if (saved is null)
        {
            return;
        }

        Username = saved.Username;
        SelectedRegion = Regions.FirstOrDefault(r => r.Id == saved.RegionId);
        SelectedGym = _allGyms.FirstOrDefault(g => g.Id == saved.GymId);
        SelectedDivision = Divisions.FirstOrDefault(d => d.Id == saved.DivisionId);
    }

    private void RefreshAvailableGyms()
    {
        AvailableGyms.Clear();

        if (SelectedRegion is null)
        {
            SelectedGym = null;
            return;
        }

        foreach (var gym in _allGyms.Where(g => g.RegionId == SelectedRegion.Id))
        {
            AvailableGyms.Add(gym);
        }

        if (SelectedGym is not null && SelectedGym.RegionId != SelectedRegion.Id)
        {
            SelectedGym = null;
        }
    }

    private void RefreshCard()
    {
        OnPropertyChanged(nameof(CardUsername));
        OnPropertyChanged(nameof(CardRegion));
        OnPropertyChanged(nameof(CardGym));
        OnPropertyChanged(nameof(CardDivision));
        OnPropertyChanged(nameof(CardMembers));
    }

    private void ResetChanges()
    {
        ClearMessages();
        ApplySavedSelection();
    }

    private void ClearMessages()
    {
        UsernameError = null;
        GymError = null;
        DivisionError = null;
        GeneralError = null;
        SuccessMessage = null;
    }

    private void ShowError(OperationResult result)
    {
        var message = result.Message ?? "Не вдалося зберегти зміни.";

        switch (result.Field)
        {
            case nameof(UpdateAffiliationRequest.Username):
                UsernameError = message;
                break;
            case nameof(UpdateAffiliationRequest.GymId):
                GymError = message;
                break;
            case nameof(UpdateAffiliationRequest.DivisionId):
                DivisionError = message;
                break;
            default:
                GeneralError = message;
                break;
        }
    }

    private async Task SaveAsync()
    {
        ClearMessages();
        IsBusy = true;

        try
        {
            var request = new UpdateAffiliationRequest(
                _userSession.UserId,
                Username,
                SelectedGym?.Id,
                SelectedDivision?.Id);

            var result = await _affiliations.UpdateAffiliationAsync(request);
            if (!result.IsSuccess)
            {
                _logger.LogWarning(
                    "Оновлення приналежності користувача {UserId} відхилено: {ErrorCode}, поле {Field}",
                    _userSession.UserId,
                    result.ErrorCode,
                    result.Field);
                ShowError(result);
                return;
            }

            var data = await _affiliations.GetAffiliationDataAsync(_userSession.UserId);
            if (data is not null)
            {
                ApplyData(data);
                _userSession.SetAffiliation(data.Username, data.GymId, data.DivisionId);
            }

            _logger.LogInformation("Користувач {UserId} оновив клубну приналежність з форми WPF", _userSession.UserId);
            SuccessMessage = "Зміни збережено.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Непередбачена помилка під час збереження приналежності");
            GeneralError = "Не вдалося зберегти зміни. Спробуйте пізніше.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
