using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyApp.Application.Dtos;
using MyApp.Application.Interfaces;
using MyApp.Wpf.Services;

namespace MyApp.Wpf.ViewModels;

public sealed partial class ProfileViewModel : ObservableObject
{
    private readonly IUserProfileService _profiles;

    private readonly CurrentUserSession _userSession;

    [ObservableProperty]
    private string _weightText = string.Empty;

    [ObservableProperty]
    private string _heightText = string.Empty;

    [ObservableProperty]
    private DateTime? _birthDate;

    [ObservableProperty]
    private string? _weightError;

    [ObservableProperty]
    private string? _heightError;

    [ObservableProperty]
    private string? _birthDateError;

    [ObservableProperty]
    private string? _generalError;

    [ObservableProperty]
    private string? _successMessage;

    public ProfileViewModel(IUserProfileService profiles, CurrentUserSession userSession)
    {
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        _userSession = userSession ?? throw new ArgumentNullException(nameof(userSession));
    }

    public async Task InitializeAsync()
    {
        var profile = await _profiles.GetAsync(_userSession.UserId);
        if (profile is null)
        {
            GeneralError = "Не вдалося завантажити профіль.";
            return;
        }

        WeightText = profile.WeightKg?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
        HeightText = profile.HeightCm?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
        BirthDate = profile.BirthDate?.ToDateTime(TimeOnly.MinValue);
    }

    private static bool TryParseDecimal(string text, out decimal? value)
    {
        value = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var normalized = text.Trim().Replace('.', ',');
        if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.GetCultureInfo("uk-UA"), out var parsed))
        {
            return false;
        }

        value = parsed;
        return true;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ClearMessages();

        if (!TryParseDecimal(WeightText, out var weight))
        {
            WeightError = "Введіть число.";
            return;
        }

        if (!TryParseDecimal(HeightText, out var height))
        {
            HeightError = "Введіть число.";
            return;
        }

        DateOnly? birth = BirthDate is { } date ? DateOnly.FromDateTime(date) : null;

        var result = await _profiles.UpdateAsync(
            new UpdateProfileRequest(_userSession.UserId, weight, height, birth));

        if (result.IsSuccess)
        {
            SuccessMessage = "Профіль збережено.";
            return;
        }

        ShowError(result);
    }

    private void ClearMessages()
    {
        WeightError = null;
        HeightError = null;
        BirthDateError = null;
        GeneralError = null;
        SuccessMessage = null;
    }

    private void ShowError(OperationResult result)
    {
        var message = result.Message ?? "Не вдалося зберегти профіль.";

        switch (result.Field)
        {
            case nameof(UpdateProfileRequest.WeightKg):
                WeightError = message;
                break;
            case nameof(UpdateProfileRequest.HeightCm):
                HeightError = message;
                break;
            case nameof(UpdateProfileRequest.BirthDate):
                BirthDateError = message;
                break;
            default:
                GeneralError = message;
                break;
        }
    }
}