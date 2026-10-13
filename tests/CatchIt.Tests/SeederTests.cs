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
}
