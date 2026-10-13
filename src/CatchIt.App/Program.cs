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
        Application.Run(new Form1());
    }
}
