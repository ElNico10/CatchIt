using CatchIt.Core.Auth;

namespace CatchIt.App;

/// <summary>
/// Creates an account. Closes with DialogResult.OK after a successful registration;
/// the new account is not logged in, the user returns to the login form.
/// </summary>
public partial class RegisterForm : Form
{
    private readonly UserService _users;

    public string RegisteredEmail { get; private set; } = string.Empty;

    public RegisterForm(UserService users)
    {
        _users = users;
        InitializeComponent();
    }

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
