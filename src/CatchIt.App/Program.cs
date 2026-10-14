using CatchIt.Core.Auth;
using CatchIt.Core.Data;

namespace CatchIt.App;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    ///  Run with "--seed" to populate the database with simulated demo data and exit.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        var db = new Database(Database.DefaultPath);

        if (args.Contains("--seed"))
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            new Seeder(db).Run();
            MessageBox.Show(
                $"Database seeded with simulated demo data in {watch.Elapsed.TotalSeconds:F1}s.\n{Database.DefaultPath}",
                "CatchIt seed");
            return;
        }

        db.Initialize();
        ApplicationConfiguration.Initialize();

        var session = new Session();
        var users = new UserService(db, session);

        // Login -> main window. Logging out (or choosing to log in from guest mode)
        // comes back to the login form; closing either window exits the app.
        while (true)
        {
            using var login = new LoginForm(users, session);
            if (login.ShowDialog() != DialogResult.OK)
                break;

            using var main = new MainForm(users, session);
            main.ShowDialog();
            if (!main.ReturnToLogin)
                break;
        }
    }
}
