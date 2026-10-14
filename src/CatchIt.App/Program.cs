using CatchIt.Core.Auth;
using CatchIt.Core.Data;

namespace CatchIt.App;

/// <summary>
/// Application entry point. Builds the shared services and runs the login and main windows.
/// </summary>
static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    ///  Run with "--seed" to populate the database with simulated demo data and exit.
    /// </summary>
    /// <param name="args">Command-line arguments. Only <c>--seed</c> is recognised.</param>
    [STAThread]
    static void Main(string[] args)
    {
        // The database lives in the user's local app data folder, outside the repository.
        var db = new Database(Database.DefaultPath);

        // Seed mode: fill the database with demo data, report how long it took, and exit
        // without showing any of the normal windows.
        if (args.Contains("--seed"))
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            new Seeder(db).Run();
            MessageBox.Show(
                $"Database seeded with simulated demo data in {watch.Elapsed.TotalSeconds:F1}s.\n{Database.DefaultPath}",
                "CatchIt seed");
            return;
        }

        // Creates the tables on first run; does nothing if they already exist.
        db.Initialize();
        ApplicationConfiguration.Initialize();

        // One session is shared by every service and form so they all agree on who is logged in.
        var session = new Session();
        var users = new UserService(db, session);
        var confirmation = new ConfirmationService(db, session, DeliverSimulatedEmail);
        var profile = new ProfileService(db, session);

        // Login -> main window. Logging out (or choosing to log in from guest mode)
        // comes back to the login form; closing either window exits the app.
        while (true)
        {
            using var login = new LoginForm(users, session);
            if (login.ShowDialog() != DialogResult.OK)
                break;

            using var main = new MainForm(users, session, confirmation, profile);
            main.ShowDialog();
            if (!main.ReturnToLogin)
                break;
        }
    }

    /// <summary>
    /// The configured confirmation "mechanism": a free, simulated email shown on screen.
    /// No email provider is used and nothing leaves the machine.
    /// </summary>
    /// <param name="email">Address the pretend email is "sent" to.</param>
    /// <param name="code">The confirmation code to show the user.</param>
    /// <remarks>
    /// Passed to <see cref="ConfirmationService"/> as its delivery hook. Swapping in a real
    /// email sender later only means replacing this method.
    /// </remarks>
    private static void DeliverSimulatedEmail(string email, string code)
    {
        using var message = new SimulatedEmailForm(email, code);
        message.ShowDialog();
    }
}
