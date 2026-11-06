using System.Security.Cryptography;
using System.Text;
using CatchIt.Core.Data;

namespace CatchIt.Core.Auth;

/// <summary>
/// Simulated "forgot password" flow (user story #4). A 6-digit reset code takes the place of a
/// reset link and is "delivered" through a hook the app configures (the demo app shows it on
/// screen as a pretend email), so no paid email provider is needed.
/// </summary>
/// <remarks>
/// Flow: <see cref="RequestReset"/> creates and delivers a code for an email address, then
/// <see cref="ResetPassword"/> checks the code and sets the new password. The user does not
/// need to be logged in for either step.
/// <para>
/// Unlike account confirmation, the code is stored in the database (<c>Users.ResetToken</c>),
/// so <see cref="UserService.ChangePassword"/> clearing that column also cancels a pending reset.
/// Only a SHA-256 hash of the code is stored, never the code itself.
/// </para>
/// </remarks>
public class PasswordResetService
{
    /// <summary>How many wrong codes are allowed before the code is thrown away.</summary>
    public const int MaxWrongAttempts = 5;

    /// <summary>How long a code stays valid after it is issued.</summary>
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(15);

    // User-facing messages. Public so the UI and the tests can refer to them instead of copying the text.
    public const string EmailNotFoundMessage = "Email not found";
    public const string CodeSentMessage = "A password reset code has been sent to your email";
    public const string NoCodeMessage = "Request a password reset code first";
    public const string WrongCodeMessage = "That code is not correct";
    public const string ExpiredMessage = "That code has expired. Please request a new one";
    public const string TooManyAttemptsMessage = "Too many incorrect attempts. Please request a new code";
    public const string PasswordsDoNotMatchMessage = "New passwords do not match";
    public const string PasswordResetMessage = "Your password has been reset. You can now log in with your new password.";

    private readonly Database _db;
    private readonly Action<string, string> _deliver;
    private readonly Func<DateTime> _now;

    /// <summary>Creates the service.</summary>
    /// <param name="db">Where the reset code and the new password are saved.</param>
    /// <param name="deliver">Called with (email, code) to "send" the code. Simulated in the demo.</param>
    /// <param name="now">Clock, overridable for tests. Defaults to the current UTC time.</param>
    public PasswordResetService(Database db, Action<string, string> deliver, Func<DateTime>? now = null)
    {
        _db = db;
        _deliver = deliver;
        _now = now ?? (() => DateTime.UtcNow);
    }

    /// <summary>
    /// Scenarios 1 and 2: creates a reset code for the account with this email and delivers it.
    /// Replaces any earlier code for that account.
    /// </summary>
    /// <param name="email">The address the user typed. Case and surrounding spaces are ignored.</param>
    /// <returns>Success with the account, or a failure whose Error explains what went wrong.</returns>
    public AuthResult RequestReset(string? email)
    {
        if (!Validation.IsValidEmail(email))
            return AuthResult.Fail(Validation.InvalidEmailMessage);

        var user = FindUser(email!.Trim());
        if (user == null)
            return AuthResult.Fail(EmailNotFoundMessage);

        // A cryptographically secure random number, padded so codes like 004821 keep six digits.
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

        using (var conn = _db.Open())
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "UPDATE Users SET ResetToken = $t, ResetTokenExpires = $x, ResetAttempts = 0 WHERE Id = $id";
            cmd.Parameters.AddWithValue("$t", Hash(code));
            cmd.Parameters.AddWithValue("$x", (_now() + CodeLifetime).ToString("O"));
            cmd.Parameters.AddWithValue("$id", user.Id);
            cmd.ExecuteNonQuery();
        }

