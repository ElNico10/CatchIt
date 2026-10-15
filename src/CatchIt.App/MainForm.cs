using CatchIt.Core.Auth;
using CatchIt.Core.Search;

namespace CatchIt.App;

/// <summary>
/// Main window hub. Each feature button asks Access.Check whether the current session may
/// use it, then opens that feature's screen in its own window. Search, watchlist and
/// dashboard are still placeholders; the profile screen already works.
/// </summary>
/// <remarks>
/// The window has three stacked panels: the top bar (who is logged in and the Logout / Log in
/// button), the confirmation banner (shown only for unconfirmed accounts), and the content area
/// with the feature buttons. The content panel fills whatever space the banner leaves, so the
/// buttons move down when the banner appears.
/// </remarks>
public partial class MainForm : Form
{
    private readonly UserService _users;
    private readonly Session _session;
    private readonly ConfirmationService _confirmation;
    private readonly ProfileService _profile;
    private readonly ListingSearchService _listingSearch;

    /// <summary>
    /// True when the form closed because the user logged out (or chose to log in from
    /// guest mode), meaning the app should show the login form again instead of exiting.
    /// </summary>
    public bool ReturnToLogin { get; private set; }

    /// <summary>Creates the main window for the current session.</summary>
    /// <param name="users">Used to log the user out.</param>
    /// <param name="session">Decides what the window shows and which features are allowed.</param>
    /// <param name="confirmation">Used by the banner to request and check confirmation codes.</param>
    /// <param name="profile">Handed to the profile screen so it can save name and email changes.</param>
    /// <param name="listingSearch">Handed to the search screen so it can find products and their prices.</param>
    public MainForm(UserService users, Session session, ConfirmationService confirmation, ProfileService profile,
        ListingSearchService listingSearch)
    {
        _listingSearch = listingSearch;
        _users = users;
        _session = session;
        _confirmation = confirmation;
        _profile = profile;
        InitializeComponent();
        ShowSessionState();
    }

    /// <summary>
    /// Updates the top bar, the Logout / Log in button, the guest note and the confirmation
    /// banner to match the current session. Call it again after the session changes.
    /// </summary>
    private void ShowSessionState()
    {
        if (_session.IsLoggedIn)
        {
            var user = _session.CurrentUser!;
            lblUser.Text = string.IsNullOrWhiteSpace(user.Name)
                ? $"Logged in as {user.Email}"
                : $"Logged in as {user.Name} ({user.Email})";
            btnLogout.Text = "Logout";
            lblGuestNote.Visible = false;
        }
        else
        {
            // A guest has no account to log out of, so the same button offers to log in instead.
            lblUser.Text = "Browsing as a guest";
            btnLogout.Text = "Log in";
            lblGuestNote.Visible = true;
        }

        // Unconfirmed accounts see a banner but can keep using the app.
        pnlBanner.Visible = _session.NeedsConfirmation;
    }

    /// <summary>
    /// Banner button: sends a confirmation code, then asks the user to enter it.
    /// </summary>
    private void btnConfirm_Click(object? sender, EventArgs e)
    {
        // Requesting the code also "delivers" it, which shows the simulated email window.
        var request = _confirmation.RequestCode();
        if (!request.Success)
        {
            // For example, the account was confirmed in the meantime; refresh so the banner goes away.
            MessageBox.Show(this, request.Error, "Confirm account", MessageBoxButtons.OK, MessageBoxIcon.Information);
            ShowSessionState();
            return;
        }

        using var confirm = new ConfirmForm(_confirmation, _session.CurrentUser!.Email);
        if (confirm.ShowDialog(this) == DialogResult.OK)
        {
            // The session now holds the confirmed user, so this hides the banner.
            ShowSessionState();
            MessageBox.Show(this, "Your account is confirmed.", "Confirm account",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    /// <summary>
    /// Top-bar button. Logs a registered user out, or sends a guest to the login form,
    /// by ending the session and closing this window so the login form can reopen.
    /// </summary>
    private void btnLogout_Click(object? sender, EventArgs e)
    {
        _users.Logout();
        ReturnToLogin = true;
        Close();
    }

    // The four feature buttons all go through OpenFeature so the access rules live in one place.
    private void btnSearch_Click(object? sender, EventArgs e) =>
        OpenFeature(Feature.Search, "Product search", () => new SearchForm(_listingSearch));
    private void btnWatchlist_Click(object? sender, EventArgs e) =>
        OpenFeature(Feature.Watchlist, "Watchlist", () => new WatchlistForm());
    private void btnDashboard_Click(object? sender, EventArgs e) =>
        OpenFeature(Feature.Dashboard, "Dashboard", () => new DashboardForm());
    private void btnProfile_Click(object? sender, EventArgs e) =>
        OpenFeature(Feature.Profile, "Profile", () => new ProfileForm(_profile, _session));

    /// <summary>
    /// Opens a feature's screen if the current session may use it; otherwise explains why not
    /// and, if the user agrees, returns to the login form.
    /// </summary>
    /// <param name="feature">The feature being opened, checked against <see cref="Access"/>.</param>
    /// <param name="name">Display name used in the message box titles.</param>
    /// <param name="createScreen">
    /// Creates the screen. Only called once access is allowed, so a screen that assumes a
    /// logged-in user (like the profile) is never built for a guest.
    /// </param>
    private void OpenFeature(Feature feature, string name, Func<Form> createScreen)
    {
        var access = Access.Check(_session, feature);

        if (!access.Allowed)
        {
            // Guests get an offer to log in; the session is ended so the login form starts clean.
            var answer = MessageBox.Show(this,
                $"{access.Reason}\n\nWould you like to log in now?",
                name, MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (answer == DialogResult.Yes)
            {
                _users.Logout();
                ReturnToLogin = true;
                Close();
            }
            return;
        }

        using (var screen = createScreen())
            screen.ShowDialog(this);

        // A screen may have changed the session (the profile screen updates the name and email),
        // so refresh the top bar once it closes.
        ShowSessionState();
    }
}
