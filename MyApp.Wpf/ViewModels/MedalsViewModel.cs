using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Wpf.Services;

namespace MyApp.Wpf.ViewModels;

public sealed class MedalsViewModel : ObservableObject
{
    private readonly IMedalService _medalService;
    private readonly CurrentUserSession _userSession;
    private readonly ILogger<MedalsViewModel> _logger;

    private string _selectedStatusFilter = "Усі";
    private int _totalMedalsCount;
    private int _earnedMedalsCount;
    private int _availableToClaimCount;
    private int _completionPercent;
    private string? _statusMessage;
    private string? _errorMessage;
    private bool _isBusy;

    public MedalsViewModel(
        IMedalService medalService,
        CurrentUserSession userSession,
        ILogger<MedalsViewModel> logger)
    {
        _medalService = medalService ?? throw new ArgumentNullException(nameof(medalService));
        _userSession = userSession ?? throw new ArgumentNullException(nameof(userSession));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        LoadMedalsCommand = new AsyncRelayCommand(LoadMedalsAsync, () => !IsBusy);
        ClaimMedalsCommand = new AsyncRelayCommand(ClaimMedalsAsync, () => !IsBusy);
    }

    public ObservableCollection<MedalItemDto> Medals { get; } = new();

    public IReadOnlyList<string> StatusFilterOptions { get; } = new[]
    {
        "Усі",
        "Здобуті",
        "Заблоковані",
    };

    public string SelectedStatusFilter
    {
        get => _selectedStatusFilter;
        set
        {
            if (SetProperty(ref _selectedStatusFilter, value))
            {
                _ = LoadMedalsAsync();
            }
        }
    }

    public int TotalMedalsCount
    {
        get => _totalMedalsCount;
        set
        {
            if (SetProperty(ref _totalMedalsCount, value))
            {
                OnPropertyChanged(nameof(EarnedCountBadge));
                OnPropertyChanged(nameof(SummaryHeaderText));
            }
        }
    }

    public int EarnedMedalsCount
    {
        get => _earnedMedalsCount;
        set
        {
            if (SetProperty(ref _earnedMedalsCount, value))
            {
                OnPropertyChanged(nameof(EarnedCountBadge));
                OnPropertyChanged(nameof(SummaryHeaderText));
            }
        }
    }

    public int AvailableToClaimCount
    {
        get => _availableToClaimCount;
        set
        {
            if (SetProperty(ref _availableToClaimCount, value))
            {
                OnPropertyChanged(nameof(SummaryDetailsText));
            }
        }
    }

    public int CompletionPercent
    {
        get => _completionPercent;
        set
        {
            if (SetProperty(ref _completionPercent, value))
            {
                OnPropertyChanged(nameof(SummaryHeaderText));
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
                LoadMedalsCommand.NotifyCanExecuteChanged();
                ClaimMedalsCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string EarnedCountBadge => $"{EarnedMedalsCount} / {TotalMedalsCount}";

    public string SummaryHeaderText =>
        $"Здобуто: {EarnedMedalsCount} із {TotalMedalsCount} нагород ({CompletionPercent}%)";

    public string SummaryDetailsText =>
        AvailableToClaimCount > 0
            ? $"Атлет: {_userSession.UserName} • Доступно до отримання: {AvailableToClaimCount} нагород(и)!"
            : $"Атлет: {_userSession.UserName} • Тренуйтеся та набирайте XP для нових досягнень";

    public IAsyncRelayCommand LoadMedalsCommand { get; }

    public IAsyncRelayCommand ClaimMedalsCommand { get; }

    public async Task LoadMedalsAsync()
    {
        ErrorMessage = null;
        IsBusy = true;

        try
        {
            var overview = await _medalService.GetUserMedalsOverviewAsync(
                _userSession.UserId,
                SelectedStatusFilter);

            TotalMedalsCount = overview.TotalMedalsCount;
            EarnedMedalsCount = overview.EarnedMedalsCount;
            AvailableToClaimCount = overview.AvailableToClaimCount;
            CompletionPercent = overview.CompletionPercent;

            Medals.Clear();
            foreach (var item in overview.Medals)
            {
                Medals.Add(item);
            }

            _logger.LogInformation(
                "Вітрину медалей оновлено у WPF для {Username}: здобуто {Earned}/{Total}, показано {Shown}",
                _userSession.UserName,
                overview.EarnedMedalsCount,
                overview.TotalMedalsCount,
                overview.Medals.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка завантаження вітрини медалей");
            ErrorMessage = "Не вдалося завантажити медалі та досягнення.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ClaimMedalsAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;
        IsBusy = true;

        try
        {
            var claimResult = await _medalService.ClaimEligibleMedalsAsync(_userSession.UserId);
            if (!claimResult.Result.IsSuccess)
            {
                _logger.LogWarning(
                    "Помилка валідації при отриманні медалей для UserId={UserId}: [{ErrorCode}] Field={Field} Message={Message}",
                    _userSession.UserId,
                    claimResult.Result.ErrorCode,
                    claimResult.Result.Field,
                    claimResult.Result.Message);
                ErrorMessage = claimResult.Result.Message ?? "Не вдалося отримати нагороди.";
                return;
            }

            if (claimResult.NewlyClaimedCount > 0)
            {
                StatusMessage = $"Вітаємо! Ви отримали нових медалей ({claimResult.NewlyClaimedCount}): {string.Join(", ", claimResult.NewlyClaimedNames)}.";
            }
            else
            {
                StatusMessage = "Усі виконані нагороди вже отримані. Продовжуйте тренуватися!";
            }

            await LoadMedalsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка під час отримання медалей");
            ErrorMessage = "Сталася помилка під час перевірки нагород.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
