using Microsoft.Data.Sqlite;

namespace CatchIt.Core.Data;

/// <summary>
/// The parts of the database schema added for fast search and compact price history:
/// the <c>PriceExtremes</c> table and the <c>ProductSearch</c> full-text index.
/// </summary>
/// <remarks>
/// Kept apart from the main schema because these objects use <c>IF NOT EXISTS</c> checks that
/// also work on databases created before they existed: opening an older database adds them,
/// and a new search index is filled from the products already there.
/// </remarks>
internal static class SearchAndPricingSchema
{
    private const string SearchTable = "ProductSearch";

    private const string PriceExtremesTable = """
        -- Absolute lowest and highest price per listing, with the first day each happened.
        CREATE TABLE IF NOT EXISTS PriceExtremes (
            ListingId INTEGER PRIMARY KEY REFERENCES Listings(Id),
            LowPrice REAL NOT NULL CHECK (LowPrice > 0),
            LowDate TEXT NOT NULL,
            HighPrice REAL NOT NULL CHECK (HighPrice > 0),
            HighDate TEXT NOT NULL
        );
        """;

    // An FTS5 index over product name and keywords. "unicode61" splits text into words and
    // ignores case; prefix queries ("headphon*") find the start of a word. content='Products'
    // means the text itself is not copied: the index only stores which products contain which words.
    private const string SearchIndex = $"""
        CREATE VIRTUAL TABLE IF NOT EXISTS {SearchTable} USING fts5(
            Name, Keywords, content='Products', content_rowid='Id', tokenize='unicode61');
        """;

    // The standard triggers that keep an external-content FTS5 index in step with its table.
    private const string SearchTriggers = $"""
        CREATE TRIGGER IF NOT EXISTS Products_search_insert AFTER INSERT ON Products BEGIN
            INSERT INTO {SearchTable}(rowid, Name, Keywords) VALUES (new.Id, new.Name, new.Keywords);
        END;
        CREATE TRIGGER IF NOT EXISTS Products_search_delete AFTER DELETE ON Products BEGIN
            INSERT INTO {SearchTable}({SearchTable}, rowid, Name, Keywords) VALUES ('delete', old.Id, old.Name, old.Keywords);
        END;
        CREATE TRIGGER IF NOT EXISTS Products_search_update AFTER UPDATE ON Products BEGIN
            INSERT INTO {SearchTable}({SearchTable}, rowid, Name, Keywords) VALUES ('delete', old.Id, old.Name, old.Keywords);
            INSERT INTO {SearchTable}(rowid, Name, Keywords) VALUES (new.Id, new.Name, new.Keywords);
        END;
        """;

    private const string DropSearchTriggersSql = """
        DROP TRIGGER IF EXISTS Products_search_insert;
        DROP TRIGGER IF EXISTS Products_search_delete;
        DROP TRIGGER IF EXISTS Products_search_update;
        """;

    /// <summary>
    /// Makes sure the extremes table and search index exist. Safe to call every time the
    /// database is opened. If the search index is new, it is filled from the existing products.
    /// </summary>
    public static void Ensure(SqliteConnection conn)
    {
        using var tx = conn.BeginTransaction();
        Run(conn, tx, PriceExtremesTable);

        var searchExists = Convert.ToInt64(Scalar(conn, tx,
            $"SELECT COUNT(*) FROM sqlite_master WHERE name = '{SearchTable}'")) > 0;
        Run(conn, tx, SearchIndex);
        Run(conn, tx, SearchTriggers);
        if (!searchExists)
            RebuildSearchIndex(conn, tx);

        tx.Commit();
    }

    /// <summary>
    /// Removes the triggers that update the search index row by row. A bulk import drops them
    /// first, because rebuilding the whole index once at the end is much faster.
    /// </summary>
    public static void DropSearchTriggers(SqliteConnection conn, SqliteTransaction tx) => Run(conn, tx, DropSearchTriggersSql);

    /// <summary>Puts back the triggers removed by <see cref="DropSearchTriggers"/>.</summary>
    public static void CreateSearchTriggers(SqliteConnection conn, SqliteTransaction tx) => Run(conn, tx, SearchTriggers);

    /// <summary>Throws the search index away and rebuilds it from the current products.</summary>
    public static void RebuildSearchIndex(SqliteConnection conn, SqliteTransaction tx) =>
        Run(conn, tx, $"INSERT INTO {SearchTable}({SearchTable}) VALUES ('rebuild');");

    private static void Run(SqliteConnection conn, SqliteTransaction tx, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static object? Scalar(SqliteConnection conn, SqliteTransaction tx, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        return cmd.ExecuteScalar();
    }
}
