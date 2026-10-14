using CatchIt.Core.Auth;

namespace CatchIt.App;

/// <summary>
/// Creates an account. Closes with DialogResult.OK after a successful registration;
/// the new account is not logged in, the user returns to the login form.
/// </summary>
public partial class RegisterForm : Form
{
    private readonly UserService _users;

    /// <summary>The email of the account that was just created; empty until registration succeeds.</summary>
    public string RegisteredEmail { get; private set; } = string.Empty;

    /// <summary>Creates the registration form.</summary>
    /// <param name="users">Validates the details and creates the account.</param>
    public RegisterForm(UserService users)
    {
        _users = users;
        InitializeComponent();
    }

    /// <summary>
    /// Register button (also triggered by Enter). Validation messages come straight from
    /// <see cref="UserService.Register"/> so the form and the logic never disagree.
    /// </summary>
    private void btnRegister_Click(object? sender, EventArgs e)
    {
        var result = _users.Register(txtEmail.Text, txtPassword.Text, txtName.Text);
        if (!result.Success)
        {
            lblError.Text = result.Error;
            return;
        }

        RegisteredEmail = result.User!.Email;
        MessageBox.Show(this, UserService.ConfirmationMessage, "Account created",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
        DialogResult = DialogResult.OK;
    }
}
