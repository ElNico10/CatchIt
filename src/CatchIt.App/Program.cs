using CatchIt.Core.Auth;
using CatchIt.Core.Data;
using CatchIt.Core.Search;

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
            var seed = new Seeder(db).Run();
            var verdict = seed.WithinLimit
                ? $"Within the {Seeder.TimeLimit.TotalMinutes:F0}-minute limit."
                : $"WARNING: slower than the {Seeder.TimeLimit.TotalMinutes:F0}-minute limit.";
            MessageBox.Show(
                $"Database seeded with simulated demo data in {seed.Elapsed.TotalSeconds:F1}s.\n{verdict}\n{Database.DefaultPath}",
                "CatchIt seed",
                MessageBoxButtons.OK,
                seed.WithinLimit ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            return;
        }

        // Import mode: "--import <file> [--limit N]" loads an Amazon Reviews 2023 metadata file
        // (meta_*.jsonl.gz) and exits. There is no window while it runs; it can take several minutes.
        var importAt = Array.IndexOf(args, "--import");
        if (importAt >= 0)
        {
            RunImport(db, args, importAt);
            return;
        }

        // Creates the tables on first run; does nothing if they already exist.
        db.Initialize();
        ApplicationConfiguration.Initialize();

        // One session is shared by every service and form so they all agree on who is logged in.
        var session = new Session();
        var users = new UserService(db, session);
        var confirmation = new ConfirmationService(db, session, DeliverSimulatedEmail);
        var passwordReset = new PasswordResetService(db, DeliverSimulatedResetEmail);
        var profile = new ProfileService(db, session);
        var listingSearch = new ListingSearchService(db);
        var productDetails = new ProductDetailService(db);
        var watchlist = new WatchlistService(db, session);
        var dashboard = new DashboardService(watchlist, db);

        // Login -> main window. Logging out (or choosing to log in from guest mode)
        // comes back to the login form; closing either window exits the app.
        while (true)
        {
            using var login = new LoginForm(users, session, passwordReset);
            if (login.ShowDialog() != DialogResult.OK)
                break;

            using var main = new MainForm(users, session, confirmation, profile, listingSearch, productDetails, watchlist, dashboard);
            main.ShowDialog();
            if (!main.ReturnToLogin)
                break;
        }
    }

    /// <summary>Runs <c>--import</c> and reports the outcome (or the problem) in a message box.</summary>
    /// <param name="db">The database to import into.</param>
    /// <param name="args">The command line.</param>
    /// <param name="importAt">Where <c>--import</c> is in <paramref name="args"/>; the file path follows it.</param>
    private static void RunImport(Database db, string[] args, int importAt)
    {
        var path = importAt + 1 < args.Length ? args[importAt + 1] : null;
        var limitAt = Array.IndexOf(args, "--limit");
        int? limit = limitAt >= 0 && limitAt + 1 < args.Length && int.TryParse(args[limitAt + 1], out var n) && n > 0 ? n : null;

        if (path == null || !File.Exists(path))
        {
            MessageBox.Show("Usage: CatchIt.App.exe --import <meta_*.jsonl.gz> [--limit N]\nThe file was not found.", "CatchIt import");
            return;
        }

        try
        {
            var r = AmazonImporter.Run(db, path, limit);
            MessageBox.Show(
                $"Imported in {r.Elapsed.TotalMinutes:F1} min.\n" +
                $"Products added: {r.Import.ProductsAdded:N0} (duplicates skipped: {r.Import.ProductsSkipped:N0}, rows without title/brand/price: {r.RowsSkipped:N0})\n" +
                $"Listings: {r.Import.ListingsAdded:N0}   History points: {r.Import.HistoryPointsAdded:N0}\n" +
                $"Brand tiers derived: {r.BrandsTiered:N0}   Alternative links: {r.AlternativeLinks:N0}\n" +
                $"Database: {new FileInfo(Database.DefaultPath).Length / 1_000_000:N0} MB",
                "CatchIt import");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"The import failed and the database was left as it was:\n{ex.Message}", "CatchIt import");
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

    /// <summary>
    /// The password-reset "mechanism": the same free, simulated email as confirmation, with the
    /// reset wording. Passed to <see cref="PasswordResetService"/> as its delivery hook.
    /// </summary>
    /// <param name="email">Address the pretend email is "sent" to.</param>
    /// <param name="code">The reset code to show the user.</param>
    private static void DeliverSimulatedResetEmail(string email, string code)
    {
        using var message = new SimulatedEmailForm(email, code,
            "Reset your CatchIt password", "Your password reset code is:");
        message.ShowDialog();
    }
}
