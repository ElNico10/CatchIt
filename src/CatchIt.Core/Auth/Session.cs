namespace CatchIt.Core.Auth;

/// <summary>
/// Tracks who is using the app: nobody yet (signed out), a guest, or a logged-in user.
/// Guests get the basic features; saving things (watchlist, profile) needs an account.
/// </summary>
public class Session
{
    public User? CurrentUser { get; private set; }
    public bool IsGuest { get; private set; }
    public bool IsLoggedIn => CurrentUser != null;

    /// <summary>True once the user has either logged in or chosen to continue as a guest.</summary>
    public bool IsActive => IsLoggedIn || IsGuest;

    public void EnterGuestMode()
    {
        CurrentUser = null;
        IsGuest = true;
    }

    /// <summary>Logging in replaces a guest session.</summary>
    public void SignIn(User user)
    {
        CurrentUser = user;
        IsGuest = false;
    }

    /// <summary>Ends the session completely; the user returns to the signed-out state.</summary>
    public void SignOut()
    {
        CurrentUser = null;
        IsGuest = false;
    }
}
