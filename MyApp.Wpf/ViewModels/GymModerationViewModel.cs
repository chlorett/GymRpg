using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Application.Services;
using MyApp.Domain.Entities;

namespace MyApp.Wpf.ViewModels;

public class GymModerationViewModel : INotifyPropertyChanged
{
    private readonly IGymModerationService _moderationService;
    private readonly int _currentModeratorId;

    private int? _selectedGymId;
    private int? _selectedDivisionId;
    private string _searchQuery = string.Empty;
    private GymRosterMemberDto? _selectedMember;
    private int _totalAthletes;
    private int _activeInLeaderboard;
    private int _excludedFromLeaderboard;
    private string _statusMessage = string.Empty;

    public GymModerationViewModel(IGymModerationService moderationService, int currentModeratorId)
    {
        _moderationService = moderationService;
        _currentModeratorId = currentModeratorId;

        RefreshCommand = new AsyncRelayCommand(LoadRosterAsync);
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
                _ = LoadRosterAsync();
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
                _ = LoadRosterAsync();
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
                _ = LoadRosterAsync();
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
        : "Виключити з лідерборду";

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

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public async Task LoadRosterAsync()
    {
        var result = await _moderationService.GetGymRosterAsync(SelectedGymId, SelectedDivisionId, SearchQuery);
        if (result == null)
        {
            StatusMessage = "Помилка завантаження списку учасників.";
            return;
        }

        TotalAthletes = result.TotalAthletes;
        ActiveInLeaderboard = result.ActiveInLeaderboard;
        ExcludedFromLeaderboard = result.ExcludedFromLeaderboard;

        Members.Clear();
        foreach (var member in result.Members)
        {
            Members.Add(member);
        }

        StatusMessage = $"Завантажено учасників: {Members.Count}";
    }

    public async Task ToggleLeaderboardExclusionAsync()
    {
        if (SelectedMember == null)
        {
            return;
        }

        var targetId = SelectedMember.UserId;
        var request = new UpdateAthleteModerationRequest
        {
            ModeratorId = _currentModeratorId,
            TargetUserId = targetId,
            IsExcludedFromLeaderboard = !SelectedMember.IsExcludedFromLeaderboard,
            IsBlocked = SelectedMember.IsBlocked
        };

        var success = await _moderationService.UpdateModerationStatusAsync(request);
        if (!success)
        {
            StatusMessage = "Не вдалося оновити статус у лідерборді.";
            return;
        }

        StatusMessage = "Статус участі в лідерборді успішно оновлено.";
        await LoadRosterAsync();
    }

    public async Task ToggleBlockStatusAsync()
    {
        if (SelectedMember == null)
        {
            return;
        }

        var targetId = SelectedMember.UserId;
        var newBlockState = !SelectedMember.IsBlocked;
        var request = new UpdateAthleteModerationRequest
        {
            ModeratorId = _currentModeratorId,
            TargetUserId = targetId,
            IsExcludedFromLeaderboard = newBlockState || SelectedMember.IsExcludedFromLeaderboard,
            IsBlocked = newBlockState
        };

        var success = await _moderationService.UpdateModerationStatusAsync(request);
        if (!success)
        {
            StatusMessage = "Не вдалося змінити статус блокування.";
            return;
        }

        StatusMessage = newBlockState ? "Акаунт заблоковано." : "Акаунт розблоковано.";
        await LoadRosterAsync();
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}