using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Wpf.Services;

namespace MyApp.Wpf.ViewModels;

public class GymModerationViewModel : INotifyPropertyChanged
{
    private const string AllowedModeratorUsername = "sazerwar";

    private readonly IGymModerationService _moderationService;
    private readonly IUserRepository _userRepository;
    private readonly CurrentUserSession _userSession;
    private readonly ILogger<GymModerationViewModel> _logger;

    private bool _filtersLoaded;
    private bool _suppressAutoLoad;
    private int? _selectedGymId = 0;
    private int? _selectedDivisionId = 0;
    private string _searchQuery = string.Empty;
    private GymRosterMemberDto? _selectedMember;
    private int _totalAthletes;
    private int _activeInLeaderboard;
    private int _excludedFromLeaderboard;
    private int _blockedAthletes;
    private string _statusMessage = string.Empty;
    private string _errorMessage = string.Empty;

    public GymModerationViewModel(
        IGymModerationService moderationService,
        IUserRepository userRepository,
        CurrentUserSession userSession,
        ILogger<GymModerationViewModel> logger)
    {
        _moderationService = moderationService;
        _userRepository = userRepository;
        _userSession = userSession;
        _logger = logger;

        RefreshCommand = new AsyncRelayCommand(() => LoadRosterAsync(preserveStatus: false));
        ResetFiltersCommand = new AsyncRelayCommand(ResetFiltersAsync);
        ToggleLeaderboardExclusionCommand = new AsyncRelayCommand(
            ToggleLeaderboardExclusionAsync,
            () => SelectedMember != null);
        ToggleBlockStatusCommand = new AsyncRelayCommand(
            ToggleBlockStatusAsync,
            () => SelectedMember != null);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<Gym> Gyms { get; } = new();

    public ObservableCollection<Division> Divisions { get; } = new();

    public ObservableCollection<GymRosterMemberDto> Members { get; } = new();

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand ResetFiltersCommand { get; }

    public IAsyncRelayCommand ToggleLeaderboardExclusionCommand { get; }

    public IAsyncRelayCommand ToggleBlockStatusCommand { get; }

    public int? SelectedGymId
    {
        get => _selectedGymId;
        set
        {
            if (_selectedGymId != value)
            {
                _selectedGymId = value;
                OnPropertyChanged();
                if (!_suppressAutoLoad)
                {
                    _ = LoadRosterAsync(preserveStatus: false);
                }
            }
        }
    }

    public int? SelectedDivisionId
    {
        get => _selectedDivisionId;
        set
        {
            if (_selectedDivisionId != value)
            {
                _selectedDivisionId = value;
                OnPropertyChanged();
                if (!_suppressAutoLoad)
                {
                    _ = LoadRosterAsync(preserveStatus: false);
                }
            }
        }
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (_searchQuery != value)
            {
                _searchQuery = value;
                OnPropertyChanged();
                if (!_suppressAutoLoad)
                {
                    _ = LoadRosterAsync(preserveStatus: false);
                }
            }
        }
    }

