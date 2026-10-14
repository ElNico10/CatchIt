using System.Security.Cryptography;
using CatchIt.Core.Data;

namespace CatchIt.Core.Auth;

/// <summary>
/// Simulated account confirmation. A 6-digit code is "delivered" through a hook the app
/// configures (the demo app shows it on screen as a pretend email), so no paid email
/// provider is needed. Codes are kept in memory only: they expire after 15 minutes and
/// do not survive restarting the app, in which case the user just requests a new one.
/// </summary>
/// <remarks>
/// Flow: <see cref="RequestCode"/> creates and delivers a code for the logged-in user, then
/// <see cref="Confirm"/> checks what the user typed. Both methods work on
/// <see cref="Session.CurrentUser"/>, so they fail with a message if nobody is logged in.
/// Because the codes are never written to the database, confirming needs no schema change.
/// </remarks>
public class ConfirmationService
{
    /// <summary>How many wrong guesses are allowed before the code is thrown away.</summary>
    public const int MaxWrongAttempts = 5;

    /// <summary>How long a code stays valid after it is issued.</summary>
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(15);

    // User-facing messages. Public so the UI and the tests can refer to them instead of copying the text.
    public const string NotLoggedInMessage = "Please log in to confirm your account";
    public const string AlreadyConfirmedMessage = "Your account is already confirmed";
    public const string NoCodeMessage = "Request a confirmation code first";
    public const string WrongCodeMessage = "That code is not correct";
    public const string ExpiredMessage = "That code has expired. Please request a new one";
    public const string TooManyAttemptsMessage = "Too many incorrect attempts. Please request a new code";

    /// <summary>A code that has been issued but not yet confirmed.</summary>
    /// <param name="Code">The 6-digit code, with leading zeros kept.</param>
    /// <param name="ExpiresAt">The moment after which the code is no longer accepted.</param>
    /// <param name="WrongAttempts">How many incorrect codes have been entered against it so far.</param>
    private record PendingCode(string Code, DateTime ExpiresAt, int WrongAttempts);

    private readonly Database _db;
    private readonly Session _session;
    private readonly Action<string, string> _deliver;
    private readonly Func<DateTime> _now;

    // At most one active code per user, keyed by user id. Requesting a new code overwrites the old one.
    private readonly Dictionary<long, PendingCode> _pending = new();

    /// <summary>Creates the service.</summary>
    /// <param name="db">Where the confirmed flag is saved.</param>
    /// <param name="session">Identifies the user being confirmed, and is updated once they are.</param>
    /// <param name="deliver">Called with (email, code) to "send" the code. Simulated in the demo.</param>
    /// <param name="now">Clock, overridable for tests. Defaults to the current UTC time.</param>
    public ConfirmationService(Database db, Session session, Action<string, string> deliver, Func<DateTime>? now = null)
    {
        _db = db;
        _session = session;
        _deliver = deliver;
        _now = now ?? (() => DateTime.UtcNow);
    }

    /// <summary>Creates a new code for the logged-in user and delivers it. Replaces any earlier code.</summary>
    /// <returns>Success with the current user, or a failure whose Error explains what went wrong.</returns>
    public AuthResult RequestCode()
    {
        var user = _session.CurrentUser;
        if (user == null)
            return AuthResult.Fail(NotLoggedInMessage);
        if (user.IsConfirmed)
            return AuthResult.Fail(AlreadyConfirmedMessage);

        // A cryptographically secure random number, padded so codes like 004821 keep six digits.
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        _pending[user.Id] = new PendingCode(code, _now() + CodeLifetime, 0);
        _deliver(user.Email, code);
        return AuthResult.Ok(user);
    }

    /// <summary>Checks the code and, if correct, marks the logged-in user's account as confirmed.</summary>
    /// <param name="code">The code the user typed. Surrounding whitespace is ignored.</param>
    /// <returns>
    /// Success with the now-confirmed user, or a failure whose Error is one of the
    /// <c>...Message</c> constants. A wrong code counts against <see cref="MaxWrongAttempts"/>.
    /// </returns>
    public AuthResult Confirm(string? code)
    {
        var user = _session.CurrentUser;
        if (user == null)
            return AuthResult.Fail(NotLoggedInMessage);
        if (user.IsConfirmed)
            return AuthResult.Fail(AlreadyConfirmedMessage);
        if (!_pending.TryGetValue(user.Id, out var pending))
            return AuthResult.Fail(NoCodeMessage);

        // Expired codes are discarded, so the user has to request a fresh one.
        if (_now() > pending.ExpiresAt)
        {
            _pending.Remove(user.Id);
            return AuthResult.Fail(ExpiredMessage);
        }

        if (!string.Equals(code?.Trim(), pending.Code, StringComparison.Ordinal))
        {
            // Limit guessing: a 6-digit code has only a million possibilities, so after a few
            // wrong tries the code is discarded and a new one must be requested.
            var attempts = pending.WrongAttempts + 1;
            if (attempts >= MaxWrongAttempts)
            {
                _pending.Remove(user.Id);
                return AuthResult.Fail(TooManyAttemptsMessage);
            }

            _pending[user.Id] = pending with { WrongAttempts = attempts };
            return AuthResult.Fail(WrongCodeMessage);
        }

        using (var conn = _db.Open())
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "UPDATE Users SET IsConfirmed = 1 WHERE Id = $id";
            cmd.Parameters.AddWithValue("$id", user.Id);
            cmd.ExecuteNonQuery();
        }

        // A code is single-use. The session holds an immutable User record, so replace it with a
        // confirmed copy; that is what makes the banner disappear without logging in again.
        _pending.Remove(user.Id);
        var confirmed = user with { IsConfirmed = true };
        _session.SignIn(confirmed);
        return AuthResult.Ok(confirmed);
    }
}
