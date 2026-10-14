using CatchIt.Core.Auth;

namespace CatchIt.App;

/// <summary>
/// Asks for the confirmation code. Closes with DialogResult.OK once the account is confirmed.
/// The code itself is "delivered" by the ConfirmationService's delivery hook (a simulated
/// email in the demo app), not by this form.
/// </summary>
public partial class ConfirmForm : Form
{
    private readonly ConfirmationService _confirmation;

    /// <summary>Creates the code-entry dialog.</summary>
    /// <param name="confirmation">Checks the code and requests replacements.</param>
    /// <param name="email">The address the code was sent to, shown in the instructions.</param>
    public ConfirmForm(ConfirmationService confirmation, string email)
    {
        _confirmation = confirmation;
        InitializeComponent();
        lblInfo.Text = $"Enter the 6-digit code we sent to {email}.";
    }

    /// <summary>
    /// Confirm button (also triggered by Enter). A wrong, expired or used-up code shows the
    /// service's message and leaves the dialog open so the user can retry or ask for a new code.
    /// </summary>
    private void btnConfirm_Click(object? sender, EventArgs e)
    {
        var result = _confirmation.Confirm(txtCode.Text);
        if (!result.Success)
        {
            lblError.Text = result.Error;
            txtCode.SelectAll();
            txtCode.Focus();
            return;
        }

        DialogResult = DialogResult.OK;
    }

    /// <summary>
    /// "Send me a new code" link. Issuing a new code invalidates the previous one and, in the
    /// demo app, shows another simulated email.
    /// </summary>
    private void lnkResend_LinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
    {
        var result = _confirmation.RequestCode();
        lblError.Text = result.Success ? string.Empty : result.Error;
        txtCode.Clear();
        txtCode.Focus();
    }
}
