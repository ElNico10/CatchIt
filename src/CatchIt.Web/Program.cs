using CatchIt.Core.Data;

// "--seed" fills the database with the simulated demo data and exits, the same as the desktop app's --seed.
// The Docker build uses it to ship a ready database inside the image (see Dockerfile and docs/DEPLOYMENT.md).
if (args.Contains("--seed"))
{
    var path = Environment.GetEnvironmentVariable("CATCHIT_DB") ?? Database.DefaultPath;
    var seed = new Seeder(new Database(path)).Run();
    Console.WriteLine($"Database seeded with simulated demo data in {seed.Elapsed.TotalSeconds:F1}s ({path}).");
    Console.WriteLine(seed.WithinLimit
        ? $"Within the {Seeder.TimeLimit.TotalMinutes:F0}-minute limit."
        : $"WARNING: over the {Seeder.TimeLimit.TotalMinutes:F0}-minute limit.");
    return;
}

CatchIt.Web.WebApp.Create(args).Run();
