using CatchIt.Core.Auth;

namespace CatchIt.App;

/// <summary>
/// "Forgot password" screen (user story #4). Step 1: the user enters their email and clicks
/// Reset Password, which shows the code in a simulated email. Step 2: the user enters the code
/// and a new password. Closes with DialogResult.OK once the password has been reset.
/// </summary>
public partial class ForgotPasswordForm : Form
{
    private readonly PasswordResetService _reset;

    /// <summary>The email whose password was reset; empty until the reset succeeds.</summary>
    public string ResetEmail { get; private set; } = string.Empty;

    /// <summary>Creates the form.</summary>
    /// <param name="reset">Creates, delivers and checks reset codes.</param>
    /// <param name="email">Pre-filled email, e.g. whatever was typed on the login screen.</param>
    public ForgotPasswordForm(PasswordResetService reset, string email = "")
    {
        _reset = reset;
        InitializeComponent();
        txtEmail.Text = email.Trim();
        ShowStep2(false);
    }

    /// <summary>Shows or hides the code and new-password fields.</summary>
    private void ShowStep2(bool visible)
    {
        foreach (Control c in new Control[] { lblCode, txtCode, lblNewPassword, txtNewPassword,
                                              lblConfirm, txtConfirm, btnSetPassword })
            c.Visible = visible;

        // Enter triggers whichever step the user is on.
        AcceptButton = visible ? btnSetPassword : btnRequest;
    }

    /// <summary>Reset Password button: creates a code and "emails" it (scenarios 1 and 2).</summary>
    private void btnRequest_Click(object? sender, EventArgs e)
    {
        var result = _reset.RequestReset(txtEmail.Text);
        if (!result.Success)
        {
            lblError.ForeColor = Color.Firebrick;
            lblError.Text = result.Error;
            ShowStep2(false);
            return;
        }

        lblError.ForeColor = Color.SeaGreen;
        lblError.Text = PasswordResetService.CodeSentMessage;
        btnRequest.Text = "Send a new code";
        ShowStep2(true);
        txtCode.Clear();
        txtCode.Focus();
    }

    /// <summary>Set New Password button: checks the code and saves the new password (scenario 3).</summary>
    private void btnSetPassword_Click(object? sender, EventArgs e)
    {
        var result = _reset.ResetPassword(txtEmail.Text, txtCode.Text, txtNewPassword.Text, txtConfirm.Text);
        if (!result.Success)
        {
            lblError.ForeColor = Color.Firebrick;
            lblError.Text = result.Error;
            // Never leave rejected passwords sitting in the boxes.
            txtNewPassword.Clear();
            txtConfirm.Clear();
            return;
        }

        ResetEmail = result.User!.Email;
        MessageBox.Show(this, PasswordResetService.PasswordResetMessage, "Password reset",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
        DialogResult = DialogResult.OK;
    }
}
