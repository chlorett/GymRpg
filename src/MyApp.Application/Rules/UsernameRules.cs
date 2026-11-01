namespace MyApp.Application.Rules;

public static class UsernameRules
{
    // Повертає текст помилки або null, якщо ім'я користувача коректне.
    public static string? GetError(string username)
    {
        if (username.Length is < AuthRules.MinUsernameLength or > AuthRules.MaxUsernameLength)
        {
            return $"Ім'я користувача має містити від {AuthRules.MinUsernameLength} до {AuthRules.MaxUsernameLength} символів.";
        }

        if (!username.All(IsUsernameChar))
        {
            return "Ім'я користувача може містити лише латинські літери, цифри та підкреслення.";
        }

        return null;
    }

    private static bool IsUsernameChar(char symbol)
    {
        return char.IsAsciiLetterOrDigit(symbol) || symbol == '_';
    }
}
