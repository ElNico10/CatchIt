using System.Text.RegularExpressions;

namespace CatchIt.Core.Auth;

public static class Validation
{
    public const int MinPasswordLength = 8;

    public const string InvalidEmailMessage = "Please enter a valid email address";
    public const string PasswordRequirementsMessage =
        "Password must be at least 8 characters and include at least one letter and one number";

    private static readonly Regex EmailPattern =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    public static bool IsValidEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email) && EmailPattern.IsMatch(email.Trim());

    public static bool IsValidPassword(string? password) =>
        password != null
        && password.Length >= MinPasswordLength
        && password.Any(char.IsLetter)
        && password.Any(char.IsDigit);
}
