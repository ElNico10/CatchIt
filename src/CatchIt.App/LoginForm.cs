using CatchIt.Core.Auth;

namespace CatchIt.App;

/// <summary>
/// First screen. Closes with DialogResult.OK once the user has logged in or
/// chosen to continue as a guest; any other result means the app should exit.
/// </summary>
public partial class LoginForm : Form
{
    private readonly UserService _users;
    private readonly Session _session;

    /// <summary>Creates the login form.</summary>
    /// <param name="users">Checks the entered credentials.</param>
    /// <param name="session">Switched into guest mode when the user skips signing in.</param>
    public LoginForm(UserService users, Session session)
    {
        _users = users;
        _session = session;
        InitializeComponent();
    }

    /// <summary>
    /// Login button (also triggered by Enter). On success the session is already signed in
    /// by <see cref="UserService.Login"/>, so the form only has to close.
    /// </summary>
    private void btnLogin_Click(object? sender, EventArgs e)
    {
        var result = _users.Login(txtEmail.Text, txtPassword.Text);
        if (!result.Success)
        {
            // Keep the email so the user can retry, but never leave a failed password sitting in the box.
            lblError.Text = result.Error;
            txtPassword.Clear();
            txtPassword.Focus();
            return;
        }

        DialogResult = DialogResult.OK;
    }

    /// <summary>Skips signing in. The user gets the basic features but nothing is saved.</summary>
    private void btnGuest_Click(object? sender, EventArgs e)
    {
        _session.EnterGuestMode();
        DialogResult = DialogResult.OK;
    }

    /// <summary>
    /// Opens the registration form. Registering does not log the user in; the form returns
    /// to the login screen with the new email filled in.
    /// </summary>
    private void lnkRegister_LinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
    {
        using var register = new RegisterForm(_users);
        if (register.ShowDialog(this) == DialogResult.OK)
        {
            // Account created: bring the email over so the user only has to type the password.
            txtEmail.Text = register.RegisteredEmail;
            txtPassword.Clear();
            lblError.Text = string.Empty;
            txtPassword.Focus();
        }
    }
}
