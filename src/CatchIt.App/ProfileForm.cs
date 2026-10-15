using CatchIt.Core.Auth;

namespace CatchIt.App;

/// <summary>
/// Lets the logged-in user edit their name and email and change their password (user story #16).
/// </summary>
/// <remarks>
/// Only reachable by logged-in users: <see cref="MainForm"/> checks <see cref="Access"/>
/// before opening it, so the form can assume <see cref="Session.CurrentUser"/> is set.
/// </remarks>
public partial class ProfileForm : Form
{
    private readonly ProfileService _profile;
    private readonly UserService _users;

    /// <summary>Creates the profile form, pre-filled with the current details.</summary>
    /// <param name="profile">Validates and saves the changes.</param>
    /// <param name="users">Verifies the current password and stores the new one.</param>
    /// <param name="session">Supplies the current name and email to show.</param>
    public ProfileForm(ProfileService profile, UserService users, Session session)
    {
        _profile = profile;
        _users = users;
        InitializeComponent();

        var user = session.CurrentUser!;
        txtName.Text = user.Name;
        txtEmail.Text = user.Email;
    }

    /// <summary>
    /// Save button (also triggered by Enter). A failure shows the service's message in red and
    /// leaves the typed values in place so they can be corrected.
    /// </summary>
    private void btnSave_Click(object? sender, EventArgs e)
    {
        var result = _profile.UpdateProfile(txtName.Text, txtEmail.Text);
        if (!result.Success)
        {
            lblStatus.ForeColor = Color.Firebrick;
            lblStatus.Text = result.Error;
            return;
        }

        lblStatus.ForeColor = Color.ForestGreen;
        lblStatus.Text = ProfileService.SuccessMessage;

        // Show the saved (trimmed) values so the user sees exactly what was stored.
        txtName.Text = result.User!.Name;
        txtEmail.Text = result.User.Email;
    }

    /// <summary>
    /// Change password button. The service does all the checking (current password, new
    /// password rules, confirmation, must differ) and its message is shown in the password
    /// section's own status line so it never overwrites the name/email message.
    /// </summary>
    /// <remarks>
    /// The current password box is always cleared afterwards so it is not left on screen;
    /// the new password boxes are kept after a failure so they can be corrected, and cleared
    /// after a success.
    /// </remarks>
    private void btnChangePassword_Click(object? sender, EventArgs e)
    {
        var result = _users.ChangePassword(txtCurrent.Text, txtNew.Text, txtConfirmPassword.Text);
        txtCurrent.Clear();

        if (!result.Success)
        {
            lblPasswordStatus.ForeColor = Color.Firebrick;
            lblPasswordStatus.Text = result.Error;
            return;
        }

        txtNew.Clear();
        txtConfirmPassword.Clear();
        lblPasswordStatus.ForeColor = Color.ForestGreen;
        lblPasswordStatus.Text = UserService.PasswordChangedMessage;
    }
}
