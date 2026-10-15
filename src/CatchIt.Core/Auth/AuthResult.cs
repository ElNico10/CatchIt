namespace CatchIt.Core.Auth;

/// <summary>
/// Outcome of a register or login attempt. On failure, Error holds the message to show the user.
/// </summary>
public class AuthResult
{
    public bool Success { get; }
    public string? Error { get; }
    public User? User { get; }

    private AuthResult(bool success, string? error, User? user)
    {
        Success = success;
        Error = error;
        User = user;
    }

    public static AuthResult Ok(User user) => new(true, null, user);
    public static AuthResult Fail(string error) => new(false, error, null);
}
