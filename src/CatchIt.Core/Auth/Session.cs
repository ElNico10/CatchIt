namespace CatchIt.Core.Auth;

/// <summary>
/// Tracks who is using the app: nobody yet (signed out), a guest, or a logged-in user.
/// Guests get the basic features; saving things (watchlist, profile) needs an account.
/// </summary>
/// <remarks>
/// The app creates one <see cref="Session"/> at startup and shares it between
/// <see cref="UserService"/>, <see cref="ConfirmationService"/> and the forms. The state lives
/// in memory only, so closing the app always ends the session.
/// </remarks>
public class Session
{
    /// <summary>The logged-in user, or <c>null</c> when signed out or browsing as a guest.</summary>
    public User? CurrentUser { get; private set; }

    /// <summary>True while the user is browsing without an account.</summary>
    public bool IsGuest { get; private set; }

    /// <summary>True when a registered user is logged in.</summary>
    public bool IsLoggedIn => CurrentUser != null;

    /// <summary>True for a logged-in user whose account is not confirmed yet (the UI shows a banner).</summary>
    public bool NeedsConfirmation => CurrentUser is { IsConfirmed: false };

    /// <summary>True once the user has either logged in or chosen to continue as a guest.</summary>
    public bool IsActive => IsLoggedIn || IsGuest;

    /// <summary>Starts a guest session: basic features only, nothing is saved.</summary>
    public void EnterGuestMode()
    {
        CurrentUser = null;
        IsGuest = true;
    }

    /// <summary>Logging in replaces a guest session.</summary>
    /// <param name="user">The user who has just been authenticated.</param>
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
