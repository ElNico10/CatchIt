using CatchIt.Core.Data;
using Microsoft.Data.Sqlite;

namespace CatchIt.Tests;

/// <summary>
/// Base class for tests that need a real, empty database. Each test instance gets its own throwaway file,
/// created and initialised before the test and deleted afterwards, so tests never share data.
/// </summary>
public abstract class TempDatabaseTests : IDisposable
{
    /// <summary>Where the test database file lives, for tests that inspect the file itself.</summary>
    protected string DbPath { get; } = Path.Combine(Path.GetTempPath(), $"catchit-test-{Guid.NewGuid():N}.db");

    /// <summary>The test's own database, with its tables created.</summary>
    protected Database Db { get; }

    protected TempDatabaseTests()
    {
        Db = new Database(DbPath);
        Db.Initialize();
    }

    public virtual void Dispose()
    {
        // SQLite keeps pooled connections open, which would stop the file being deleted.
        SqliteConnection.ClearAllPools();
        if (File.Exists(DbPath)) File.Delete(DbPath);
        GC.SuppressFinalize(this);
    }

    /// <summary>Runs one SQL statement against the test database, with optional named parameters.</summary>
    protected void Sql(string sql, params (string Name, object? Value)[] args)
    {
        using var conn = Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in args) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }
}
