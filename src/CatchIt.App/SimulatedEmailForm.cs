using System.Runtime.InteropServices;

namespace CatchIt.App;

/// <summary>
/// The pretend confirmation email. The code sits in a read-only text box so it can be
/// highlighted, and the Copy button puts it on the clipboard.
/// </summary>
/// <remarks>
/// The code is deliberately not copied automatically: replacing whatever the user already
/// has on the clipboard without being asked would be an unpleasant surprise.
/// </remarks>
public partial class SimulatedEmailForm : Form
{
    private readonly string _code;

    /// <summary>Creates the simulated email window.</summary>
    /// <param name="email">Address shown on the "To:" line.</param>
    /// <param name="code">The code to display and copy.</param>
    /// <param name="subject">Subject line. Defaults to the account-confirmation email.</param>
    /// <param name="intro">Text above the code. Defaults to the account-confirmation wording.</param>
    public SimulatedEmailForm(string email, string code,
        string subject = "Confirm your CatchIt account", string intro = "Your confirmation code is:")
    {
        _code = code;
        InitializeComponent();
        lblMeta.Text = $"To: {email}\nSubject: {subject}";
        lblIntro.Text = intro;
        txtCode.Text = code;

        // Pre-select the code so it is already highlighted, ready for Ctrl+C.
        Shown += (_, _) => txtCode.SelectAll();
    }

    /// <summary>Copy button: puts the code on the clipboard and confirms by changing the button text.</summary>
    private void btnCopy_Click(object? sender, EventArgs e)
    {
        try
        {
            Clipboard.SetText(_code);
            btnCopy.Text = "Copied!";
        }
        catch (ExternalException)
        {
            // Another program is holding the clipboard; the code can still be highlighted and copied by hand.
            btnCopy.Text = "Couldn't copy, select the code instead";
        }
    }
}
