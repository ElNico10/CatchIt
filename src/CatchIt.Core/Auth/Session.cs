namespace CatchIt.Core.Auth;

/// <summary>
/// Tracks who is logged in. The UI should check IsLoggedIn before showing the
/// dashboard or watchlist, and send the user to the login form when it is false.
/// </summary>
public class Session
{
    public User? CurrentUser { get; private set; }
    public bool IsLoggedIn => CurrentUser != null;

    public void SignIn(User user) => CurrentUser = user;
    public void SignOut() => CurrentUser = null;
}
