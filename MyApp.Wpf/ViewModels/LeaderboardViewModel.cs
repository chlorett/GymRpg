using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Wpf.Services;

namespace MyApp.Wpf.ViewModels;

public sealed class LeaderboardViewModel : ObservableObject
{
    private readonly ILeaderboardService _leaderboardService;
    private readonly CurrentUserSession _userSession;
    private readonly ILogger<LeaderboardViewModel> _logger;

    private string _searchQuery = string.Empty;
    private int? _currentUserRank;
    private int _currentUserLevel = 1;
    private long _currentUserXp;
    private int _totalParticipants;
    private string? _errorMessage;
    private bool _isBusy;

    public LeaderboardViewModel(
        ILeaderboardService leaderboardService,
        CurrentUserSession userSession,
        ILogger<LeaderboardViewModel> logger)
    {
        _leaderboardService = leaderboardService ?? throw new ArgumentNullException(nameof(leaderboardService));
        _userSession = userSession ?? throw new ArgumentNullException(nameof(userSession));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        LoadLeaderboardCommand = new AsyncRelayCommand(LoadLeaderboardAsync, () => !IsBusy);
        ClearSearchCommand = new AsyncRelayCommand(ClearSearchAsync, () => !IsBusy);
    }

    public ObservableCollection<LeaderboardEntryDto> Entries { get; } = new();

    public string SearchQuery
    {
        get => _searchQuery;
        set => SetProperty(ref _searchQuery, value);
    }

    public int? CurrentUserRank
    {
        get => _currentUserRank;
        set
        {
            if (SetProperty(ref _currentUserRank, value))
            {
                OnPropertyChanged(nameof(CurrentUserRankBadge));
                OnPropertyChanged(nameof(CurrentUserPositionText));
            }
        }
    }

    public int CurrentUserLevel
    {
        get => _currentUserLevel;
        set
        {
            if (SetProperty(ref _currentUserLevel, value))
            {
                OnPropertyChanged(nameof(CurrentUserSummaryText));
            }
        }
    }

    public long CurrentUserXp
    {
        get => _currentUserXp;
        set
        {
            if (SetProperty(ref _currentUserXp, value))
            {
                OnPropertyChanged(nameof(CurrentUserSummaryText));
            }
        }
    }

    public int TotalParticipants
    {
        get => _totalParticipants;
        set
        {
            if (SetProperty(ref _totalParticipants, value))
            {
                OnPropertyChanged(nameof(CurrentUserPositionText));
            }
        }
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
                LoadLeaderboardCommand.NotifyCanExecuteChanged();
                ClearSearchCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string CurrentUserRankBadge =>
        CurrentUserRank is { } rank ? $"#{rank}" : "—";

    public string CurrentUserPositionText =>
        CurrentUserRank is { } rank
            ? $"Ваше місце: #{rank} із {TotalParticipants} атлетів"
            : $"Атлет: {_userSession.UserName} (поза загальним рейтингом)";

    public string CurrentUserSummaryText =>
        $"{_userSession.UserName} • Рівень {CurrentUserLevel} • {CurrentUserXp} XP";

    public IAsyncRelayCommand LoadLeaderboardCommand { get; }

    public IAsyncRelayCommand ClearSearchCommand { get; }

    public async Task LoadLeaderboardAsync()
    {
        ErrorMessage = null;
        IsBusy = true;

        try
        {
            var dto = await _leaderboardService.GetLeaderboardAsync(
                _userSession.UserId,
                SearchQuery);

            CurrentUserRank = dto.CurrentUserRank;
            CurrentUserLevel = dto.CurrentUserLevel;
            CurrentUserXp = dto.CurrentUserXp;
            TotalParticipants = dto.TotalParticipants;

            Entries.Clear();
            foreach (var entry in dto.Entries)
            {
                Entries.Add(entry);
            }

            _logger.LogInformation(
                "Лідерборд відображено у WPF для {Username}: записів {Count}, позиція {Rank}",
                _userSession.UserName,
                dto.Entries.Count,
                dto.CurrentUserRank);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка завантаження таблиці лідерів");
            ErrorMessage = "Не вдалося завантажити рейтинг атлетів.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ClearSearchAsync()
    {
        SearchQuery = string.Empty;
        await LoadLeaderboardAsync();
    }
}
