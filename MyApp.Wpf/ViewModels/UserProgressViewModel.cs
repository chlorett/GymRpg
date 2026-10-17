using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Wpf.Services;

namespace MyApp.Wpf.ViewModels;

public sealed class UserProgressViewModel : ObservableObject
{
    private readonly IUserProgressService _progressService;
    private readonly CurrentUserSession _userSession;
    private readonly ILogger<UserProgressViewModel> _logger;

    private string _username = "athlete";
    private int _level = 1;
    private long _totalXp;
    private long _xpForCurrentLevel;
    private long _xpForNextLevel = 1000;
    private int _workoutsCount;
    private decimal _totalVolumeKg;
    private string? _errorMessage;
    private bool _isBusy;

    public UserProgressViewModel(
        IUserProgressService progressService,
        CurrentUserSession userSession,
        ILogger<UserProgressViewModel> logger)
    {
        _progressService = progressService ?? throw new ArgumentNullException(nameof(progressService));
        _userSession = userSession ?? throw new ArgumentNullException(nameof(userSession));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        LoadProgressCommand = new AsyncRelayCommand(LoadProgressAsync, () => !IsBusy);
    }

    public ObservableCollection<WorkoutHistoryItemDto> RecentWorkouts { get; } = new();

    public string Username
    {
        get => _username;
        set
        {
            if (SetProperty(ref _username, value))
            {
                OnPropertyChanged(nameof(AthleteHeaderText));
            }
        }
    }

    public int Level
    {
        get => _level;
        set
        {
            if (SetProperty(ref _level, value))
            {
                OnPropertyChanged(nameof(LevelBadgeText));
                OnPropertyChanged(nameof(RankTitle));
            }
        }
    }

    public long TotalXp
    {
        get => _totalXp;
        set => SetProperty(ref _totalXp, value);
    }

    public long XpForCurrentLevel
    {
        get => _xpForCurrentLevel;
        set
        {
            if (SetProperty(ref _xpForCurrentLevel, value))
            {
                OnPropertyChanged(nameof(ProgressPercentage));
                OnPropertyChanged(nameof(XpBarLabel));
            }
        }
    }

    public long XpForNextLevel
    {
        get => _xpForNextLevel;
        set
        {
            if (SetProperty(ref _xpForNextLevel, value))
            {
                OnPropertyChanged(nameof(ProgressPercentage));
                OnPropertyChanged(nameof(XpBarLabel));
            }
        }
    }

    public int WorkoutsCount
    {
        get => _workoutsCount;
        set => SetProperty(ref _workoutsCount, value);
    }

    public decimal TotalVolumeKg
    {
        get => _totalVolumeKg;
        set => SetProperty(ref _totalVolumeKg, value);
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
                LoadProgressCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string AthleteHeaderText => $"Атлет: {Username} (ID: {_userSession.UserId})";

    public string LevelBadgeText => $"РІВЕНЬ {Level}";

    public string RankTitle => Level switch
    {
        <= 1 => "Ранг: Новачок залу",
        <= 3 => "Ранг: Залізний аматор",
        <= 5 => "Ранг: Досвідчений атлет",
        <= 10 => "Ранг: Ветеран штанги",
        _ => "Ранг: Легенда PulseForge",
    };

    public double ProgressPercentage =>
        XpForNextLevel > 0 ? Math.Clamp((double)XpForCurrentLevel / XpForNextLevel * 100.0, 0.0, 100.0) : 0.0;

    public string XpBarLabel =>
        $"{XpForCurrentLevel} / {XpForNextLevel} XP до наступного рівня ({ProgressPercentage:F0}%)";

    public IAsyncRelayCommand LoadProgressCommand { get; }

    public async Task LoadProgressAsync()
    {
        ErrorMessage = null;
        IsBusy = true;

        try
        {
            var dto = await _progressService.GetProgressAsync(_userSession.UserId);
            if (dto is null)
            {
                ErrorMessage = "Не вдалося знайти профіль прогресу для поточного користувача.";
                _logger.LogWarning("Профіль прогресу не знайдено для користувача {UserId}", _userSession.UserId);
                return;
            }

            Username = dto.Username;
            Level = dto.Level;
            TotalXp = dto.TotalXp;
            XpForCurrentLevel = dto.XpForCurrentLevel;
            XpForNextLevel = dto.XpForNextLevel;
            WorkoutsCount = dto.WorkoutsCount;
            TotalVolumeKg = dto.RecentWorkouts.Sum(w => w.TotalWeightKg);

            RecentWorkouts.Clear();
            foreach (var item in dto.RecentWorkouts)
            {
                RecentWorkouts.Add(item);
            }

            _logger.LogInformation(
                "RPG-прогрес успішно відображено у WPF для користувача {Username} (Рівень {Level}, XP {TotalXp})",
                dto.Username,
                dto.Level,
                dto.TotalXp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка завантаження RPG-прогресу атлета");
            ErrorMessage = $"Помилка завантаження прогресу: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
