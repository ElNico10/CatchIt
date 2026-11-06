using CatchIt.Core.Data;
using Microsoft.Data.Sqlite;

namespace CatchIt.Tests;

public class SeederTests : IDisposable
{
    private static readonly string[] Tables =
        { "Users", "Marketplaces", "Brands", "Products", "Listings", "PriceHistory", "Alternatives" };

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"catchit-test-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_path)) File.Delete(_path);
    }

    private static long Count(Database db, string table)
    {
        using var conn = db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM {table}";
        return (long)cmd.ExecuteScalar()!;
    }

    [Fact]
    public void Seed_CreatesDemoData()
    {
        var db = new Database(_path);
        new Seeder(db).Run();

        Assert.True(Count(db, "Users") >= 1);
        Assert.True(Count(db, "Marketplaces") >= 3);
        Assert.True(Count(db, "Products") >= 5);
        Assert.True(Count(db, "Listings") >= 5);
        Assert.True(Count(db, "PriceHistory") > 0);
        Assert.True(Count(db, "Alternatives") > 0);
    }

    [Fact]
    public void Seed_RunTwice_CreatesNoDuplicates()
    {
        var db = new Database(_path);
        new Seeder(db).Run();
        var before = Tables.Select(t => Count(db, t)).ToArray();

        new Seeder(db).Run();
        var after = Tables.Select(t => Count(db, t)).ToArray();

        Assert.Equal(before, after);
    }

    [Fact]
    public void Seed_DemoUserPasswordIsBcryptHashed()
    {
        var db = new Database(_path);
        new Seeder(db).Run();

        using var conn = db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT PasswordHash FROM Users WHERE Email = 'demo@catchit.test'";
        var hash = (string)cmd.ExecuteScalar()!;

        Assert.StartsWith("$2", hash);
        Assert.True(BCrypt.Net.BCrypt.Verify("Demo1234!", hash));
    }

    // ---- Execution time (issue #28) ----

    [Fact]
    public void Seed_FreshDatabase_PopulatesEverything_WithinFiveMinutes()
    {
        // Given a fresh database (the file does not exist yet), when the seed command runs...
        Assert.False(File.Exists(_path));
        var db = new Database(_path);

        var result = new Seeder(db).Run();

        // ...then users, products, marketplaces, seller ratings and price history are populated...
        Assert.True(Count(db, "Users") >= 1);
        Assert.True(Count(db, "Products") >= 1);
        Assert.True(Count(db, "Marketplaces") >= 1);
        Assert.True(Count(db, "Listings WHERE SellerRating BETWEEN 0 AND 5") >= 1);
        Assert.Equal(0, Count(db, "Listings WHERE SellerRating IS NULL"));
        Assert.True(Count(db, "PriceHistory") >= 1);

        // ...and the command completes within five minutes.
        Assert.Equal(TimeSpan.FromMinutes(5), Seeder.TimeLimit);
        Assert.True(result.WithinLimit,
            $"Seeding took {result.Elapsed.TotalSeconds:F1}s; the limit is {Seeder.TimeLimit.TotalMinutes:F0} minutes.");
    }

    [Fact]
    public void Seed_Rerun_FinishesWithinTimeLimit()
    {
        var db = new Database(_path);
        new Seeder(db).Run();

        var second = new Seeder(db).Run();

        Assert.True(second.WithinLimit,
            $"Re-seeding took {second.Elapsed.TotalSeconds:F1}s; the limit is {Seeder.TimeLimit.TotalMinutes:F0} minutes.");
    }

    [Fact]
    public void Seed_ReportsTimeItActuallyTook()
    {
        var db = new Database(_path);

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var result = new Seeder(db).Run();
        watch.Stop();

        // The reported time is real (not zero) and covers the run, not more than the caller measured.
        Assert.True(result.Elapsed > TimeSpan.Zero);
        Assert.True(result.Elapsed <= watch.Elapsed);
    }

    [Fact]
    public void SeedResult_WithinLimit_ComparesAgainstTimeLimit()
    {
        Assert.True(new SeedResult(Seeder.TimeLimit).WithinLimit);
        Assert.False(new SeedResult(Seeder.TimeLimit + TimeSpan.FromMilliseconds(1)).WithinLimit);
    }
}
