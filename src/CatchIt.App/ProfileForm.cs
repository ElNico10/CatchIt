using CatchIt.Core.Auth;

namespace CatchIt.App;

/// <summary>
/// Lets the logged-in user edit their name and email (user story #16). The "Change password"
/// section is shown but disabled until password changes are implemented.
/// </summary>
/// <remarks>
/// Only reachable by logged-in users: <see cref="MainForm"/> checks <see cref="Access"/>
/// before opening it, so the form can assume <see cref="Session.CurrentUser"/> is set.
/// </remarks>
public partial class ProfileForm : Form
{
    private readonly ProfileService _profile;

    /// <summary>Creates the profile form, pre-filled with the current details.</summary>
    /// <param name="profile">Validates and saves the changes.</param>
    /// <param name="session">Supplies the current name and email to show.</param>
    public ProfileForm(ProfileService profile, Session session)
    {
        _profile = profile;
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
}
