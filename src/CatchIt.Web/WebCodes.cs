using CatchIt.Core.Auth;
using CatchIt.Core.Data;

namespace CatchIt.Web;

/// <summary>A "sent" email's code, shown on the page because there is no real mail server (like the desktop's simulated email window).</summary>
/// <param name="Email">The address the email was "sent" to.</param>
/// <param name="Code">The six-digit code in it.</param>
public record SimulatedEmail(string Email, string Code);

/// <summary>
/// Holds the confirmation and password-reset codes between web requests. The Core services keep their pending
/// codes in memory, so one instance of this class (a singleton) keeps one service per user alive for the whole run.
/// </summary>
/// <remarks>
/// Codes are lost when the server restarts, and the simulated email is shown to whoever asks for it, exactly as the
/// desktop app does. A real deployment would send the code by email instead.
/// </remarks>
public sealed class WebCodes(Database db)
{
    private readonly object _gate = new();
    private readonly Dictionary<long, ConfirmationService> _confirmations = new();
    private readonly Dictionary<long, SimulatedEmail> _confirmMail = new();
    private readonly Dictionary<string, SimulatedEmail> _resetMail = new(StringComparer.OrdinalIgnoreCase);
    private SimulatedEmail? _delivered;
    private PasswordResetService? _reset;

    private void Deliver(string email, string code) => _delivered = new SimulatedEmail(email, code);

    /// <summary>Creates a new confirmation code for the user and "sends" it.</summary>
    public AuthResult RequestConfirmation(User user)
    {
        lock (_gate)
        {
            var result = ServiceFor(user).RequestCode();
            if (result.Success)
                _confirmMail[user.Id] = _delivered!;
            return result;
        }
    }

    /// <summary>The email waiting for this user, or null when no code was requested (or it was used).</summary>
    public SimulatedEmail? PendingConfirmation(long userId)
    {
        lock (_gate) return _confirmMail.GetValueOrDefault(userId);
    }

    /// <summary>Checks a confirmation code. On success the returned user is the confirmed account.</summary>
    public AuthResult Confirm(User user, string? code)
    {
        lock (_gate)
        {
            var result = ServiceFor(user).Confirm(code);
            if (result.Success)
            {
                _confirmations.Remove(user.Id);
                _confirmMail.Remove(user.Id);
            }
            return result;
        }
    }

    // The service needs a Session of its own, signed in as the user, because it outlives the request's Session.
    private ConfirmationService ServiceFor(User user)
    {
        if (!_confirmations.TryGetValue(user.Id, out var service))
        {
            var session = new Session();
            session.SignIn(user);
            _confirmations[user.Id] = service = new ConfirmationService(db, session, Deliver);
        }
        return service;
    }

    /// <summary>Creates a password-reset code for the account with this email and "sends" it.</summary>
    public AuthResult RequestReset(string? email)
    {
        lock (_gate)
        {
            _reset ??= new PasswordResetService(db, Deliver);
            var result = _reset.RequestReset(email);
            if (result.Success)
                _resetMail[email!.Trim()] = _delivered!;
            return result;
        }
    }

    /// <summary>The reset email waiting for this address, or null.</summary>
    public SimulatedEmail? PendingReset(string? email)
    {
        lock (_gate) return email == null ? null : _resetMail.GetValueOrDefault(email.Trim());
    }

    /// <summary>Checks the reset code and sets the new password.</summary>
    public AuthResult ResetPassword(string? email, string? code, string? newPassword, string? confirmNewPassword)
    {
        lock (_gate)
        {
            _reset ??= new PasswordResetService(db, Deliver);
            var result = _reset.ResetPassword(email, code, newPassword, confirmNewPassword);
            if (result.Success && email != null)
                _resetMail.Remove(email.Trim());
            return result;
        }
    }
}
