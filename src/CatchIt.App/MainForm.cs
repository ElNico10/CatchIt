using CatchIt.Core.Auth;

namespace CatchIt.App;

/// <summary>
/// Main window shell. The feature buttons are placeholders: each one asks
/// Access.Check whether the current session may use it, so guest and
/// logged-in behaviour can be tried out before the real screens exist.
/// </summary>
public partial class MainForm : Form
{
    private readonly UserService _users;
    private readonly Session _session;

    /// <summary>
    /// True when the form closed because the user logged out (or chose to log in from
    /// guest mode), meaning the app should show the login form again instead of exiting.
    /// </summary>
    public bool ReturnToLogin { get; private set; }

    public MainForm(UserService users, Session session)
    {
        _users = users;
        _session = session;
        InitializeComponent();
        ShowSessionState();
    }

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
            lblUser.Text = "Browsing as a guest";
            btnLogout.Text = "Log in";
            lblGuestNote.Visible = true;
        }
    }

    private void btnLogout_Click(object? sender, EventArgs e)
    {
        _users.Logout();
        ReturnToLogin = true;
        Close();
    }

    private void btnSearch_Click(object? sender, EventArgs e) => OpenFeature(Feature.Search, "Product search");
    private void btnWatchlist_Click(object? sender, EventArgs e) => OpenFeature(Feature.Watchlist, "Watchlist");
    private void btnDashboard_Click(object? sender, EventArgs e) => OpenFeature(Feature.Dashboard, "Dashboard");
    private void btnProfile_Click(object? sender, EventArgs e) => OpenFeature(Feature.Profile, "Profile");

    private void OpenFeature(Feature feature, string name)
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

        // Real screens are not built yet.
        MessageBox.Show(this, $"{name} is coming soon.", name, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