        _deliver(user.Email, code);
        return AuthResult.Ok(user);
    }

    /// <summary>
    /// Scenario 3: checks the reset code and, if it is right, sets the new password.
    /// </summary>
    /// <param name="email">The account's email, as used in <see cref="RequestReset"/>.</param>
    /// <param name="code">The code the user typed. Surrounding whitespace is ignored.</param>
    /// <param name="newPassword">The new password; must meet the registration rules.</param>
    /// <param name="confirmNewPassword">The new password typed a second time.</param>
    /// <returns>
    /// Success with the account, or a failure whose Error is one of the <c>...Message</c> constants.
    /// A wrong code counts against <see cref="MaxWrongAttempts"/>. A password that breaks the rules
    /// does not, and leaves the code usable so the user can try another password.
    /// </returns>
    public AuthResult ResetPassword(string? email, string? code, string? newPassword, string? confirmNewPassword)
    {
        if (!Validation.IsValidEmail(email))
            return AuthResult.Fail(Validation.InvalidEmailMessage);

        var user = FindUser(email!.Trim());
        if (user == null)
            return AuthResult.Fail(EmailNotFoundMessage);

        using var conn = _db.Open();

        string? storedHash;
        string? expiresText;
        long attempts;
        using (var select = conn.CreateCommand())
        {
            select.CommandText = "SELECT ResetToken, ResetTokenExpires, ResetAttempts FROM Users WHERE Id = $id";
            select.Parameters.AddWithValue("$id", user.Id);
            using var reader = select.ExecuteReader();
            reader.Read();
            storedHash = reader.IsDBNull(0) ? null : reader.GetString(0);
            expiresText = reader.IsDBNull(1) ? null : reader.GetString(1);
            attempts = reader.GetInt64(2);
        }

        if (storedHash == null || expiresText == null)
            return AuthResult.Fail(NoCodeMessage);

        // Expired codes are discarded, so the user has to request a fresh one.
        var expiresAt = DateTime.Parse(expiresText, null, System.Globalization.DateTimeStyles.RoundtripKind);
        if (_now() > expiresAt)
        {
            ClearCode(conn, user.Id);
            return AuthResult.Fail(ExpiredMessage);
        }

        if (!Matches(code, storedHash))
        {
            // Limit guessing: a 6-digit code has only a million possibilities, so after a few
            // wrong tries the code is discarded and a new one must be requested.
            attempts++;
            if (attempts >= MaxWrongAttempts)
            {
                ClearCode(conn, user.Id);
                return AuthResult.Fail(TooManyAttemptsMessage);
            }

            using var count = conn.CreateCommand();
            count.CommandText = "UPDATE Users SET ResetAttempts = $a WHERE Id = $id";
            count.Parameters.AddWithValue("$a", attempts);
            count.Parameters.AddWithValue("$id", user.Id);
            count.ExecuteNonQuery();
            return AuthResult.Fail(WrongCodeMessage);
        }

        if (!Validation.IsValidPassword(newPassword))
            return AuthResult.Fail(Validation.PasswordRequirementsMessage);

        if (newPassword != confirmNewPassword)
            return AuthResult.Fail(PasswordsDoNotMatchMessage);

        var newHash = BCrypt.Net.BCrypt.HashPassword(newPassword!, Seeder.BcryptCost);
        using (var update = conn.CreateCommand())
        {
            // The code is single-use: it is cleared in the same statement that saves the password.
            update.CommandText =
                "UPDATE Users SET PasswordHash = $h, ResetToken = NULL, ResetTokenExpires = NULL, ResetAttempts = 0 " +
                "WHERE Id = $id";
            update.Parameters.AddWithValue("$h", newHash);
            update.Parameters.AddWithValue("$id", user.Id);
            update.ExecuteNonQuery();
        }

        return AuthResult.Ok(user);
    }

    /// <summary>Looks up an account by email (case-insensitive, as the Users table is). Null if none.</summary>
    private User? FindUser(string email)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Email, IsConfirmed FROM Users WHERE Email = $e";
        cmd.Parameters.AddWithValue("$e", email);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return null;
        return new User(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetInt64(3) == 1);
    }

    /// <summary>Removes any pending reset code from the account.</summary>
    private static void ClearCode(Microsoft.Data.Sqlite.SqliteConnection conn, long userId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Users SET ResetToken = NULL, ResetTokenExpires = NULL, ResetAttempts = 0 WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", userId);
        cmd.ExecuteNonQuery();
    }

    /// <summary>SHA-256 of the code as lowercase hex. This is what is stored instead of the code.</summary>
    internal static string Hash(string code) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(code)));

    /// <summary>Compares a typed code with the stored hash in constant time.</summary>
    private static bool Matches(string? typed, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(typed))
            return false;
        var typedHash = Encoding.ASCII.GetBytes(Hash(typed.Trim()));
        var stored = Encoding.ASCII.GetBytes(storedHash);
        return CryptographicOperations.FixedTimeEquals(typedHash, stored);
    }
}
