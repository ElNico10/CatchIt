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

    public LoginForm(UserService users, Session session)
    {
        _users = users;
        _session = session;
        InitializeComponent();
    }

    private void btnLogin_Click(object? sender, EventArgs e)
    {
        var result = _users.Login(txtEmail.Text, txtPassword.Text);
        if (!result.Success)
        {
            lblError.Text = result.Error;
            txtPassword.Clear();
            txtPassword.Focus();
            return;
        }

        DialogResult = DialogResult.OK;
    }

    private void btnGuest_Click(object? sender, EventArgs e)
    {
        _session.EnterGuestMode();
        DialogResult = DialogResult.OK;
    }

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