    public GymRosterMemberDto? SelectedMember
    {
        get => _selectedMember;
        set
        {
            _selectedMember = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ExcludeButtonText));
            OnPropertyChanged(nameof(BlockButtonText));
            OnPropertyChanged(nameof(HasSelectedMember));
            ToggleLeaderboardExclusionCommand.NotifyCanExecuteChanged();
            ToggleBlockStatusCommand.NotifyCanExecuteChanged();
        }
    }

    public bool HasSelectedMember => SelectedMember != null;

    public string ExcludeButtonText => SelectedMember?.IsExcludedFromLeaderboard == true
        ? "Повернути в лідерборд"
        : "Приховати з лідерборду";

    public string BlockButtonText => SelectedMember?.IsBlocked == true
        ? "Розблокувати акаунт"
        : "Заблокувати акаунт";

    public int TotalAthletes
    {
        get => _totalAthletes;
        set
        {
            _totalAthletes = value;
            OnPropertyChanged();
        }
    }

    public int ActiveInLeaderboard
    {
        get => _activeInLeaderboard;
        set
        {
            _activeInLeaderboard = value;
            OnPropertyChanged();
        }
    }

    public int ExcludedFromLeaderboard
    {
        get => _excludedFromLeaderboard;
        set
        {
            _excludedFromLeaderboard = value;
            OnPropertyChanged();
        }
    }

    public int BlockedAthletes
    {
        get => _blockedAthletes;
        set
        {
            _blockedAthletes = value;
            OnPropertyChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            _errorMessage = value;
            OnPropertyChanged();
        }
    }

    public async Task LoadRosterAsync(bool preserveStatus = false)
    {
        if (!_filtersLoaded)
        {
            await LoadFilterDictionariesAsync();
        }

        int? gymFilter = SelectedGymId is > 0 ? SelectedGymId : null;
        int? divFilter = SelectedDivisionId is > 0 ? SelectedDivisionId : null;
        int? previousSelectedUserId = SelectedMember?.UserId;

        var result = await _moderationService.GetGymRosterAsync(gymFilter, divFilter, SearchQuery);
        if (result == null)
        {
            ErrorMessage = "Помилка завантаження списку учасників.";
            return;
        }

        TotalAthletes = result.TotalAthletes;
        ActiveInLeaderboard = result.ActiveInLeaderboard;
        ExcludedFromLeaderboard = result.ExcludedFromLeaderboard;
        BlockedAthletes = result.BlockedAthletes;

        Members.Clear();
        foreach (var member in result.Members)
        {
            Members.Add(member);
        }

        if (previousSelectedUserId.HasValue)
        {
            SelectedMember = Members.FirstOrDefault(m => m.UserId == previousSelectedUserId.Value);
        }

        if (!preserveStatus)
        {
            ErrorMessage = string.Empty;
            string roleNote = IsCurrentUserSazerwar() ? "Адміністратор" : "Лише перегляд";
            StatusMessage = $"Завантажено учасників: {Members.Count} | Поточний акаунт: {_userSession.UserName} ({roleNote})";
        }
    }

    public async Task ResetFiltersAsync()
    {
        _suppressAutoLoad = true;
        SelectedGymId = 0;
        SelectedDivisionId = 0;
        SearchQuery = string.Empty;
        _suppressAutoLoad = false;

        await LoadRosterAsync(preserveStatus: false);
    }

    public async Task ToggleLeaderboardExclusionAsync()
    {
        if (SelectedMember == null)
        {
            return;
        }

        if (!EnsureSazerwarAccess())
        {
            return;
        }

        var targetId = SelectedMember.UserId;
        var targetName = SelectedMember.Nickname;
        var newExcludedState = !SelectedMember.IsExcludedFromLeaderboard;

        var request = new UpdateAthleteModerationRequest
        {
            ModeratorId = _userSession.UserId,
            TargetUserId = targetId,
            IsExcludedFromLeaderboard = newExcludedState,
            IsBlocked = SelectedMember.IsBlocked,
            BlockedAt = SelectedMember.BlockedAt,
        };

        var result = await _moderationService.UpdateModerationStatusAsync(request);
        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "Помилка зміни статусу у лідерборді для {TargetUserId}: {ErrorCode} - {Message}",
                targetId,
                result.ErrorCode,
                result.Message);
            StatusMessage = string.Empty;
            ErrorMessage = result.Message ?? "Не вдалося оновити статус у лідерборді.";
            return;
        }

        ErrorMessage = string.Empty;
        StatusMessage = newExcludedState
            ? $"Атлета «{targetName}» приховано з лідерборду."
            : $"Атлета «{targetName}» повернуто в активний лідерборд.";
        await LoadRosterAsync(preserveStatus: true);
    }

    public async Task ToggleBlockStatusAsync()
    {
        if (SelectedMember == null)
        {
            return;
        }

        if (!EnsureSazerwarAccess())
        {
            return;
        }

        var targetId = SelectedMember.UserId;
        var targetName = SelectedMember.Nickname;
        var newBlockState = !SelectedMember.IsBlocked;

        var request = new UpdateAthleteModerationRequest
        {
            ModeratorId = _userSession.UserId,
            TargetUserId = targetId,
            IsExcludedFromLeaderboard = newBlockState || SelectedMember.IsExcludedFromLeaderboard,
            IsBlocked = newBlockState,
        };

        var result = await _moderationService.UpdateModerationStatusAsync(request);
        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "Помилка зміни статусу блокування для {TargetUserId}: {ErrorCode} - {Message}",
                targetId,
                result.ErrorCode,
                result.Message);
            StatusMessage = string.Empty;
            ErrorMessage = result.Message ?? "Не вдалося змінити статус блокування.";
            return;
        }

        ErrorMessage = string.Empty;
        StatusMessage = newBlockState
            ? $"Акаунт «{targetName}» заблоковано та виключено з рейтингу."
            : $"Акаунт «{targetName}» розблоковано.";
        await LoadRosterAsync(preserveStatus: true);
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool IsCurrentUserSazerwar() =>
        string.Equals(_userSession.UserName, AllowedModeratorUsername, StringComparison.OrdinalIgnoreCase);

    private bool EnsureSazerwarAccess()
    {
        if (IsCurrentUserSazerwar())
        {
            return true;
        }

        _logger.LogWarning(
            "Відмовлено у доступі до модерації для користувача {Username} (ID: {UserId}). Доступ дозволено лише {AllowedModerator}",
            _userSession.UserName,
            _userSession.UserId,
            AllowedModeratorUsername);

        StatusMessage = string.Empty;
        ErrorMessage = $"Доступ заборонено: модерація дозволена лише адміністратору ({AllowedModeratorUsername}). Поточний акаунт: {_userSession.UserName}.";
        return false;
    }

    private async Task LoadFilterDictionariesAsync()
    {
        var gyms = await _userRepository.GetGymsAsync();
        var divisions = await _userRepository.GetDivisionsAsync();

        Gyms.Clear();
        Gyms.Add(new Gym { Id = 0, Name = "Усі спортзали" });
        foreach (var gym in gyms.OrderBy(g => g.Name))
        {
            Gyms.Add(gym);
        }

        Divisions.Clear();
        Divisions.Add(new Division { Id = 0, Name = "Усі дивізіони" });
        foreach (var division in divisions.OrderBy(d => d.Id))
        {
            Divisions.Add(division);
        }

        _filtersLoaded = true;
    }
}
