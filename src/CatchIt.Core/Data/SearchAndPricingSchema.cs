using Microsoft.Data.Sqlite;

namespace CatchIt.Core.Data;

/// <summary>
/// The parts of the database schema added for fast search and compact price history:
/// the <c>PriceHistory</c> and <c>PriceExtremes</c> tables and the <c>ProductSearch</c> full-text index.
/// </summary>
/// <remarks>
/// Kept apart from the main schema because these objects use checks that also work on databases
/// created before they existed: opening an older database adds them, fills a new search index
/// from the products already there, and upgrades an old-layout <c>PriceHistory</c> table.
/// </remarks>
internal static class SearchAndPricingSchema
{
    private const string SearchTable = "ProductSearch";

    // Lets the search screen list the cheapest listings in the whole catalog (no search text) by reading
    // them in price order and stopping early, instead of sorting every listing.
    private const string ListingPriceIndex =
        "CREATE INDEX IF NOT EXISTS Listings_CurrentPrice ON Listings(CurrentPrice);";

    /// <summary>
    /// The price history table. The (listing, date) pair is the primary key and the table is stored
    /// directly in that key's order (<c>WITHOUT ROWID</c>), so there is no separate id column or second
    /// index to maintain. That makes it about 45% smaller and about 1.5 times faster to fill than the
    /// original layout, and a listing's history sits together on disk, which suits reading one listing.
    /// </summary>
    private static string PriceHistoryTable(string name) => $"""
        CREATE TABLE IF NOT EXISTS {name} (
            ListingId INTEGER NOT NULL REFERENCES Listings(Id),
            Date TEXT NOT NULL,
            Price REAL NOT NULL CHECK (Price > 0),
            PRIMARY KEY (ListingId, Date)
        ) WITHOUT ROWID;
        """;

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
        bool migratedHistory;
        using (var tx = conn.BeginTransaction())
        {
            Run(conn, tx, PriceExtremesTable);
            Run(conn, tx, ListingPriceIndex);
            migratedHistory = EnsurePriceHistory(conn, tx);

            var searchExists = Convert.ToInt64(Scalar(conn, tx,
                $"SELECT COUNT(*) FROM sqlite_master WHERE name = '{SearchTable}'")) > 0;
            Run(conn, tx, SearchIndex);
            Run(conn, tx, SearchTriggers);
            if (!searchExists)
                RebuildSearchIndex(conn, tx);

            tx.Commit();
        }

        if (migratedHistory)
            ReclaimSpace(conn);
    }

    /// <summary>
    /// Creates the price history table, or, if the database has the original layout (with an
    /// <c>Id</c> column), copies every row into the compact layout and swaps it in. All of it
    /// happens in the caller's transaction, so a failure leaves the original table untouched.
    /// </summary>
    /// <returns>True when an old table was converted.</returns>
    private static bool EnsurePriceHistory(SqliteConnection conn, SqliteTransaction tx)
    {
        var hasOldLayout = Convert.ToInt64(Scalar(conn, tx,
            "SELECT COUNT(*) FROM pragma_table_info('PriceHistory') WHERE name = 'Id'")) > 0;

        if (!hasOldLayout)
        {
            Run(conn, tx, PriceHistoryTable("PriceHistory"));
            return false;
        }

        Run(conn, tx, PriceHistoryTable("PriceHistory_compact"));
        // Copied in key order, which is how the new table stores rows, so the copy is a straight append.
        Run(conn, tx, """
            INSERT INTO PriceHistory_compact (ListingId, Date, Price)
            SELECT ListingId, Date, Price FROM PriceHistory ORDER BY ListingId, Date;
            """);
        Run(conn, tx, "DROP TABLE PriceHistory;");
        Run(conn, tx, "ALTER TABLE PriceHistory_compact RENAME TO PriceHistory;");
        return true;
    }

    /// <summary>
    /// Shrinks the database file after the conversion. SQLite keeps the freed pages inside the
    /// file for reuse, so without this the file would stay as large as before. Skipped quietly if
    /// the database is busy: the data is already converted, only the file size is affected.
    /// </summary>
    private static void ReclaimSpace(SqliteConnection conn)
    {
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "VACUUM;";
            cmd.ExecuteNonQuery();
        }
        catch (SqliteException)
        {
            // Another connection is using the database; the space will be reused by later inserts.
        }
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
